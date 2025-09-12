using PinguChan.Core.Models;

namespace PinguChan.Orchestration;

public sealed class QuorumRulesService : IRulesService
{
    private readonly TimeSpan _window;
    private readonly double _failThreshold; // 0..1
    private readonly int _minSamples;
    private readonly string _ruleId;
    private readonly Severity _severity;

    public QuorumRulesService(TimeSpan window, double failThreshold, int minSamples, string ruleId = "quorum_fail", Severity severity = Severity.Warning)
    {
        _window = window > TimeSpan.Zero ? window : TimeSpan.FromMinutes(1);
        _failThreshold = Math.Clamp(failThreshold, 0, 1);
        _minSamples = Math.Max(1, minSamples);
        _ruleId = ruleId;
        _severity = severity;
    }

    public RuleFinding? Evaluate(IReadOnlyList<NetSample> recent, DateTimeOffset now)
    {
        if (recent.Count == 0) return null;
        var cutoff = now - _window;
        int total = 0, fails = 0;
        string target = recent[^1].Target;
        string pool = SampleMetaCodec.TryParse(recent[^1].Extra)?.Pool ?? string.Empty;
        for (int i = recent.Count - 1; i >= 0; i--)
        {
            var s = recent[i];
            if (s.Timestamp < cutoff) break;
            if (s.Target != target) break; // defensive: list should be per-target
            total++;
            if (!s.Ok) fails++;
        }
        if (total < _minSamples) return null;
        var failPct = (double)fails / total;
        if (failPct >= _failThreshold)
        {
            var msg = $"{target}: failure rate {(failPct*100):0.#}% over {_window.TotalSeconds:0}s ({fails}/{total})";
            var ctx = new Dictionary<string, object>
            {
                ["target"] = target,
                ["pool"] = pool,
                ["count"] = total,
                ["fails"] = fails,
                ["failPct"] = failPct,
                ["windowSec"] = _window.TotalSeconds
            };
            return new RuleFinding(now, _ruleId, _severity, msg, ctx);
        }
        return null;
    }
}
