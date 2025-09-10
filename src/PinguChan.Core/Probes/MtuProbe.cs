using System.Net.NetworkInformation;
using PinguChan.Core.Models;

namespace PinguChan.Core.Probes;

public sealed class MtuProbe : IProbe
{
    private readonly string _target;
    private readonly int _start;
    private readonly int _end;
    public string Id { get; }
    public TimeSpan Interval { get; }

    // Attempts to find approximate path MTU by DF pings with payload sweep.
    // Note: Some platforms ignore DF via PingOptions; treat as best-effort.
    public MtuProbe(string target = "8.8.8.8", TimeSpan? interval = null, int start = 1472, int end = 1200)
    {
        _target = target;
        _start = Math.Max(end, start);
        _end = end;
        Id = $"mtu:{target}";
        Interval = interval ?? TimeSpan.FromMinutes(30);
    }

    public async Task<NetSample> ExecuteAsync(CancellationToken ct)
    {
        var t0 = DateTimeOffset.UtcNow;
        try
        {
            using var ping = new Ping();
            int okSize = _end;
            for (int size = _start; size >= _end; size -= 16)
            {
                ct.ThrowIfCancellationRequested();
                var buffer = new byte[size];
                var opts = new PingOptions { DontFragment = true, Ttl = 64 };
                try
                {
                    var reply = await ping.SendPingAsync(_target, 1000, buffer, opts);
                    if (reply.Status == IPStatus.Success) { okSize = size; break; }
                }
                catch { }
            }
            var extra = $"{okSize}"; // store MTU payload size (approx). Full MTU ~ okSize + 28 bytes IP+ICMP
            return new NetSample(t0, SampleKind.Mtu, _target, true, null, extra);
        }
        catch
        {
            return new NetSample(t0, SampleKind.Mtu, _target, false, null, null);
        }
    }
}
