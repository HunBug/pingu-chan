using System;
using System.IO;
using System.Text;

namespace PinguChan.Cli.Tui;

// Simple file logger used when cfg.Sinks.Logs is set
public sealed class FileLogger : ISimpleLogger, IDisposable
{
    private readonly object _lock = new();
    private readonly string _filePath;

    public FileLogger(string filePath)
    {
        _filePath = filePath;
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath) ?? ".");
    }

    public void LogInfo(string message) => Write("INFORMATION", message);
    public void LogWarn(string message) => Write("WARNING", message);
    public void LogError(string message) => Write("ERROR", message);

    private void Write(string level, string message)
    {
        var ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var line = $"{ts} [{level}] {message}\n";
        lock (_lock)
        {
            File.AppendAllText(_filePath, line, Encoding.UTF8);
        }
    }

    public void Dispose() { }

    private sealed class _Noop : IDisposable { public static readonly _Noop Instance = new(); public void Dispose() { } }
}
