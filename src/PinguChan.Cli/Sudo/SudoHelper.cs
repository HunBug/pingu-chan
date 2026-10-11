using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PinguChan.Cli.Sudo;

public static class SudoHelper
{
    public static bool IsUnixLike => RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    // Ensures sudo credentials are cached and usable by non-interactive child processes.
    // Flow:
    //   1. If a credential is already usable (`sudo -n true` succeeds), return true — no prompt.
    //   2. Otherwise prompt in the terminal (`sudo -v`) and BLOCK until the user responds.
    //   3. Re-verify with `sudo -n true` so the returned value reflects what the collectors
    //      will actually get, not merely that the prompt was dismissed.
    // The terminal prompt (never a GUI agent) makes this safe over SSH / on headless servers.
    public static bool TryElevateInteractive()
    {
        if (!IsUnixLike) return false;
        if (HasUsableCredentials()) return true;
        try
        {
            var psi = new ProcessStartInfo("sudo", "-v")
            {
                UseShellExecute = false,
                // Inherit stdin/stdout/stderr so the password prompt is read from the terminal.
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                RedirectStandardInput = false,
                CreateNoWindow = false
            };
            ForceTerminalPrompt(psi);
            using var p = Process.Start(psi);
            if (p is null) return false;
            p.WaitForExit(); // block until the user finishes authenticating (or cancels)
        }
        catch { return false; }

        // Only report success if a non-interactive child can actually use the credential.
        return HasUsableCredentials();
    }

    // Runs `sudo -n true`: succeeds (exit 0) only when a credential is cached AND reachable by a
    // redirected, non-interactive child — the same conditions the privileged collectors run under.
    public static bool HasUsableCredentials()
    {
        if (!IsUnixLike) return false;
        try
        {
            var psi = new ProcessStartInfo("sudo", "-n true")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            ForceTerminalPrompt(psi);
            using var p = Process.Start(psi);
            if (p is null) return false;
            p.WaitForExit();
            return p.ExitCode == 0;
        }
        catch { return false; }
    }

    // Prevents graphical auth agents (polkit, gnome-keyring) from intercepting the prompt so the
    // password is always requested on the terminal — required for SSH/headless use.
    private static void ForceTerminalPrompt(ProcessStartInfo psi)
    {
        psi.Environment["DISPLAY"] = string.Empty;
        psi.Environment["WAYLAND_DISPLAY"] = string.Empty;
        psi.Environment.Remove("SUDO_ASKPASS");
    }
}
