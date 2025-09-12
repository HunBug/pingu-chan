using PinguChan.Core.Models;
using PinguChan.Core.Runtime;

namespace PinguChan.Orchestration;

public sealed class TriggerEngine : ITriggerEngine
{
    public sealed record TriggerSpec(string Id, TimeSpan Debounce, TimeSpan Cooldown, Func<NetSample, bool> Predicate, Func<CancellationToken, Task<NetSample[]>> Action);

    private readonly List<TriggerSpec> _triggers = new();
    private readonly Dictionary<string, (DateTimeOffset lastFire, bool running)> _state = new();
    private readonly object _gate = new();

    public TriggerEngine AddTrigger(TriggerSpec spec)
    {
        _triggers.Add(spec);
        _state.TryAdd(spec.Id, (DateTimeOffset.MinValue, false));
        return this;
    }

    // In this simplified design, Observe buffers “armed” time and OnTick evaluates debounce/cooldown
    private sealed record Cause(DateTimeOffset ArmedAt, string Source, string? Detail);
    private readonly Dictionary<string, Cause> _armed = new();

    public void Observe(NetSample sample)
    {
        foreach (var t in _triggers)
        {
            if (t.Predicate(sample))
            {
                var meta = SampleMetaCodec.TryParse(sample.Extra);
                var src = meta is null ? "(unknown)" : $"{meta.Pool}:{meta.Key}";
                var detail = BuildCauseDetail(sample);
                if (_armed.TryAdd(t.Id, new Cause(sample.Timestamp, src, detail)))
                {
                    LoggerHelper.LogInfo($"[orchestrator] trigger armed id={t.Id} source={src} at={sample.Timestamp:HH:mm:ss} cause={detail}");
                }
            }
        }
    }

    public void OnTick(DateTimeOffset now)
    {
        foreach (var t in _triggers)
        {
            if (!_state.TryGetValue(t.Id, out var st)) st = (DateTimeOffset.MinValue, false);
            if (!_armed.TryGetValue(t.Id, out var cause)) continue;

            // Debounce window not satisfied yet
            if (now - cause.ArmedAt < t.Debounce) continue;

            // Cooldown active
            if (now - st.lastFire < t.Cooldown) continue;

            // Concurrency guard: skip if running
            if (st.running) continue;
            LoggerHelper.LogInfo($"[orchestrator] trigger firing id={t.Id} armedFor={(now - cause.ArmedAt).TotalSeconds:F1}s cooldown={t.Cooldown.TotalSeconds:F0}s source={cause.Source} cause={cause.Detail}");
            _ = RunActionAsync(t, now);
        }
    }

    private async Task RunActionAsync(TriggerSpec spec, DateTimeOffset now)
    {
        lock (_gate)
        {
            var st = _state[spec.Id];
            if (st.running) return;
            _state[spec.Id] = (st.lastFire, true);
        }
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            LoggerHelper.LogInfo($"[orchestrator] trigger action start id={spec.Id}");
            var samples = await spec.Action(cts.Token);
            // Emit action results as normal samples
            foreach (var s in samples)
                await SamplesBus.PublishAsync(s);
            // Emit a finding that trigger fired
            var ctx = new Dictionary<string, object> { ["trigger"] = spec.Id, ["results"] = samples.Length };
            await FindingsBus.PublishAsync(new RuleFinding(now, $"trigger:{spec.Id}", Severity.Info, $"Trigger {spec.Id} executed", ctx));
            LoggerHelper.LogInfo($"[orchestrator] trigger action done id={spec.Id} results={samples.Length}");
        }
        catch
        {
            await FindingsBus.PublishAsync(new RuleFinding(now, $"trigger:{spec.Id}", Severity.Warning, $"Trigger {spec.Id} failed"));
            LoggerHelper.LogWarn($"[orchestrator] trigger action failed id={spec.Id}");
        }
        finally
        {
            lock (_gate)
            {
                _state[spec.Id] = (now, false);
                _armed.Remove(spec.Id);
            }
        }
    }

    private static string? BuildCauseDetail(NetSample sample)
    {
        // Provide a compact cause: kind=..., target=..., ok=..., ms=..., extra(meta)
        var meta = SampleMetaCodec.TryParse(sample.Extra);
        var parts = new List<string>
        {
            $"kind={sample.Kind}",
            $"target={sample.Target}",
            $"ok={(sample.Ok ? "yes" : "no")}"
        };
        if (sample.Milliseconds is { } ms) parts.Add($"ms={ms}");
        if (meta is not null && (!string.IsNullOrWhiteSpace(meta.Pool) || !string.IsNullOrWhiteSpace(meta.Key)))
            parts.Add($"meta={meta.Pool}:{meta.Key}");
        return string.Join(' ', parts);
    }
}
