using System.Text.Json;

namespace PinguChan.Core.Util;

public static class JsonUtil
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize(object obj)
        => JsonSerializer.Serialize(obj, Options);
}
