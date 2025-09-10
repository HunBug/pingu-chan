using System.Text;
using System.Text.Json;
using PinguChan.Core.Models;

namespace PinguChan.Core.Sinks;

public sealed class CsvSink : IResultSink, IAsyncDisposable
{
    private readonly StreamWriter _writer;
    private readonly object _gate = new();

    public CsvSink(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        _writer = new StreamWriter(fs, new UTF8Encoding(false)) { AutoFlush = true };
        if (fs.Length == 0)
        {
            // header
            _writer.WriteLine("ts,kind,target,ok,ms,extra");
        }
    }

    public Task WriteAsync(NetSample sample, CancellationToken ct)
    {
        var extraEscaped = sample.Extra?.Replace('"', '\'').Replace("\n", " ") ?? string.Empty;
        var line = string.Join(',',
            sample.Timestamp.ToString("O"),
            sample.Kind,
            Escape(sample.Target),
            sample.Ok ? "true" : "false",
            sample.Milliseconds?.ToString("F3") ?? string.Empty,
            Escape(extraEscaped)
        );
        lock (_gate) { _writer.WriteLine(line); }
        return Task.CompletedTask;
    }

    public Task WriteAsync(RuleFinding finding, CancellationToken ct)
    {
        // CSV for findings could be separate; for MVP write as JSON in extra column with special kind
        var extra = JsonSerializer.Serialize(new
        {
            finding.RuleId,
            finding.Severity,
            finding.Message,
            finding.Context
        });
        var sample = new NetSample(finding.Timestamp, SampleKind.Collector, "finding:" + finding.RuleId, true, null, extra);
        return WriteAsync(sample, ct);
    }

    private static string Escape(string s)
    {
        if (s.Contains(',') || s.Contains('"')) return '"' + s.Replace("\"", "\"\"") + '"';
        return s;
    }

    public async ValueTask DisposeAsync()
    {
        await _writer.FlushAsync();
        _writer.Dispose();
    }
}

public sealed class JsonlSink : IResultSink, IAsyncDisposable
{
    private readonly StreamWriter _writer;
    private readonly object _gate = new();

    public JsonlSink(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        _writer = new StreamWriter(fs, new UTF8Encoding(false)) { AutoFlush = true };
    }

    public Task WriteAsync(NetSample sample, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(sample);
        lock (_gate) { _writer.WriteLine(json); }
        return Task.CompletedTask;
    }

    public Task WriteAsync(RuleFinding finding, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(finding);
        lock (_gate) { _writer.WriteLine(json); }
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _writer.FlushAsync();
        _writer.Dispose();
    }
}
