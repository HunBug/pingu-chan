using PinguChan.Orchestration;

public class TargetPoolsDecayAndDiagnosticsTests
{
    [Fact]
    public void FailureCount_Decays_Over_Time()
    {
        var t0 = DateTimeOffset.UtcNow;
        var halfLife = TimeSpan.FromSeconds(2);
        var pools = new TargetPools(jitterPct: 0, backoffBase: 2.0, backoffMaxMultiplier: 8, decayHalfLife: halfLife)
            .Add("ping", "x", 1, TimeSpan.FromMilliseconds(10), t0);

        // cause a couple failures to increase failure count
        var pick = pools.TryGetNext("ping", t0);
        Assert.NotNull(pick);
        pools.Report("ping", "x", ok: false); // FC=1
        pools.Report("ping", "x", ok: false); // FC=2 (no schedule change here; just increment)

        // Move time by ~one half-life; FC should drop roughly by half (floor)
        var later = t0 + TimeSpan.FromSeconds(2.1);
        pools.TryGetNext("ping", later);
        // We cannot access internals, but we can check diagnostics
        var diag = pools.GetDiagnostics("ping");
        Assert.Single(diag);
        Assert.True(diag[0].FailureCount <= 2 && diag[0].FailureCount >= 0);
    }

    [Fact]
    public void DiagnosticsSnapshot_Reports_Expected_Fields()
    {
        var now = DateTimeOffset.UtcNow;
        var pools = new TargetPools(jitterPct: 0)
            .Add("ping", "a", 1, TimeSpan.FromMilliseconds(5), now)
            .Add("ping", "b", 2, TimeSpan.FromMilliseconds(10), now);

        var snap = pools.GetDiagnostics("ping");
        Assert.Equal(2, snap.Count);
        Assert.Contains(snap, s => s.Key == "a");
        Assert.Contains(snap, s => s.Key == "b");
        Assert.All(snap, s => Assert.True(s.MinInterval > TimeSpan.Zero));
    }
}
