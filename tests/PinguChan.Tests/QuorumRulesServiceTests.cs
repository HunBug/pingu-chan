using PinguChan.Core.Models;
using PinguChan.Orchestration;

public class QuorumRulesServiceTests
{
    [Fact]
    public void Emits_When_FailureRate_Exceeds_Threshold()
    {
        var now = DateTimeOffset.Parse("2024-01-01T00:00:10Z");
        var window = TimeSpan.FromSeconds(10);
        var rule = new QuorumRulesService(window, failThreshold: 0.5, minSamples: 4);
        var target = "host";
        var samples = new List<NetSample>
        {
            new(now - TimeSpan.FromSeconds(9), SampleKind.Ping, target, false, null, null),
            new(now - TimeSpan.FromSeconds(7), SampleKind.Ping, target, true, 10, null),
            new(now - TimeSpan.FromSeconds(5), SampleKind.Ping, target, false, null, null),
            new(now - TimeSpan.FromSeconds(1), SampleKind.Ping, target, true, 12, null)
        };
        var f = rule.Evaluate(samples, now);
        Assert.NotNull(f);
        Assert.Equal("quorum_fail", f!.RuleId);
    }

    [Fact]
    public void NoFinding_When_Below_Threshold_Or_Insufficient_Samples()
    {
        var now = DateTimeOffset.Parse("2024-01-01T00:00:10Z");
        var window = TimeSpan.FromSeconds(10);
        var rule = new QuorumRulesService(window, failThreshold: 0.75, minSamples: 4);
        var target = "host";
        var samples = new List<NetSample>
        {
            new(now - TimeSpan.FromSeconds(9), SampleKind.Ping, target, false, null, null),
            new(now - TimeSpan.FromSeconds(7), SampleKind.Ping, target, true, 10, null),
            new(now - TimeSpan.FromSeconds(5), SampleKind.Ping, target, false, null, null),
            new(now - TimeSpan.FromSeconds(1), SampleKind.Ping, target, true, 12, null)
        };
        var f = rule.Evaluate(samples, now);
        Assert.Null(f);

        // Also check insufficient samples
        var rule2 = new QuorumRulesService(window, failThreshold: 0.5, minSamples: 5);
        var f2 = rule2.Evaluate(samples, now);
        Assert.Null(f2);
    }
}
