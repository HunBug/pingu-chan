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
    private readonly IReadOnlyDictionary<string, Func<string, IProbe>> _probeFactories;
    private readonly List<string> _kinds;
    private Task? _loop;

    public MonitorOrchestrator(
        ITargetPools pools,
        IStatsService stats,
        IReadOnlyDictionary<string, Func<string, IProbe>> probeFactories,
        IRulesService? rules = null)
    {
        _pools = pools;
        _stats = stats;
        _rules = rules;
        _probeFactories = new Dictionary<string, Func<string, IProbe>>(probeFactories, StringComparer.OrdinalIgnoreCase);
        _kinds = _probeFactories.Keys.OrderBy(k => k).ToList();
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
        var rrIndex = 0;
        while (!_cts.IsCancellationRequested)
        {
            now = DateTimeOffset.UtcNow;
            (string kind, string key, TimeSpan delay)? pick = null;
            TimeSpan minDelay = TimeSpan.MaxValue;

            for (int i = 0; i < _kinds.Count; i++)
            {
                var kindIdx = (rrIndex + i) % _kinds.Count;
                var kind = _kinds[kindIdx];
                var next = _pools.TryGetNext(kind, now);
                if (next is null) continue;
                var (key, delay) = next.Value;
                if (delay == TimeSpan.Zero)
                {
                    pick = (kind, key, delay);
                    rrIndex = (kindIdx + 1) % _kinds.Count; // advance RR for fairness
                    break;
                }
                else if (delay < minDelay)
                {
                    minDelay = delay;
                }
            }

            if (pick is null)
            {
                var wait = minDelay == TimeSpan.MaxValue ? TimeSpan.FromMilliseconds(100) : minDelay;
                try { await Task.Delay(wait, _cts.Token); } catch { }
                continue;
            }

            var (selKind, key2, _) = pick.Value;
            var probeKey = $"{selKind}:{key2}";
            if (!probes.TryGetValue(probeKey, out var probe))
            {
                var factory = _probeFactories[selKind];
                probe = factory(key2);
                probes[probeKey] = probe;
            }

            NetSample sample;
            try
            {
                sample = await probe.ExecuteAsync(_cts.Token);
            }
            catch
            {
                // Build a sample with kind inferred from selection
                var kindEnum = selKind.ToLowerInvariant() switch
                {
                    "ping" => SampleKind.Ping,
                    "dns" => SampleKind.Dns,
                    "http" => SampleKind.Http,
                    _ => SampleKind.Ping
                };
                sample = new NetSample(now, kindEnum, key2, false, null, null);
            }

            // Tag pool/key into Extra for downstream consumers using typed codec
            var meta = SampleMetaCodec.TryParse(sample.Extra) ?? new PinguChan.Core.Models.SampleMeta();
            meta = meta with { Pool = selKind, Key = key2 };
            var tagged = sample with { Extra = SampleMetaCodec.Serialize(meta) };
            _stats.Observe(tagged);
            await SamplesBus.PublishAsync(tagged, _cts.Token);
            _pools.Report(selKind, key2, sample.Ok);

            if (_rules is not null)
            {
                // very small local window from stats not available; for now, evaluate on singleton
                var finding = _rules.Evaluate(new List<NetSample> { sample }, now);
                if (finding is not null) await FindingsBus.PublishAsync(finding, _cts.Token);
            }
        }
    }
}
