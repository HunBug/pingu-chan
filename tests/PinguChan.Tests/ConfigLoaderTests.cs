using PinguChan.Core.Config;

namespace PinguChan.Tests;

public class ConfigLoaderTests
{
    [Fact]
    public void LoadsDefaultsWhenMissing()
    {
        var cfg = ConfigLoader.LoadFromFile("nonexistent.yaml");
        Assert.NotNull(cfg);
        Assert.True(cfg.Targets.Ping.Count > 0);
    }

    [Fact]
    public void ParsesYaml()
    {
                var yaml = """
intervals:
    ping: 3s
targets:
    ping: ['9.9.9.9']
sinks:
    csv: foo.csv
""";
        var cfg = ConfigLoader.LoadFromString(yaml);
        Assert.Equal("3s", cfg.Intervals.Ping);
        Assert.Single(cfg.Targets.Ping);
        Assert.Equal("foo.csv", cfg.Sinks.Csv);
    }

    [Fact]
    public void ParsesJson()
    {
    var json = "{\"intervals\": { \"ping\": \"4s\" }, \"targets\": { \"dns\": [\"example.com\"] }, \"sinks\": { \"jsonl\": \"bar.jsonl\" } }";
        var cfg = ConfigLoader.LoadFromString(json);
        Assert.Equal("4s", cfg.Intervals.Ping);
        Assert.Contains("example.com", cfg.Targets.Dns);
        Assert.Equal("bar.jsonl", cfg.Sinks.Jsonl);
    }
}
