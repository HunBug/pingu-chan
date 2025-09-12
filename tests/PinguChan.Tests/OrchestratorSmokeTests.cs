using PinguChan.Orchestration;
using PinguChan.Core.Models;
using PinguChan.Core.Runtime;

public class OrchestratorSmokeTests
{
    private sealed class FakeProbe(string id) : PinguChan.Core.IProbe
    {
        public string Id { get; } = id;
        public TimeSpan Interval => TimeSpan.FromMilliseconds(10);
        public Task<NetSample> ExecuteAsync(CancellationToken ct)
            => Task.FromResult(new NetSample(DateTimeOffset.UtcNow, SampleKind.Ping, Id.Split(':')[1], true, 1, null));
    }

    [Fact]
    public async Task Orchestrator_Emits_Samples_And_Stops()
    {
        var pools = new TargetPools(jitterPct: 0, backoffBase: 2.0, backoffMaxMultiplier: 2);
        var now = DateTimeOffset.UtcNow;
        pools.Add("ping", "a", 1, TimeSpan.FromMilliseconds(1), now);
        var stats = new StatsService();
        var factories = new Dictionary<string, Func<string, PinguChan.Core.IProbe>>
        {
            ["ping"] = key => new FakeProbe($"ping:{key}")
        };
        var orch = new MonitorOrchestrator(pools, stats, factories, rules: null);
        await orch.StartAsync();

        // Collect a couple of samples from the bus
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        int seen = 0;
        await foreach (var s in SamplesBus.ReadAllAsync(cts.Token))
        {
            if (s.Target == "a") seen++;
            if (seen >= 2) break;
        }
        await orch.StopAsync();
        Assert.True(seen >= 1);
    }
}
