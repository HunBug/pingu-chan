using System.Diagnostics;

namespace PinguChan.Core.Util;

public static class ProcessRunner
{
    public sealed record Result(int ExitCode, string StdOut, string StdErr, TimeSpan Duration);

    // When noGraphicalAuth is true, clears DISPLAY/WAYLAND_DISPLAY/SUDO_ASKPASS in the child
    // environment so tools like sudo/ufw/pkexec cannot spawn a graphical auth agent. Without a
    // display, a non-interactive `sudo -n` fails fast instead of popping (and orphaning) a dialog.
    public static async Task<Result> RunAsync(string fileName, string args, TimeSpan timeout, CancellationToken ct, bool noGraphicalAuth = false)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        if (noGraphicalAuth)
        {
            psi.Environment["DISPLAY"] = string.Empty;
            psi.Environment["WAYLAND_DISPLAY"] = string.Empty;
            psi.Environment.Remove("SUDO_ASKPASS");
        }

        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sw = Stopwatch.StartNew();
        var stdout = new System.Text.StringBuilder();
        var stderr = new System.Text.StringBuilder();

        proc.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };
        _ = ct.Register(() => { try { if (!proc.HasExited) proc.Kill(true); } catch { } });
        try
        {
            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            return new Result(-1, string.Empty, ex.Message, TimeSpan.Zero);
        }

        _ = Task.Run(async () =>
        {
            try
            {
                if (timeout > TimeSpan.Zero)
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(timeout);
                    while (!proc.HasExited && !cts.IsCancellationRequested)
                    {
                        await Task.Delay(50, ct);
                    }
                    if (!proc.HasExited)
                    {
                        try { proc.Kill(true); } catch { }
                    }
                }
                else
                {
                    await proc.WaitForExitAsync(ct);
                }
            }
            catch { }
            finally { tcs.TrySetResult(); }
        });

        await tcs.Task;
        sw.Stop();
        return new Result(proc.HasExited ? proc.ExitCode : -1, stdout.ToString().Trim(), stderr.ToString().Trim(), sw.Elapsed);
    }
}
