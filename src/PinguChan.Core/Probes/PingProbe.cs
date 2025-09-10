using System.Net.NetworkInformation;
using PinguChan.Core.Models;

namespace PinguChan.Core.Probes;

public sealed class PingProbe : IProbe
{
    private readonly string _target;
    public string Id { get; }
    public TimeSpan Interval { get; }

    public PingProbe(string target, TimeSpan? interval = null)
    {
        _target = target;
        Id = $"ping:{target}";
        Interval = interval ?? TimeSpan.FromSeconds(2);
    }

    public async Task<NetSample> ExecuteAsync(CancellationToken ct)
    {
        var t0 = DateTimeOffset.UtcNow;
        try
        {
            using var p = new Ping();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(1));
            var reply = await p.SendPingAsync(_target, 1000);
            var ok = reply.Status == IPStatus.Success;
            var ms = ok ? (double?)reply.RoundtripTime : null;
            return new NetSample(t0, SampleKind.Ping, _target, ok, ms, null);
        }
        catch
        {
            return new NetSample(t0, SampleKind.Ping, _target, false, null, null);
        }
    }
}
