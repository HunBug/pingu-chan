using System.Diagnostics;
using PinguChan.Core.Models;

namespace PinguChan.Core.Probes;

public sealed class DnsProbe : IProbe
{
    private readonly string _domain;
    public string Id { get; }
    public TimeSpan Interval { get; }
    public TimeSpan Timeout { get; }

    public DnsProbe(string domain, TimeSpan? interval = null, TimeSpan? timeout = null)
    {
        _domain = domain;
        Id = $"dns:{domain}";
        Interval = interval ?? TimeSpan.FromSeconds(5);
        Timeout = timeout ?? TimeSpan.FromSeconds(2);
    }

    public async Task<NetSample> ExecuteAsync(CancellationToken ct)
    {
        var t0 = DateTimeOffset.UtcNow;
        var sw = Stopwatch.StartNew();
        try
        {
            var task = System.Net.Dns.GetHostAddressesAsync(_domain);
            var done = await Task.WhenAny(task, Task.Delay(Timeout, ct));
            if (done != task)
            {
                return new NetSample(t0, SampleKind.Dns, _domain, false, null, null);
            }
            var addrs = await task;
            sw.Stop();
            var extra = addrs.Length > 0 ? string.Join(' ', addrs.Select(a => a.ToString()).Take(3)) : string.Empty;
            return new NetSample(t0, SampleKind.Dns, _domain, addrs.Length > 0, sw.Elapsed.TotalMilliseconds, extra);
        }
        catch
        {
            return new NetSample(t0, SampleKind.Dns, _domain, false, null, null);
        }
    }
}
