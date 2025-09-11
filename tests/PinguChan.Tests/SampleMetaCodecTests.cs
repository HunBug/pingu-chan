using PinguChan.Core.Models;

namespace PinguChan.Tests;

public class SampleMetaCodecTests
{
    [Fact]
    public void Serialize_Parse_RoundTrip_Basic()
    {
        var src = new SampleMeta(Pool: "ping", Key: "1.1.1.1");
        var json = SampleMetaCodec.Serialize(src);

        Assert.Contains("\"pool\":\"ping\"", json);
        Assert.Contains("\"key\":\"1.1.1.1\"", json);

        var parsed = SampleMetaCodec.TryParse(json);
        Assert.NotNull(parsed);
        Assert.Equal(src.Pool, parsed!.Pool);
        Assert.Equal(src.Key, parsed.Key);
        Assert.Null(parsed.Tags);
    }

    [Fact]
    public void Parse_Gracefully_Handles_Null_And_BadJson()
    {
        Assert.Null(SampleMetaCodec.TryParse(null));
        Assert.Null(SampleMetaCodec.TryParse("   "));
        Assert.Null(SampleMetaCodec.TryParse("not-json"));
    }
}
