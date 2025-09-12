using PinguChan.Core.Models;

namespace PinguChan.Orchestration;

public sealed class CompositeRulesService : IRulesService
{
    private readonly IReadOnlyList<IRulesService> _rules;
    public CompositeRulesService(IEnumerable<IRulesService> rules) => _rules = rules.ToList();

    public RuleFinding? Evaluate(IReadOnlyList<NetSample> recent, DateTimeOffset now)
    {
        foreach (var r in _rules)
        {
            var f = r.Evaluate(recent, now);
            if (f is not null) return f;
        }
        return null;
    }
}
