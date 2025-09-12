using PinguChan.Core.Models;
using PinguChan.Core.Runtime;

public class BackPressureTests
{
    [Fact]
    public async Task SamplesBus_DropsOldest_WhenFlooded()
    {
        // Flood the bus with more than capacity using non-blocking TryPublish
        for (int i = 0; i < 2000; i++)
        {
            Assert.True(SamplesBus.TryPublish(new NetSample(DateTimeOffset.UtcNow, SampleKind.Ping, "bp", true, 1, null)));
        }
        // Now drain for a short window and ensure we get at most capacity items
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        int count = 0;
        try
        {
            await foreach (var _ in SamplesBus.ReadAllAsync(cts.Token))
            {
                count++;
            }
        }
        catch (OperationCanceledException) { }
        Assert.InRange(count, 1, 1024);
    }

    [Fact]
    public async Task FindingsBus_DropsOldest_WhenFlooded()
    {
        for (int i = 0; i < 1000; i++)
        {
            Assert.True(FindingsBus.TryPublish(new RuleFinding(DateTimeOffset.UtcNow, "r", Severity.Info, "msg")));
        }
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        int count = 0;
        try
        {
            await foreach (var _ in FindingsBus.ReadAllAsync(cts.Token))
            {
                count++;
            }
        }
        catch (OperationCanceledException) { }
        Assert.InRange(count, 1, 256);
    }
}
