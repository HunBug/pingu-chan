using PinguChan.Core.Models;
using PinguChan.Core.Util;

namespace PinguChan.Orchestration.Actions;

public static class TriggerActions
{
    private static NetSample Make(string key, bool ok, object payload)
        => new NetSample(DateTimeOffset.UtcNow, SampleKind.Collector, key, ok, null, JsonUtil.Serialize(payload));

    public static Func<CancellationToken, Task<NetSample[]>> MtuSweep(string target = "8.8.8.8")
        => async ct =>
        {
            // Stub: pretend we probed MTU path
            try { await Task.Delay(80, ct); } catch { }
            var res = new { op = "mtu_sweep", target, mtu = 1500, method = "stub" };
            return new[] { Make("mtu_sweep", true, res) };
        };

    public static Func<CancellationToken, Task<NetSample[]>> SnapshotArpDhcp()
        => async ct =>
        {
            try { await Task.Delay(50, ct); } catch { }
            var isLinux = OperatingSystem.IsLinux();
            var payload = new { op = "snapshot", kind = "arp_dhcp", platform = isLinux ? "linux" : (OperatingSystem.IsWindows() ? "windows" : "other") };
            return new[] { Make("snapshot.arp_dhcp", true, payload) };
        };

    public static Func<CancellationToken, Task<NetSample[]>> NextHopRefresh()
        => async ct =>
        {
            try { await Task.Delay(60, ct); } catch { }
            var payload = new { op = "next_hop_refresh", refreshed = true };
            return new[] { Make("next_hop_refresh", true, payload) };
        };

    public static Func<CancellationToken, Task<NetSample[]>> WifiLink()
        => async ct =>
        {
            try { await Task.Delay(70, ct); } catch { }
            var supported = OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();
            if (!supported)
            {
                // Report as OK with a note; engine will treat as normal sample
                return new[] { Make("wifi_link", true, new { op = "wifi_link", supported = false }) };
            }
            var payload = new { op = "wifi_link", supported = true, quality = "unknown" };
            return new[] { Make("wifi_link", true, payload) };
        };
}
