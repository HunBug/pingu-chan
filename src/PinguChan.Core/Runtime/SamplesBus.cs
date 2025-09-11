using System.Threading.Channels;
using PinguChan.Core.Models;

namespace PinguChan.Core.Runtime;

public static class SamplesBus
{
    private static readonly Channel<NetSample> _bus = Channel.CreateBounded<NetSample>(new BoundedChannelOptions(1024)
    {
        FullMode = BoundedChannelFullMode.DropOldest
    });

    public static bool TryPublish(NetSample sample) => _bus.Writer.TryWrite(sample);
    public static ValueTask PublishAsync(NetSample sample, CancellationToken ct = default)
        => _bus.Writer.WriteAsync(sample, ct);
    public static IAsyncEnumerable<NetSample> ReadAllAsync(CancellationToken ct = default)
        => _bus.Reader.ReadAllAsync(ct);
}
