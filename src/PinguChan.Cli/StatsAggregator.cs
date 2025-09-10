using System.Collections.Concurrent;
using PinguChan.Core.Models;

namespace PinguChan.Cli;

public sealed class StatsAggregator
{
    private readonly TimeSpan _window;
    private readonly ConcurrentDictionary<string, ConcurrentQueue<NetSample>> _byTarget = new();
    private readonly ConcurrentQueue<NetSample> _dns = new();
    private readonly ConcurrentQueue<NetSample> _http = new();

    public StatsAggregator(TimeSpan window)
    {
        _window = window;
    }

    public void Add(NetSample sample)
    {
        switch (sample.Kind)
        {
            case SampleKind.Ping:
                {
                    var q = _byTarget.GetOrAdd(sample.Target, _ => new ConcurrentQueue<NetSample>());
                    q.Enqueue(sample);
                    Prune(q);
                    break;
                }
            case SampleKind.Dns:
                _dns.Enqueue(sample); Prune(_dns); break;
            case SampleKind.Http:
                _http.Enqueue(sample); Prune(_http); break;
        }
    }

    private void Prune(ConcurrentQueue<NetSample> q)
    {
        var cutoff = DateTimeOffset.UtcNow - _window;
        while (q.TryPeek(out var s) && s.Timestamp < cutoff)
        {
            q.TryDequeue(out _);
        }
    }

    public (string line1, string line2) BuildSummary()
    {
        var parts = new List<string>();
        foreach (var kv in _byTarget.OrderBy(k => k.Key))
        {
            var arr = kv.Value.ToArray();
            if (arr.Length == 0) continue;
            var ok = arr.Count(s => s.Ok);
            var loss = 100.0 * (arr.Length - ok) / Math.Max(1, arr.Length);
            var ms = arr.Where(s => s.Ok && s.Milliseconds.HasValue).Select(s => s.Milliseconds!.Value).ToArray();
            var avg = ms.Length > 0 ? ms.Average() : (double?)null;
            parts.Add($"PING {kv.Key} loss {loss,5:F1}% ({arr.Length}){(avg.HasValue ? $" avg={avg:F0}ms" : string.Empty)}");
        }
        // DNS failure rate
        var dnsArr = _dns.ToArray();
        if (dnsArr.Length > 0)
        {
            var ok = dnsArr.Count(s => s.Ok);
            var loss = 100.0 * (dnsArr.Length - ok) / Math.Max(1, dnsArr.Length);
            var ms = dnsArr.Where(s => s.Ok && s.Milliseconds.HasValue).Select(s => s.Milliseconds!.Value).ToArray();
            var p95 = Percentile(ms, 0.95);
            parts.Add($"DNS loss {loss,5:F1}% ({dnsArr.Length}){(p95.HasValue ? $" p95={p95:F0}ms" : string.Empty)}");
        }
        // HTTP TTFB percentiles
        var httpArr = _http.ToArray();
        if (httpArr.Length > 0)
        {
            var ok = httpArr.Count(s => s.Ok);
            var fail = httpArr.Length - ok;
            var ms = httpArr.Where(s => s.Ok && s.Milliseconds.HasValue).Select(s => s.Milliseconds!.Value).OrderBy(x => x).ToArray();
            var p50 = Percentile(ms, 0.50);
            var p95 = Percentile(ms, 0.95);
            parts.Add($"HTTP ok={ok} fail={fail}{(p50.HasValue ? $" p50={p50:F0}ms" : string.Empty)}{(p95.HasValue ? $" p95={p95:F0}ms" : string.Empty)}");
        }
        var text = string.Join("  |  ", parts);
        // Split across up to two lines for readability
        if (text.Length <= Console.WindowWidth - 2)
            return (text, string.Empty);
        var mid = text.Length / 2;
        var split = text.IndexOf("|", mid, StringComparison.Ordinal);
        if (split < 0) split = mid;
        var line1 = text[..split].Trim().Trim('|');
        var line2 = text[(split + 1)..].Trim();
        return (line1, line2);
    }

    private static double? Percentile(double[] sorted, double p)
    {
        if (sorted == null || sorted.Length == 0) return null;
        var n = sorted.Length;
        var rank = p * (n - 1);
        var lo = (int)Math.Floor(rank);
        var hi = (int)Math.Ceiling(rank);
        if (lo == hi) return sorted[lo];
        var w = rank - lo;
        return sorted[lo] * (1 - w) + sorted[hi] * w;
    }
}
