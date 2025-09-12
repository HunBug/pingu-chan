using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Channels;
using PinguChan.Core.Models;

namespace PinguChan.Core.Runtime;

public sealed class MonitorHost : IAsyncDisposable
{
    private readonly IReadOnlyList<IProbe> _probes;
    private readonly IReadOnlyList<ICollector> _collectors;
    private readonly IReadOnlyList<IResultSink> _sinks;
    private readonly Channel<NetSample> _bus = Channel.CreateBounded<NetSample>(new BoundedChannelOptions(1024)
    {
        FullMode = BoundedChannelFullMode.DropOldest
    });
    private readonly CancellationTokenSource _cts = new();
    private readonly List<Task> _workers = new();
    private readonly TaskCompletionSource _stoppedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private volatile bool _stopping;

    public TimeSpan SummaryInterval { get; init; } = TimeSpan.FromSeconds(30);

    public MonitorHost(IEnumerable<IProbe> probes, IEnumerable<IResultSink> sinks)
    {
        _probes = probes.ToList();
        _sinks = sinks.ToList();
        _collectors = Array.Empty<ICollector>();
    }

    public MonitorHost(IEnumerable<IProbe> probes, IEnumerable<ICollector> collectors, IEnumerable<IResultSink> sinks)
    {
        _probes = probes.ToList();
        _collectors = collectors.ToList();
        _sinks = sinks.ToList();
    }

    public async Task RunAsync(TimeSpan? duration = null, bool once = false)
    {
        LoggerHelper.LogInfo($"Starting monitor with {_probes.Count} probes");
    // writer for internal bus
    _workers.Add(Task.Run(WriterLoop));
    // writer for orchestration samples bus
    _workers.Add(Task.Run(SamplesWriterLoop));
    // findings writer
    _workers.Add(Task.Run(FindingsWriterLoop));
        // probes
        foreach (var probe in _probes)
        {
            _workers.Add(Task.Run(() => ProbeLoop(probe)));
        }
        // collectors
        foreach (var collector in _collectors)
        {
            _workers.Add(Task.Run(() => CollectorLoop(collector)));
        }
    // summaries disabled for now; live stats are shown by the CLI renderer

        if (once)
        {
            // run one cycle of each probe
            await Task.WhenAll(_probes.Select(p => EmitOnce(p)));
            await Task.Delay(100); // let writer flush
            await StopAsync();
            return;
        }

        if (duration.HasValue)
        {
            try { await Task.Delay(duration.Value, _cts.Token); }
            catch (OperationCanceledException) { }
            await StopAsync();
            return;
        }

        // Indefinite: wait until StopAsync signals completion
        await _stoppedTcs.Task;
    }

    private async Task EmitOnce(IProbe probe)
    {
        try
        {
            var sample = await probe.ExecuteAsync(_cts.Token);
            await _bus.Writer.WriteAsync(sample, _cts.Token);
        }
        catch (Exception ex)
        {
            LoggerHelper.LogWarn($"Probe {probe.Id} single run failed: {ex.Message}");
        }
    }

    private async Task ProbeLoop(IProbe probe)
    {
        while (!_cts.IsCancellationRequested)
        {
            var t0 = DateTime.UtcNow;
            try
            {
                var sample = await probe.ExecuteAsync(_cts.Token);
                await _bus.Writer.WriteAsync(sample, _cts.Token);
            }
            catch (Exception ex)
            {
                LoggerHelper.LogWarn($"Probe {probe.Id} failed: {ex.Message}");
            }
            var elapsed = DateTime.UtcNow - t0;
            var delay = probe.Interval - elapsed;
            if (delay > TimeSpan.Zero)
            {
                try { await Task.Delay(delay, _cts.Token); } catch { }
            }
        }
    }

    private async Task CollectorLoop(ICollector collector)
    {
        while (!_cts.IsCancellationRequested)
        {
            var t0 = DateTime.UtcNow;
            try
            {
                var samples = await collector.CollectAsync(_cts.Token);
                foreach (var sample in samples)
                {
                    await _bus.Writer.WriteAsync(sample, _cts.Token);
                }
            }
            catch (Exception ex)
            {
                LoggerHelper.LogWarn($"Collector {collector.Id} failed: {ex.Message}");
            }
            var elapsed = DateTime.UtcNow - t0;
            var delay = collector.Interval - elapsed;
            if (delay > TimeSpan.Zero)
            {
                try { await Task.Delay(delay, _cts.Token); } catch { }
            }
        }
    }

