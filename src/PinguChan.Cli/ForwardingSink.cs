using PinguChan.Core;
using PinguChan.Core.Models;

namespace PinguChan.Cli;

public sealed class ForwardingSink : IResultSink
{
    private readonly Action<NetSample> _onSample;
    public ForwardingSink(Action<NetSample> onSample) => _onSample = onSample;
    public Task WriteAsync(NetSample sample, CancellationToken ct)
    {
        _onSample(sample);
        return Task.CompletedTask;
    }
    public Task WriteAsync(RuleFinding finding, CancellationToken ct) => Task.CompletedTask;
}
