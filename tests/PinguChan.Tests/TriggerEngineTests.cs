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
}
