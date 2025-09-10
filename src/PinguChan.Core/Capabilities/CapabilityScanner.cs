using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PinguChan.Core.Capabilities;

public static class CapabilityScanner
{
    public static async Task<IReadOnlyList<CapabilityReport>> ScanAsync(CancellationToken ct)
    {
        var list = new List<CapabilityReport>();

        var isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var isMac = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        // Privilege detection (best-effort)
    bool elevated = isWindows ? IsWindowsAdmin() : (Environment.UserName == "root" || GetEuid() == 0);

        // Ping raw ICMP capability (best-effort check)
        list.Add(new CapabilityReport(
            Id: "icmp_raw",
            Description: "Ability to send raw ICMP (Ping)",
            ImplementedOnOs: true,
            Available: elevated || isWindows, // Windows Ping works unprivileged typically
            RequiresSudo: isLinux && !elevated,
            ExternalDepsOk: true,
            Hint: isLinux && !elevated ? "Grant CAP_NET_RAW or run with sudo for raw ICMP." : null
        ));

        // External tools presence (Linux/macOS)
        if (isLinux || isMac)
        {
            list.Add(await CheckToolAsync("iw", "Wireless tooling (Linux)", ct));
            list.Add(await CheckToolAsync("nmcli", "NetworkManager CLI (Linux)", ct));
            list.Add(await CheckToolAsync("tracepath", "Path MTU / traceroute-lite", ct));
            list.Add(await CheckToolAsync("traceroute", "Traceroute", ct));
        }

        return list;
    }

    private static async Task<CapabilityReport> CheckToolAsync(string exe, string desc, CancellationToken ct)
    {
        try
        {
            var (ok, hint) = await WhichAsync(exe, ct);
            return new CapabilityReport(exe, desc, ImplementedOnOs: true, Available: ok, RequiresSudo: false, ExternalDepsOk: ok, Hint: ok ? null : InstallHint(exe));
        }
        catch
        {
            return new CapabilityReport(exe, desc, ImplementedOnOs: true, Available: false, RequiresSudo: false, ExternalDepsOk: false, Hint: InstallHint(exe));
        }
    }

    private static async Task<(bool found, string? path)> WhichAsync(string name, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "bash",
            ArgumentList = { "-lc", $"command -v {name}" },
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var p = Process.Start(psi)!;
        var output = await p.StandardOutput.ReadToEndAsync();
        await p.WaitForExitAsync(ct);
        return (p.ExitCode == 0 && !string.IsNullOrWhiteSpace(output), output.Trim());
    }

    private static string? InstallHint(string exe)
    {
        // Best-effort, short hint
        return OperatingSystem.IsLinux()
            ? exe switch
            {
                "iw" => "Debian: apt install iw; Arch: pacman -S iw",
                "nmcli" => "Debian: apt install network-manager; Arch: pacman -S networkmanager",
                "traceroute" => "Debian: apt install traceroute; Arch: pacman -S traceroute",
                "tracepath" => "Debian: apt install iputils-tracepath; Arch: pacman -S iputils",
                _ => null
            }
            : null;
    }

    private static bool IsWindowsAdmin()
    {
        try
        {
            var id = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(id);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    private static int GetEuid()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS()) return -1;
        try { return (int)geteuid(); } catch { return -1; }
    }

    [DllImport("libc", EntryPoint = "geteuid")]
    private static extern uint geteuid();
}
