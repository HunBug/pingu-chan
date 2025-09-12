using PinguChan.Core.Models;

namespace PinguChan.Orchestration;

public sealed class StatsService : IStatsService
{
    private sealed record Key(string Kind, string Target);
    private readonly Dictionary<Key, LinkedList<(DateTimeOffset ts, bool ok, double? ms)>> _data = new();

    public void Observe(NetSample sample)
    {
        var kind = sample.Kind.ToString().ToLowerInvariant();
        var key = new Key(kind, sample.Target);
        if (!_data.TryGetValue(key, out var list))
        {
            list = new LinkedList<(DateTimeOffset, bool, double?)>();
            _data[key] = list;
        }
        list.AddLast((sample.Timestamp, sample.Ok, sample.Milliseconds));
        // prune older than 10 minutes by default to cap memory
        var cutoff = sample.Timestamp - TimeSpan.FromMinutes(10);
        while (list.First is not null && list.First.Value.ts < cutoff)
            list.RemoveFirst();
    }

    public (int count, int ok, double failPct)? TryGetWindow(string kind, string targetKey, TimeSpan window, DateTimeOffset now)
    {
        var key = new Key(kind.ToLowerInvariant(), targetKey);
        if (!_data.TryGetValue(key, out var list)) return null;
        var cutoff = now - window;
        int total = 0, oks = 0;
        for (var node = list.Last; node is not null; node = node.Previous)
        {
            if (node.Value.ts < cutoff) break;
            total++;
            if (node.Value.ok) oks++;
        }
        if (total == 0) return (0, 0, 0);
        var fails = total - oks;
        var failPct = (double)fails / total;
        return (total, oks, failPct);
    }

    // Extended API: includes latency percentiles over OK samples in window.
    public (int count, int ok, double failPct, double? p50, double? p95)? TryGetWindowWithLatency(string kind, string targetKey, TimeSpan window, DateTimeOffset now)
    {
        var key = new Key(kind.ToLowerInvariant(), targetKey);
        if (!_data.TryGetValue(key, out var list)) return null;
        var cutoff = now - window;
        int total = 0, oks = 0;
        List<double> lat = new();
        for (var node = list.Last; node is not null; node = node.Previous)
        {
            if (node.Value.ts < cutoff) break;
            total++;
            if (node.Value.ok)
            {
                oks++;
                if (node.Value.ms is double v) lat.Add(v);
            }
        }
        if (total == 0) return (0, 0, 0, null, null);
        var fails = total - oks;
        var failPct = (double)fails / total;
        double? p50 = null, p95 = null;
        if (lat.Count > 0)
        {
            lat.Sort();
            p50 = QuantileNearestRank(lat, 0.5);
            p95 = QuantileNearestRank(lat, 0.95);
        }
        return (total, oks, failPct, p50, p95);
    }

    private static double QuantileNearestRank(List<double> sorted, double q)
    {
        if (sorted.Count == 0) return double.NaN;
        if (q <= 0) return sorted[0];
        if (q >= 1) return sorted[^1];
        var rank = (int)Math.Ceiling(q * sorted.Count);
        var idx = Math.Clamp(rank - 1, 0, sorted.Count - 1);
        return sorted[idx];
    }
}
