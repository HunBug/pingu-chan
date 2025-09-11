using System.Text.Json;
using PinguChan.Core.Models;
using PinguChan.Core.Sinks;

namespace PinguChan.Tests;

public class FindingsSinkTests
{
    [Fact]
    public async Task CsvSink_WritesFinding_AsSpecialRow()
    {
        var path = Path.Combine(Path.GetTempPath(), $"find_{Guid.NewGuid():N}.csv");
        var finding = new RuleFinding(DateTimeOffset.UtcNow, "test_rule", Severity.Warning, "hello", new Dictionary<string, object>{{"k","v"}});
        await using (var sink = new CsvSink(path))
        {
            await sink.WriteAsync(finding, CancellationToken.None);
        }
        var lines = await File.ReadAllLinesAsync(path);
        Assert.True(lines.Length >= 2);
        Assert.Contains("finding:test_rule", lines[1]);
    }

    [Fact]
    public async Task JsonlSink_WritesFinding_AsJson()
    {
        var path = Path.Combine(Path.GetTempPath(), $"find_{Guid.NewGuid():N}.jsonl");
        var finding = new RuleFinding(DateTimeOffset.UtcNow, "test_rule", Severity.Warning, "hello", new Dictionary<string, object>{{"k","v"}});
        await using (var sink = new JsonlSink(path))
        {
            await sink.WriteAsync(finding, CancellationToken.None);
        }
        var text = await File.ReadAllTextAsync(path);
        var parsed = JsonSerializer.Deserialize<RuleFinding>(text.Trim());
        Assert.NotNull(parsed);
        Assert.Equal("test_rule", parsed!.RuleId);
        Assert.Equal("hello", parsed.Message);
    }
}
