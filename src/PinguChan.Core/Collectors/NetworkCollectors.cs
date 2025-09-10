using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using PinguChan.Core.Models;
using PinguChan.Core.Util;

namespace PinguChan.Core.Collectors;

public sealed class NetIfCollector(TimeSpan interval) : ICollector
{
    public string Id => "net.if";
    public TimeSpan Interval { get; } = interval;

    public Task<IReadOnlyCollection<NetSample>> CollectAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var list = new List<NetSample>();
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            var ipProps = ni.GetIPProperties();
            var addrs = ipProps.UnicastAddresses
                .Where(a => a.Address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
                .Select(a => a.Address.ToString())
                .ToArray();
            var gw = ipProps.GatewayAddresses.FirstOrDefault()?.Address?.ToString();
            var dns = ipProps.DnsAddresses.Select(d => d.ToString()).ToArray();
            var info = new
            {
                name = ni.Name,
                type = ni.NetworkInterfaceType.ToString(),
                op = ni.OperationalStatus.ToString(),
                speed = ni.Speed,
                mac = ni.GetPhysicalAddress()?.ToString(),
                ips = addrs,
                gateway = gw,
                dns
            };
            list.Add(new NetSample(now, SampleKind.Collector, ni.Name, true, null, JsonUtil.Serialize(info)));
        }
        return Task.FromResult<IReadOnlyCollection<NetSample>>(list);
    }
}

public sealed class RouteCollector(TimeSpan interval) : ICollector
{
    public string Id => "net.route";
    public TimeSpan Interval { get; } = interval;

    public async Task<IReadOnlyCollection<NetSample>> CollectAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var list = new List<NetSample>();
        try
        {
            string os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" :
                        RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macos" : "linux";
            var cmd = os switch
            {
                "windows" => ("route", "print -4"),
                "macos" => ("netstat", "-rn"),
                _ => ("ip", "route show")
            };
            var res = await ProcessRunner.RunAsync(cmd.Item1, cmd.Item2, TimeSpan.FromSeconds(3), ct);
            var ok = res.ExitCode == 0 && string.IsNullOrWhiteSpace(res.StdErr);
            var extra = JsonUtil.Serialize(new { os, output = Truncate(res.StdOut, 4000) });
            list.Add(new NetSample(now, SampleKind.Collector, "routes", ok, null, extra));
        }
        catch (Exception ex)
        {
            list.Add(new NetSample(now, SampleKind.Collector, "routes", false, null, JsonUtil.Serialize(new { error = ex.Message })));
        }
        return list;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}

public sealed class FirewallCollector(TimeSpan interval) : ICollector
{
    public string Id => "net.firewall";
    public TimeSpan Interval { get; } = interval;

    public async Task<IReadOnlyCollection<NetSample>> CollectAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var list = new List<NetSample>();
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var res = await ProcessRunner.RunAsync("netsh", "advfirewall show allprofiles", TimeSpan.FromSeconds(3), ct);
                list.Add(new NetSample(now, SampleKind.Collector, "firewall", res.ExitCode == 0, null, JsonUtil.Serialize(new { output = Truncate(res.StdOut, 4000) })));
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var res = await ProcessRunner.RunAsync("/usr/libexec/ApplicationFirewall/socketfilterfw", "--getglobalstate", TimeSpan.FromSeconds(3), ct);
                list.Add(new NetSample(now, SampleKind.Collector, "firewall", res.ExitCode == 0, null, JsonUtil.Serialize(new { output = Truncate(res.StdOut, 4000) })));
            }
            else
            {
                // Try ufw, then firewalld-cmd
                var resUfw = await ProcessRunner.RunAsync("ufw", "status", TimeSpan.FromSeconds(3), ct);
                if (resUfw.ExitCode == 0)
                    list.Add(new NetSample(now, SampleKind.Collector, "firewall.ufw", true, null, JsonUtil.Serialize(new { output = Truncate(resUfw.StdOut, 4000) })));
                else
                {
                    var resFw = await ProcessRunner.RunAsync("firewall-cmd", "--state", TimeSpan.FromSeconds(3), ct);
                    var ok = resFw.ExitCode == 0;
                    list.Add(new NetSample(now, SampleKind.Collector, "firewall.firewalld", ok, null, JsonUtil.Serialize(new { output = Truncate(resFw.StdOut, 4000) })));
                }
            }
        }
        catch (Exception ex)
        {
            list.Add(new NetSample(now, SampleKind.Collector, "firewall", false, null, JsonUtil.Serialize(new { error = ex.Message })));
        }
        return list;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}

public sealed class TracerouteCollector(TimeSpan interval, string target = "8.8.8.8") : ICollector
{
    public string Id => "net.traceroute";
    public TimeSpan Interval { get; } = interval;
    private readonly string _target = target;

    public async Task<IReadOnlyCollection<NetSample>> CollectAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var list = new List<NetSample>();
        try
        {
            string os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" :
                        RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macos" : "linux";
            var cmd = os switch
            {
                "windows" => ("tracert", $"-d -h 20 -w 800 {_target}"),
                "macos" => ("traceroute", $"-n -m 20 -w 1 {_target}"),
                _ => ("traceroute", $"-n -m 20 -w 1 {_target}")
            };
            var res = await ProcessRunner.RunAsync(cmd.Item1, cmd.Item2, TimeSpan.FromSeconds(20), ct);
            var ok = res.ExitCode == 0 || (!string.IsNullOrWhiteSpace(res.StdOut) && res.StdOut.Contains(_target));
            var extra = JsonUtil.Serialize(new { target = _target, hops = Truncate(res.StdOut, 4000) });
            list.Add(new NetSample(now, SampleKind.Collector, _target, ok, null, extra));
        }
        catch (Exception ex)
        {
            list.Add(new NetSample(now, SampleKind.Collector, _target, false, null, JsonUtil.Serialize(new { error = ex.Message })));
        }
        return list;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}

public sealed class InternetConnectivityCollector(TimeSpan interval) : ICollector
{
    public string Id => "net.internet";
    public TimeSpan Interval { get; } = interval;

    public Task<IReadOnlyCollection<NetSample>> CollectAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var list = new List<NetSample>();
        try
        {
            var hasDefaultGw = NetworkInterface.GetAllNetworkInterfaces()
                .SelectMany(n => n.GetIPProperties().GatewayAddresses)
                .Any(g => g?.Address != null && !g.Address.Equals(System.Net.IPAddress.Any));

            var hasDns = NetworkInterface.GetAllNetworkInterfaces()
                .SelectMany(n => n.GetIPProperties().DnsAddresses)
                .Any();

            var info = new { defaultGateway = hasDefaultGw, dnsConfigured = hasDns };
            var ok = hasDefaultGw && hasDns;
            list.Add(new NetSample(now, SampleKind.Collector, "internet-heuristics", ok, null, JsonUtil.Serialize(info)));
        }
        catch (Exception ex)
        {
            list.Add(new NetSample(now, SampleKind.Collector, "internet-heuristics", false, null, JsonUtil.Serialize(new { error = ex.Message })));
        }
        return Task.FromResult<IReadOnlyCollection<NetSample>>(list);
    }
}
