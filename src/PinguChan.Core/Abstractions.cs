using PinguChan.Core.Models;

namespace PinguChan.Core;

public interface IProbe
{
    string Id { get; }
    TimeSpan Interval { get; }
    Task<NetSample> ExecuteAsync(CancellationToken ct);
}

public interface ICollector
{
    string Id { get; }
    TimeSpan Interval { get; }
    Task<IReadOnlyCollection<NetSample>> CollectAsync(CancellationToken ct);
}

public interface IRule
{
    string Id { get; }
    Severity DefaultSeverity { get; }
    RuleFinding? Evaluate(IReadOnlyList<NetSample> window, DateTimeOffset now);
}

public interface IResultSink
{
    Task WriteAsync(NetSample sample, CancellationToken ct);
    Task WriteAsync(RuleFinding finding, CancellationToken ct);
}

public interface INotifier
{
    Task NotifyAsync(RuleFinding finding, CancellationToken ct);
}
