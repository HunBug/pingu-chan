using PinguChan.Core;
using PinguChan.Core.Models;
using PinguChan.Core.Runtime;

namespace PinguChan.Orchestration;

public interface ITargetPools
{
    // Returns (targetKey, baseDelay) or null if nothing eligible right now
    (string targetKey, TimeSpan delay)? TryGetNext(string kind, DateTimeOffset now);
    void Report(string kind, string targetKey, bool ok);
    IReadOnlyList<TargetPoolDiagnostic> GetDiagnostics(string kind);
}

public sealed record TargetPoolDiagnostic(
    string Key,
    DateTimeOffset NextDue,
    int FailureCount,
    bool InFlight,
    TimeSpan MinInterval
);

public interface IStatsService
{
    void Observe(NetSample sample);
    // Example aggregate query (expand later)
    (int count, int ok, double failPct)? TryGetWindow(string kind, string targetKey, TimeSpan window, DateTimeOffset now);
}

public interface IRulesService
{
    RuleFinding? Evaluate(IReadOnlyList<NetSample> recent, DateTimeOffset now);
}

public interface ITriggerEngine
{
    void OnTick(DateTimeOffset now);
}

public sealed class MonitorOrchestrator : IAsyncDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly ITargetPools _pools;
    private readonly IStatsService _stats;
    private readonly IRulesService? _rules;
    private readonly Func<string, IProbe> _pingProbeFactory;
    private Task? _loop;

    public MonitorOrchestrator(
        ITargetPools pools,
        IStatsService stats,
        Func<string, IProbe> pingProbeFactory,
        IRulesService? rules = null)
    {
        _pools = pools;
        _stats = stats;
        _rules = rules;
        _pingProbeFactory = pingProbeFactory;
    }

    public ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _cts.Dispose();
        return ValueTask.CompletedTask;
    }

    public Task StartAsync()
    {
        _loop = Task.Run(SchedulerLoop);
        return Task.CompletedTask;
    }
    public Task StopAsync()
    {
        _cts.Cancel();
        return Task.CompletedTask;
    }

    public IAsyncEnumerable<RuleFinding> Findings(CancellationToken ct = default)
        => FindingsBus.ReadAllAsync(ct);

    private async Task SchedulerLoop()
    {
        var now = DateTimeOffset.UtcNow;
        var probes = new Dictionary<string, IProbe>();
        while (!_cts.IsCancellationRequested)
        {
            now = DateTimeOffset.UtcNow;
            var next = _pools.TryGetNext("ping", now);
            if (next is null)
            {
                try { await Task.Delay(200, _cts.Token); } catch { }
                continue;
            }
            var (key, delay) = next.Value;
            if (delay > TimeSpan.Zero)
            {
                try { await Task.Delay(delay, _cts.Token); } catch { }
                continue;
            }

            if (!probes.TryGetValue(key, out var probe))
            {
                probe = _pingProbeFactory(key);
                probes[key] = probe;
            }
            NetSample sample;
            try
            {
                sample = await probe.ExecuteAsync(_cts.Token);
            }
            catch
            {
                sample = new NetSample(now, SampleKind.Ping, key, false, null, null);
            }

                // Tag pool/key into Extra for downstream consumers using typed codec
                var meta = SampleMetaCodec.TryParse(sample.Extra) ?? new PinguChan.Core.Models.SampleMeta();
                meta = meta with { Pool = "ping", Key = key };
                var tagged = sample with { Extra = SampleMetaCodec.Serialize(meta) };
            _stats.Observe(tagged);
            await SamplesBus.PublishAsync(tagged, _cts.Token);
            _pools.Report("ping", key, sample.Ok);

            if (_rules is not null)
            {
                // very small local window from stats not available; for now, evaluate on singleton
                var finding = _rules.Evaluate(new List<NetSample> { sample }, now);
                if (finding is not null) await FindingsBus.PublishAsync(finding, _cts.Token);
            }
        }
    }
}
