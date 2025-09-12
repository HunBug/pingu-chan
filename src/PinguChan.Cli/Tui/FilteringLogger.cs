namespace PinguChan.Cli.Tui;

public enum LogLevel
{
    Debug = 0,
    Info = 1,
    Warn = 2,
    Error = 3
}

public static class LogLevelParser
{
    public static LogLevel Parse(string? s, LogLevel @default = LogLevel.Info)
    {
        if (string.IsNullOrWhiteSpace(s)) return @default;
        switch (s.Trim().ToLowerInvariant())
        {
            case "debug": return LogLevel.Debug;
            case "info": return LogLevel.Info;
            case "warn":
            case "warning": return LogLevel.Warn;
            case "error":
            case "err": return LogLevel.Error;
            default: return @default;
        }
    }
}

public sealed class FilteringLogger : ISimpleLogger
{
    private readonly ISimpleLogger _inner;
    private readonly LogLevel _threshold;
    public FilteringLogger(ISimpleLogger inner, LogLevel threshold)
    {
        _inner = inner; _threshold = threshold;
    }

    public void LogDebug(string message)
    {
        if (_threshold <= LogLevel.Debug) _inner.LogDebug(message);
    }
    public void LogInfo(string message)
    {
        if (_threshold <= LogLevel.Info) _inner.LogInfo(message);
    }
    public void LogWarn(string message)
    {
        if (_threshold <= LogLevel.Warn) _inner.LogWarn(message);
    }
    public void LogError(string message)
    {
        if (_threshold <= LogLevel.Error) _inner.LogError(message);
    }
}
