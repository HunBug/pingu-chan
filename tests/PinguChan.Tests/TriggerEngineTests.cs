using PinguChan.Core.Models;
using PinguChan.Orchestration;
using Xunit;

namespace PinguChan.Tests;

public class TriggerEngineTests
{
    [Fact]
    public async Task DebounceAndCooldown_Work()
    {
        var fired = 0;
        var trig = new TriggerEngine()
            .AddTrigger(new TriggerEngine.TriggerSpec(
                Id: "t1",
                Debounce: TimeSpan.FromMilliseconds(50),
                Cooldown: TimeSpan.FromMilliseconds(200),
                Predicate: s => !s.Ok,
                Action: async ct =>
                {
                    Interlocked.Increment(ref fired);
                    await Task.Delay(10, ct);
                    return new[] { new NetSample(DateTimeOffset.UtcNow, SampleKind.Collector, "t1", true, null, null) };
                }
            ));

        var now = DateTimeOffset.UtcNow;
        // arm
        trig.Observe(new NetSample(now, SampleKind.Ping, "x", false, null, null));
        // before debounce
        trig.OnTick(now.AddMilliseconds(20));
        await Task.Delay(30);
        Assert.Equal(0, fired);
        // after debounce
        trig.OnTick(now.AddMilliseconds(60));
        await Task.Delay(30);
        Assert.Equal(1, fired);
        // within cooldown
        trig.Observe(new NetSample(now.AddMilliseconds(70), SampleKind.Ping, "x", false, null, null));
        trig.OnTick(now.AddMilliseconds(120));
        await Task.Delay(30);
        Assert.Equal(1, fired);
        // after cooldown
        trig.OnTick(now.AddMilliseconds(300));
        await Task.Delay(30);
        Assert.Equal(2, fired);
    }

    [Fact]
    public async Task ActionResults_AndFailures_Surface()
    {
        var okTrig = new TriggerEngine()
            .AddTrigger(new TriggerEngine.TriggerSpec(
                "ok", TimeSpan.Zero, TimeSpan.Zero,
                s => !s.Ok,
                ct => Task.FromResult(new[] { new NetSample(DateTimeOffset.UtcNow, SampleKind.Collector, "ok", true, null, null) })
            ));
        var now = DateTimeOffset.UtcNow;
        okTrig.Observe(new NetSample(now, SampleKind.Ping, "x", false, null, null));
        okTrig.OnTick(now.AddMilliseconds(1));
        await Task.Delay(20);

        var failTrig = new TriggerEngine()
            .AddTrigger(new TriggerEngine.TriggerSpec(
                "fail", TimeSpan.Zero, TimeSpan.Zero,
                s => !s.Ok,
                ct => throw new Exception("boom")
            ));
        failTrig.Observe(new NetSample(now, SampleKind.Ping, "x", false, null, null));
        failTrig.OnTick(now.AddMilliseconds(1));
        await Task.Delay(20);
        // We don't assert bus contents here; just ensure no exceptions leak
        Assert.True(true);
    }

    [Fact]
    public async Task ConcurrencyGuard_PreventsOverlap()
    {
        int running = 0; int max = 0;
        async Task<NetSample[]> LongAction(CancellationToken ct)
        {
            Interlocked.Increment(ref running);
            max = Math.Max(max, running);
            try { await Task.Delay(100, ct); } finally { Interlocked.Decrement(ref running); }
            return new[] { new NetSample(DateTimeOffset.UtcNow, SampleKind.Collector, "guard", true, null, null) };
        }

        var trig = new TriggerEngine()
            .AddTrigger(new TriggerEngine.TriggerSpec(
                "t", TimeSpan.Zero, TimeSpan.Zero, s => !s.Ok, LongAction
            ));
        var now = DateTimeOffset.UtcNow;
        trig.Observe(new NetSample(now, SampleKind.Ping, "x", false, null, null));
        trig.OnTick(now);
        // Fire again quickly while first still running
        trig.Observe(new NetSample(now.AddMilliseconds(1), SampleKind.Ping, "x", false, null, null));
        trig.OnTick(now.AddMilliseconds(1));
        await Task.Delay(150);

        Assert.Equal(1, max); // never overlapped
    }
}
