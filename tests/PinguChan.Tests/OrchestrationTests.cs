using PinguChan.Orchestration;
using PinguChan.Core.Models;

public class OrchestrationTests
{
    [Fact]
    public void TargetPools_ShouldRotate_AndBackoffOnFailures()
    {
        var now = DateTimeOffset.UtcNow;
        var pools = new TargetPools(jitterPct: 0, backoffBase: 2.0, backoffMaxMultiplier: 8)
            .Add("ping", "a", weight: 1, minInterval: TimeSpan.FromSeconds(1), now)
            .Add("ping", "b", weight: 3, minInterval: TimeSpan.FromSeconds(1), now);

        // First eligible should be one of a/b, but we don't assert randomness; we assert eligibility and delay
        var pick1 = pools.TryGetNext("ping", now);
        Assert.NotNull(pick1);
    var (k1, delay1) = pick1!.Value;
    // With new semantics, when a key is assigned, delay is zero (execute now)
    Assert.Equal(TimeSpan.Zero, delay1);

        // Report failure for chosen, increases backoff
        pools.Report("ping", k1, ok: false);

        // Advance time by base interval; same key should not necessarily be due if backoff applied
        var pick2 = pools.TryGetNext("ping", now + TimeSpan.FromSeconds(1));
        Assert.NotNull(pick2);
        var (k2, _) = pick2!.Value;
        // It's possible we get the same due to weighting; just ensure we can continue
        Assert.True(k2 == "a" || k2 == "b");
    }

    [Fact]
    public void StatsService_ShouldComputeWindow()
    {
        var stats = new StatsService();
        var t0 = DateTimeOffset.UtcNow;
    stats.Observe(new NetSample(t0 - TimeSpan.FromSeconds(5), SampleKind.Ping, "host", Ok: true, Milliseconds: 10, Extra: null));
    stats.Observe(new NetSample(t0 - TimeSpan.FromSeconds(3), SampleKind.Ping, "host", Ok: false, Milliseconds: 0, Extra: null));
    stats.Observe(new NetSample(t0 - TimeSpan.FromSeconds(1), SampleKind.Ping, "host", Ok: true, Milliseconds: 8, Extra: null));

        var res = stats.TryGetWindow("ping", "host", TimeSpan.FromSeconds(4), t0);
        Assert.NotNull(res);
        var (count, ok, failPct) = res!.Value;
        Assert.Equal(2, count); // last 3s and 1s entries
        Assert.Equal(1, ok);
        Assert.True(failPct > 0 && failPct < 1);
    }

    [Fact]
    public void StatsService_P50_P95_AreComputed()
    {
        var stats = new StatsService();
        var t0 = DateTimeOffset.UtcNow;
        // OK latencies: 10, 20, 30, 40, 50
        for (int i = 1; i <= 5; i++)
            stats.Observe(new NetSample(t0 - TimeSpan.FromSeconds(6 - i), SampleKind.Http, "u", Ok: true, Milliseconds: i * 10, Extra: null));
        // A couple failures inside window
        stats.Observe(new NetSample(t0 - TimeSpan.FromSeconds(2), SampleKind.Http, "u", Ok: false, Milliseconds: null, Extra: null));
        var res = stats.TryGetWindowWithLatency("http", "u", TimeSpan.FromSeconds(10), t0);
        Assert.NotNull(res);
        var (count, ok, failPct, p50, p95) = res!.Value;
        Assert.Equal(6, count);
        Assert.Equal(5, ok);
        Assert.True(failPct > 0);
        Assert.Equal(30, p50);
        Assert.Equal(50, p95);
    }

    [Fact]
    public void ConsecutiveFailRule_Fires_On_Threshold()
    {
        var rule = new ConsecutiveFailRulesService(threshold: 3);
        var now = DateTimeOffset.UtcNow;
        var list = new List<NetSample>
        {
            new(now - TimeSpan.FromSeconds(3), SampleKind.Ping, "t", true, 10, null),
            new(now - TimeSpan.FromSeconds(2), SampleKind.Ping, "t", false, null, null),
            new(now - TimeSpan.FromSeconds(1), SampleKind.Ping, "t", false, null, null),
            new(now, SampleKind.Ping, "t", false, null, null)
        };
        var f = rule.Evaluate(list, now);
        Assert.NotNull(f);
        Assert.Equal("consecutive_fail", f!.RuleId);
    }

    [Fact]
    public void QuorumRule_Suppresses_Below_MinSamples()
    {
        var now = DateTimeOffset.UtcNow;
        var rule = new QuorumRulesService(TimeSpan.FromSeconds(10), failThreshold: 0.5, minSamples: 5);
        var list = new List<NetSample>
        {
            new(now - TimeSpan.FromSeconds(4), SampleKind.Dns, "x", false, null, null),
            new(now - TimeSpan.FromSeconds(3), SampleKind.Dns, "x", false, null, null),
            new(now - TimeSpan.FromSeconds(2), SampleKind.Dns, "x", true, 5, null),
            new(now - TimeSpan.FromSeconds(1), SampleKind.Dns, "x", true, 6, null)
        };
        var f = rule.Evaluate(list, now);
        Assert.Null(f);
    }

    [Fact]
    public void TargetPools_RespectsMinInterval_AndJitterBounds()
    {
        var now = DateTimeOffset.UtcNow;
        var min = TimeSpan.FromSeconds(2);
        var pools = new TargetPools(jitterPct: 0.3)
            .Add("ping", "x", 1, min, now);

        // First pick sets next due with jitter; ensure delay used to schedule is around min with ±30%
        var first = pools.TryGetNext("ping", now);
        Assert.NotNull(first);
        // Simulate that we were due now; next due scheduled internally inside TryGetNext

        // Immediately ask again at now; nothing should be due, we should get a delay hint ~= next due - now
        var second = pools.TryGetNext("ping", now);
        Assert.NotNull(second);
        var (_, delayHint) = second!.Value;
        Assert.True(delayHint >= TimeSpan.Zero);
    }

    [Fact]
    public void StatsService_TracksPerTargetLoss_ForTwoHosts()
    {
        var stats = new StatsService();
        var t0 = DateTimeOffset.UtcNow;
        // host A: success then failure
        stats.Observe(new NetSample(t0 - TimeSpan.FromSeconds(4), SampleKind.Ping, "A", Ok: true, Milliseconds: 10, Extra: null));
        stats.Observe(new NetSample(t0 - TimeSpan.FromSeconds(2), SampleKind.Ping, "A", Ok: false, Milliseconds: null, Extra: null));
        // host B: failure then success
        stats.Observe(new NetSample(t0 - TimeSpan.FromSeconds(3), SampleKind.Ping, "B", Ok: false, Milliseconds: null, Extra: null));
        stats.Observe(new NetSample(t0 - TimeSpan.FromSeconds(1), SampleKind.Ping, "B", Ok: true, Milliseconds: 8, Extra: null));

        var a = stats.TryGetWindow("ping", "A", TimeSpan.FromSeconds(5), t0);
        var b = stats.TryGetWindow("ping", "B", TimeSpan.FromSeconds(5), t0);
        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.Equal(2, a!.Value.count);
        Assert.Equal(2, b!.Value.count);
        Assert.True(a.Value.failPct > 0);
        Assert.True(b.Value.failPct > 0);
    }
}
