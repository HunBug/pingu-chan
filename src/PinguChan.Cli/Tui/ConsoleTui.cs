using System;
using Microsoft.Extensions.Logging;

namespace PinguChan.Cli.Tui;

// A minimal TUI that:
// - Implements ILogger to capture Core logs and render them with a bottom status area
// - Provides Write/WriteLine for direct prints
// - Maintains up to two status lines (line1, line2)
public sealed class ConsoleTui : ILogger, IDisposable
{
    private readonly object _lock = new();
    private readonly string _name;
    private string _status1 = string.Empty;
    private string _status2 = string.Empty;

    public ConsoleTui(string name = "tui") { _name = name; }

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
            RedrawStatus_NoLock();
        }
    }

    public void Write(string text)
    {
        lock (_lock)
        {
            MoveToLogArea_NoLock();
            Console.Write(text);
            RedrawStatus_NoLock();
        }
    }

    // ILogger implementation
    IDisposable ILogger.BeginScope<TState>(TState state) => _NoopScope.Instance;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var line = formatter(state, exception);
        if (exception != null) line += $"\n{exception}";
        lock (_lock)
        {
            MoveToLogArea_NoLock();
            var ts = DateTime.Now.ToString("HH:mm:ss");
            var lvl = logLevel.ToString().ToUpperInvariant()[..Math.Min(3, logLevel.ToString().Length)];
            Console.WriteLine($"{ts} [{lvl}] {line}");
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
        WriteColored_NoLock(plain);
        // Pad remainder
        var pad = Math.Max(0, available - plain.Length);
        if (pad > 0) Console.Write(new string(' ', pad));
    }

    private void WriteColored_NoLock(string text)
    {
        var parts = (text ?? string.Empty).Split(' ');
        for (int i = 0; i < parts.Length; i++)
        {
            var p = parts[i];
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
            else Console.Write(p);
            if (i < parts.Length - 1) Console.Write(' ');
        }
    }

    public void Dispose() { }

    private sealed class _NoopScope : IDisposable
    {
        public static readonly _NoopScope Instance = new();
        public void Dispose() { }
    }
}
