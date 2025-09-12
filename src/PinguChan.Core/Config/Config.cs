using System.Text.Json;
using System.Text.Json.Serialization;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace PinguChan.Core.Config;

public sealed class PinguConfig
{
    public IntervalsConfig Intervals { get; set; } = new();
    public TargetsConfig Targets { get; set; } = new();
    public SinksConfig Sinks { get; set; } = new();
    public OrchestrationConfig Orchestration { get; set; } = new();
    public RulesConfig Rules { get; set; } = new();
}

public sealed class IntervalsConfig
{
    public string Ping { get; set; } = "2s";
    public string Dns { get; set; } = "5s";
    public string Http { get; set; } = "10s";
}

public sealed class TargetsConfig
{
    public List<string> Ping { get; set; } = new() { "1.1.1.1", "8.8.8.8" };
    public List<string> Dns { get; set; } = new() { "github.com" };
    public List<string> Http { get; set; } = new() { "https://www.google.com/generate_204" };
}

public sealed class SinksConfig
{
    public string? Csv { get; set; } = "netwatch.csv";
    public string? Jsonl { get; set; } = "netwatch.jsonl";
    public string? Logs { get; set; }
    // If true (default), append a timestamp to sink filenames so each run writes a new file.
    // When false, files are appended like today.
    public bool AppendTimestamp { get; set; } = true;
}

public sealed class OrchestrationConfig
{
    public SchedulerConfig Scheduler { get; set; } = new();
}

public sealed class SchedulerConfig
{
    public double JitterPct { get; set; } = 0.2;
    public double BackoffBase { get; set; } = 2.0;
    public int BackoffMaxMultiplier { get; set; } = 8;
    public string DecayHalfLife { get; set; } = "00:05:00"; // TimeSpan format or shorthand accepted by DurationParser
}

public sealed class RulesConfig
{
    // Minimal rules config to start; more to be added later
    public int ConsecutiveFailThreshold { get; set; } = 3;
    public QuorumRuleConfig Quorum { get; set; } = new();
}

public sealed class QuorumRuleConfig
{
    public string Window { get; set; } = "00:01:00"; // 60s default
    public double FailThreshold { get; set; } = 0.5; // 50%
    public int MinSamples { get; set; } = 5; // minimum events to consider
}

public static class ConfigLoader
{
    private static readonly IDeserializer Yaml = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static PinguConfig LoadFromString(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new PinguConfig();
        // Try YAML first
        try
        {
            var cfg = Yaml.Deserialize<PinguConfig>(text);
            if (cfg is not null) return cfg;
        }
        catch { }
        // Fallback to JSON
        try
        {
            var cfg = JsonSerializer.Deserialize<PinguConfig>(text, JsonOpts);
            return cfg ?? new PinguConfig();
        }
        catch { return new PinguConfig(); }
    }

    public static PinguConfig LoadFromFile(string? path)
    {
        var file = path;
        if (string.IsNullOrWhiteSpace(file))
        {
            if (File.Exists("pingu.yaml")) file = "pingu.yaml";
            else if (File.Exists("pingu.yml")) file = "pingu.yml";
            else if (File.Exists("pingu.json")) file = "pingu.json";
        }

        if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
            return new PinguConfig();

        var text = File.ReadAllText(file);
        return LoadFromString(text);
    }
}

public static class DurationParser
{
    // Supports forms like "2s", "500ms", "1m", "00:00:10"
    public static TimeSpan Parse(string input, TimeSpan @default)
    {
        if (string.IsNullOrWhiteSpace(input)) return @default;
        if (TimeSpan.TryParse(input, out var ts)) return ts;
        try
        {
            var s = input.Trim().ToLowerInvariant();
            if (s.EndsWith("ms"))
            {
                if (double.TryParse(s[..^2], out var ms)) return TimeSpan.FromMilliseconds(ms);
            }
            else if (s.EndsWith("s"))
            {
                if (double.TryParse(s[..^1], out var sec)) return TimeSpan.FromSeconds(sec);
            }
            else if (s.EndsWith("m"))
            {
                if (double.TryParse(s[..^1], out var min)) return TimeSpan.FromMinutes(min);
            }
            else if (s.EndsWith("h"))
            {
                if (double.TryParse(s[..^1], out var hr)) return TimeSpan.FromHours(hr);
            }
        }
        catch { }
        return @default;
    }
}
