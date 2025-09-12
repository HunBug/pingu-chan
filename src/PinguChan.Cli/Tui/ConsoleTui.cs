using System;
using System.IO;
using System.Text;

namespace PinguChan.Cli.Tui;

// A minimal TUI that:
// - Implements ILogger to capture Core logs and render them with a bottom status area
// - Provides Write/WriteLine for direct prints
// - Maintains up to two status lines (line1, line2)
public interface ISimpleLogger
{
    void LogDebug(string message);
    void LogInfo(string message);
    void LogWarn(string message);
    void LogError(string message);
}

public sealed class ConsoleTui : ISimpleLogger, IDisposable
{
    private readonly object _lock = new();
    private readonly string _name;
    private string _status1 = string.Empty;
    private string _status2 = string.Empty;
    private readonly string? _logPath;

    public ConsoleTui(string name = "tui", string? logPath = null)
    {
        _name = name;
        _logPath = logPath;
        if (!string.IsNullOrWhiteSpace(_logPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_logPath!) ?? ".");
        }
    }

    public void SetStatus(string line1, string line2)
    {
        lock (_lock)
        {
            _status1 = line1 ?? string.Empty;
            _status2 = line2 ?? string.Empty;
            RedrawStatus_NoLock();
        }
    }

    public void WriteLine(string text)
    {
        lock (_lock)
        {
            MoveToLogArea_NoLock();
            Console.WriteLine(text);
            WriteToFile_NoLock(text + "\n");
            RedrawStatus_NoLock();
        }
    }

    public void Write(string text)
    {
        lock (_lock)
        {
            MoveToLogArea_NoLock();
            Console.Write(text);
            WriteToFile_NoLock(text);
            RedrawStatus_NoLock();
        }
    }

    // Simple logging API used by LoggerHelper fallback
    public void LogDebug(string message) => WriteLineWithLevel("DBG", message);
    public void LogInfo(string message) => WriteLineWithLevel("INF", message);
    public void LogWarn(string message) => WriteLineWithLevel("WRN", message);
    public void LogError(string message) => WriteLineWithLevel("ERR", message);

    private void WriteLineWithLevel(string lvl, string message)
    {
        lock (_lock)
        {
            MoveToLogArea_NoLock();
            var ts = DateTime.Now.ToString("HH:mm:ss");
            // Plain formatted text for file output
            var plain = $"{ts} [{lvl}] {message}";
            // Console: color the [LVL] token and value tokens in the message
            Console.Write(ts + " ");
            var prev = Console.ForegroundColor;
            Console.ForegroundColor = lvl switch
            {
                "WRN" => ConsoleColor.Yellow,
                "ERR" => ConsoleColor.Red,
                _ => prev
            };
            Console.Write($"[{lvl}]");
            Console.ForegroundColor = prev;
            Console.Write(' ');
            WriteColoredTokens_NoLock(message);
            Console.WriteLine();
            WriteToFile_NoLock(plain + "\n");
            RedrawStatus_NoLock();
        }
    }

    private static int BottomRow => Math.Max(0, Console.WindowTop + Console.WindowHeight - 2);
    private static int LastRow => Math.Max(0, Console.WindowTop + Console.WindowHeight - 1);

    private void MoveToLogArea_NoLock()
    {
        // Scroll up by one line to make room for a new log, then write directly above the status area
        try
        {
            Console.SetCursorPosition(0, LastRow);
            Console.WriteLine(); // this scrolls the window/buffer by one
        }
        catch { }
        var row = Math.Max(0, BottomRow - 1);
        try { Console.SetCursorPosition(0, row); } catch { }
    }

    private void RedrawStatus_NoLock()
    {
        var bottom = BottomRow;
        try
        {
            Console.SetCursorPosition(0, bottom);
            PrintLinePadded_NoLock(_status1);
            Console.SetCursorPosition(0, bottom + 1);
            PrintLinePadded_NoLock(_status2);
        }
        catch { }
    }

    private void PrintLinePadded_NoLock(string text)
    {
        // basic colorization for % tokens
        var available = Console.WindowWidth;
        var plain = text ?? string.Empty;
        // Write text
        WriteColoredTokens_NoLock(plain);
        // Pad remainder
        var pad = Math.Max(0, available - plain.Length);
        if (pad > 0) Console.Write(new string(' ', pad));
    }

    private void WriteColoredTokens_NoLock(string text)
    {
        var parts = (text ?? string.Empty).Split(' ');
        for (int i = 0; i < parts.Length; i++)
        {
            var p = parts[i];
            // Percent tokens
            if (p.EndsWith("%", StringComparison.Ordinal) && double.TryParse(p.TrimEnd('%'), out var val))
            {
                var prev = Console.ForegroundColor;
                Console.ForegroundColor = val switch
                {
                    < 1 => ConsoleColor.Green,
                    < 5 => ConsoleColor.Yellow,
                    _ => ConsoleColor.Red
                };
                Console.Write(p);
                Console.ForegroundColor = prev;
            }
            // Millisecond tokens (e.g., 20ms, avg=17ms, p95=350ms)
            else if (TryParseMs(p, out var msVal, out var prefix, out var suffix))
            {
                // write prefix (e.g., "avg=")
                if (!string.IsNullOrEmpty(prefix)) Console.Write(prefix);
                var prev = Console.ForegroundColor;
                Console.ForegroundColor = msVal switch
                {
                    < 50 => ConsoleColor.Green,
                    < 200 => ConsoleColor.Yellow,
                    _ => ConsoleColor.Red
                };
                Console.Write($"{msVal:F0}ms");
                Console.ForegroundColor = prev;
                if (!string.IsNullOrEmpty(suffix)) Console.Write(suffix);
            }
            // OK/FAIL tokens
            else if (string.Equals(p, "OK", StringComparison.OrdinalIgnoreCase))
            {
                var prev = Console.ForegroundColor; Console.ForegroundColor = ConsoleColor.Green; Console.Write(p); Console.ForegroundColor = prev;
            }
            else if (string.Equals(p, "FAIL", StringComparison.OrdinalIgnoreCase))
            {
                var prev = Console.ForegroundColor; Console.ForegroundColor = ConsoleColor.Red; Console.Write(p); Console.ForegroundColor = prev;
            }
            else Console.Write(p);
            if (i < parts.Length - 1) Console.Write(' ');
        }
    }

    private static bool TryParseMs(string token, out double value, out string prefix, out string suffix)
    {
        value = 0; prefix = string.Empty; suffix = string.Empty;
        if (!token.Contains("ms", StringComparison.Ordinal)) return false;
        var t = token;
        // handle prefixes like avg= or p95=
        var idxEq = t.IndexOf('=');
        if (idxEq >= 0)
        {
            prefix = t.Substring(0, idxEq + 1);
            t = t.Substring(idxEq + 1);
        }
        // strip trailing punctuation (like ")" or ",")
        var trail = new List<char>();
        while (t.Length > 0 && !char.IsDigit(t[^1]) && t[^1] != 's')
        {
            trail.Insert(0, t[^1]);
            t = t[..^1];
        }
        suffix = new string(trail.ToArray());
        if (!t.EndsWith("ms", StringComparison.Ordinal)) return false;
        var num = t[..^2];
        if (double.TryParse(num, out var v)) { value = v; return true; }
        return false;
    }

    public void Dispose() { }

    private void WriteToFile_NoLock(string content)
    {
        if (string.IsNullOrWhiteSpace(_logPath)) return;
        try { File.AppendAllText(_logPath!, content, Encoding.UTF8); } catch { }
    }

    private sealed class _NoopScope : IDisposable
    {
        public static readonly _NoopScope Instance = new();
        public void Dispose() { }
    }
}

// Fan-out logger to multiple ILogger targets
// Composite logger for ISimpleLogger
public sealed class CompositeLogger : ISimpleLogger
{
    private readonly ISimpleLogger[] _targets;
    public CompositeLogger(params ISimpleLogger[] targets) { _targets = targets; }
    public void LogDebug(string message) { foreach (var t in _targets) t.LogDebug(message); }
    public void LogInfo(string message) { foreach (var t in _targets) t.LogInfo(message); }
    public void LogWarn(string message) { foreach (var t in _targets) t.LogWarn(message); }
    public void LogError(string message) { foreach (var t in _targets) t.LogError(message); }
}
