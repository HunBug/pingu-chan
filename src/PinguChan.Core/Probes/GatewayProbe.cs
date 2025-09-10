using System.Net;
using System.Net.NetworkInformation;
using PinguChan.Core.Models;

namespace PinguChan.Core.Probes;

public sealed class GatewayProbe : IProbe
{
    private readonly IPAddress? _gateway;
    public string Id { get; }
    public TimeSpan Interval { get; }

    public GatewayProbe(TimeSpan? interval = null)
    {
        _gateway = GetDefaultGateway();
        Id = _gateway is null ? "gateway:unknown" : $"gateway:{_gateway}";
        Interval = interval ?? TimeSpan.FromSeconds(5);
    }

    public async Task<NetSample> ExecuteAsync(CancellationToken ct)
    {
        var t0 = DateTimeOffset.UtcNow;
        if (_gateway is null)
        {
            return new NetSample(t0, SampleKind.Gateway, "(none)", false, null, null);
        }
        try
        {
            using var p = new Ping();
            var reply = await p.SendPingAsync(_gateway, 1000);
            var ok = reply.Status == IPStatus.Success;
            var ms = ok ? (double?)reply.RoundtripTime : null;
            return new NetSample(t0, SampleKind.Gateway, _gateway.ToString(), ok, ms, null);
        }
        catch
        {
            return new NetSample(t0, SampleKind.Gateway, _gateway.ToString(), false, null, null);
        }
    }

    private static IPAddress? GetDefaultGateway()
    {
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                var gw = ni.GetIPProperties().GatewayAddresses.FirstOrDefault()?.Address;
                if (gw != null && !IPAddress.IsLoopback(gw)) return gw;
            }
        }
        catch { }
        return null;
    }
}
