using PinguChan.Core.Models;

namespace PinguChan.Orchestration;

public sealed class ConsecutiveFailRulesService(int threshold = 3, string ruleId = "consecutive_fail", Severity severity = Severity.Warning) : IRulesService
{
    private readonly int _threshold = Math.Max(1, threshold);
    private readonly string _ruleId = ruleId;
    private readonly Severity _severity = severity;

    public RuleFinding? Evaluate(IReadOnlyList<NetSample> recent, DateTimeOffset now)
    {
        if (recent.Count < _threshold) return null;
        int streak = 0;
        string? target = null;
        for (int i = recent.Count - 1; i >= 0 && streak < _threshold; i--)
        {
            var s = recent[i];
            target ??= s.Target;
            if (!s.Ok && s.Target == target) streak++; else break;
        }
        if (streak >= _threshold)
        {
            var msg = $"{target}: {streak} consecutive failures";
            var meta = SampleMetaCodec.TryParse(recent[^1].Extra);
            var ctx = new Dictionary<string, object>
            {
                ["target"] = target!,
                ["streak"] = streak,
                ["pool"] = meta?.Pool ?? "",
                ["key"] = meta?.Key ?? target!
            };
            return new RuleFinding(now, _ruleId, _severity, msg, ctx);
        }
        return null;
    }
}
