using PinguChan.Orchestration;

public class TargetPoolsConcurrencyTests
{
    [Fact]
    public void InFlight_Target_IsNotReissued_UntilReported()
    {
        var now = DateTimeOffset.UtcNow;
        var pools = new TargetPools(jitterPct: 0).Add("ping", "a", 1, TimeSpan.FromMilliseconds(10), now);
        var n1 = pools.TryGetNext("ping", now);
        Assert.NotNull(n1);
        var (key1, _) = n1!.Value;
        Assert.Equal("a", key1);

        // Immediately ask again; should not return the same key as it's in-flight
        var n2 = pools.TryGetNext("ping", now);
        Assert.NotNull(n2);
        var (key2, delay) = n2!.Value;
        Assert.Equal("a", key2);
        Assert.True(delay > TimeSpan.Zero); // only a delay hint, not due now

        // Report completion; now it can be scheduled again when due
        pools.Report("ping", "a", ok: true);
        var n3 = pools.TryGetNext("ping", now + TimeSpan.FromMilliseconds(20));
        Assert.NotNull(n3);
        var (key3, _) = n3!.Value;
        Assert.Equal("a", key3);
    }
}
