using System.Diagnostics;
using PinguChan.Core.Models;

namespace PinguChan.Core.Probes;

public sealed class HttpProbe : IProbe
{
    private static readonly HttpClient Shared = new(new SocketsHttpHandler
    {
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
        MaxConnectionsPerServer = 8,
        AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
    })
    {
        Timeout = System.Threading.Timeout.InfiniteTimeSpan
    };

    private readonly string _url;
    public string Id { get; }
    public TimeSpan Interval { get; }
    public TimeSpan Timeout { get; }

    public HttpProbe(string url, TimeSpan? interval = null, TimeSpan? timeout = null)
    {
        _url = url;
        Id = $"http:{url}";
        Interval = interval ?? TimeSpan.FromSeconds(10);
        Timeout = timeout ?? TimeSpan.FromSeconds(3);
    }

    public async Task<NetSample> ExecuteAsync(CancellationToken ct)
    {
        var t0 = DateTimeOffset.UtcNow;
        var sw = Stopwatch.StartNew();
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(Timeout);
            using var req = new HttpRequestMessage(HttpMethod.Get, _url);
            using var resp = await Shared.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            sw.Stop();
            var ok = (int)resp.StatusCode < 500; // consider <500 ok for TTFB
            var extra = $"{(int)resp.StatusCode}";
            return new NetSample(t0, SampleKind.Http, _url, ok, sw.Elapsed.TotalMilliseconds, extra);
        }
        catch
        {
            return new NetSample(t0, SampleKind.Http, _url, false, null, null);
        }
    }
}
