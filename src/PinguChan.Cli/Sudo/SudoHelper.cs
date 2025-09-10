using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PinguChan.Cli.Sudo;

public static class SudoHelper
{
    public static bool IsUnixLike => RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    // Attempts to cache sudo credentials by invoking `sudo -v` interactively in the current TTY.
    // Returns true if elevation succeeded (exit code 0), false otherwise.
    public static bool TryElevateInteractive()
    {
        if (!IsUnixLike) return false;
        try
        {
            var psi = new ProcessStartInfo("sudo", "-v")
            {
                UseShellExecute = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                RedirectStandardInput = false,
                CreateNoWindow = false
            };
            using var p = Process.Start(psi);
            if (p is null) return false;
            p.WaitForExit();
            return p.ExitCode == 0;
        }
        catch { return false; }
    }
}
