using System.Threading.Channels;
using PinguChan.Core.Models;

namespace PinguChan.Core.Runtime;

public static class FindingsBus
{
    private static readonly Channel<RuleFinding> _bus = Channel.CreateBounded<RuleFinding>(new BoundedChannelOptions(256)
    {
        FullMode = BoundedChannelFullMode.DropOldest
    });

    public static bool TryPublish(RuleFinding finding)
        => _bus.Writer.TryWrite(finding);

    public static ValueTask PublishAsync(RuleFinding finding, CancellationToken ct = default)
        => _bus.Writer.WriteAsync(finding, ct);

    public static IAsyncEnumerable<RuleFinding> ReadAllAsync(CancellationToken ct = default)
        => _bus.Reader.ReadAllAsync(ct);
}
