using System.Text.Json;
using System.Text.Json.Serialization;

namespace PinguChan.Core.Models;

public sealed record SampleMeta(
    string? Pool = null,
    string? Key = null,
    IReadOnlyDictionary<string, object>? Tags = null
);

public static class SampleMetaCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string Serialize(SampleMeta meta) => JsonSerializer.Serialize(meta, Options);

    public static SampleMeta? TryParse(string? extra)
    {
        if (string.IsNullOrWhiteSpace(extra)) return null;
        try { return JsonSerializer.Deserialize<SampleMeta>(extra, Options); }
        catch { return null; }
    }
}