    private async Task WriterLoop()
    {
        // Do not use cancellation here so we can drain the channel after completion
    await foreach (var sample in _bus.Reader.ReadAllAsync())
        {
            foreach (var sink in _sinks)
            {
        try { await sink.WriteAsync(sample, CancellationToken.None); }
                catch (Exception ex) { LoggerHelper.LogError($"Sink write failed: {ex.Message}"); }
            }
            // Emit a console log for each sample
            LogSample(sample);
        }
    }

    private async Task FindingsWriterLoop()
    {
        try
        {
            await foreach (var finding in FindingsBus.ReadAllAsync(_cts.Token))
            {
                foreach (var sink in _sinks)
                {
                    try { await sink.WriteAsync(finding, CancellationToken.None); }
                    catch (Exception ex) { LoggerHelper.LogError($"Finding write failed: {ex.Message}"); }
                }
                LoggerHelper.LogWarn($"FINDING {finding.RuleId}: {finding.Message}");
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task SamplesWriterLoop()
    {
        try
        {
            await foreach (var sample in SamplesBus.ReadAllAsync(_cts.Token))
            {
                foreach (var sink in _sinks)
                {
                    try { await sink.WriteAsync(sample, CancellationToken.None); }
                    catch (Exception ex) { LoggerHelper.LogError($"Sink write failed (orchestrated): {ex.Message}"); }
                }
                LogSample(sample);
            }
        }
        catch (OperationCanceledException) { }
    }

    private static void LogSample(NetSample sample)
    {
        var kind = sample.Kind.ToString().ToUpperInvariant();
        var tag = kind switch
        {
            "PING" => "PING",
            "DNS" => "DNS",
            "HTTP" => "HTTP",
            _ => kind
        };
    var status = sample.Ok ? "OK" : "FAIL";
    var ms = sample.Milliseconds.HasValue ? $" {sample.Milliseconds.Value:0}ms" : string.Empty;
    var extra = string.IsNullOrWhiteSpace(sample.Extra) ? string.Empty : $" | {Truncate(sample.Extra!, 120)}";
    var line = $"{tag} {sample.Target} {status}{ms}{extra}";
        if (sample.Ok) LoggerHelper.LogInfo(line); else LoggerHelper.LogWarn(line);
    }

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s[..max] + "…";

    public async Task StopAsync()
    {
        if (_stopping) return;
        _stopping = true;
        // Stop producer loops (probes/summary)
        _cts.Cancel();
    // Complete internal channel and cancel readers
    _bus.Writer.TryComplete();
    try { await Task.WhenAll(_workers); } catch (OperationCanceledException) { } catch { }
        foreach (var sink in _sinks.OfType<IAsyncDisposable>())
        {
            try { await sink.DisposeAsync(); } catch { }
        }
        LoggerHelper.LogInfo("Monitor stopped");
        _stoppedTcs.TrySetResult();
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _cts.Dispose();
    }
}

public static class LoggerHelper
{
    public static readonly object ConsoleSync = new();
    public static object? ExternalLogger { get; set; }
    public static void LogDebug(string message)
    {
        if (TryExternal("LogDebug", message)) return;
        lock (ConsoleSync) { Console.WriteLine($"{DateTime.Now:HH:mm:ss} [DBG] {message}"); }
    }
    public static void LogInfo(string message)
    {
        if (TryExternal("LogInfo", message)) return;
        lock (ConsoleSync) { Console.WriteLine($"{DateTime.Now:HH:mm:ss} [INF] {message}"); }
    }
    public static void LogWarn(string message)
    {
        if (TryExternal("LogWarn", message)) return;
        lock (ConsoleSync) { Console.WriteLine($"{DateTime.Now:HH:mm:ss} [WRN] {message}"); }
    }
    public static void LogError(string message)
    {
        if (TryExternal("LogError", message)) return;
        lock (ConsoleSync) { Console.WriteLine($"{DateTime.Now:HH:mm:ss} [ERR] {message}"); }
    }

    private static bool TryExternal(string method, string message)
    {
        var logger = ExternalLogger;
        if (logger is null) return false;
        try
        {
            var mi = logger.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.Instance);
            if (mi is null) return false;
            mi.Invoke(logger, new object?[] { message });
            return true;
        }
        catch { return false; }
    }
}
