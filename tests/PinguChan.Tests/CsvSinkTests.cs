using PinguChan.Core.Models;
using PinguChan.Core.Sinks;

namespace PinguChan.Tests;

public class CsvSinkTests
{
    [Fact]
    public async Task WritesHeaderAndSample()
    {
        var path = Path.Combine(Path.GetTempPath(), $"netwatch_{Guid.NewGuid():N}.csv");
        await using (var sink = new CsvSink(path))
        {
            var sample = new NetSample(DateTimeOffset.UtcNow, SampleKind.Ping, "1.1.1.1", true, 12.3, null);
            await sink.WriteAsync(sample, CancellationToken.None);
        }
        var lines = await File.ReadAllLinesAsync(path);
        Assert.True(lines.Length >= 2, "Expect header and one data line");
        Assert.StartsWith("ts,kind,target,ok,ms,extra", lines[0]);
        Assert.Contains("1.1.1.1", lines[1]);
    }
}
