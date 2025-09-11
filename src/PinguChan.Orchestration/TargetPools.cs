using System.Collections.Concurrent;

namespace PinguChan.Orchestration;

public sealed class TargetPools : ITargetPools
{
    private sealed class Entry
    {
        public required string Key { get; init; }
        public required int Weight { get; init; }
        public required TimeSpan MinInterval { get; init; }
        public DateTimeOffset NextDue { get; set; }
        public int FailureCount { get; set; }
    }

    private readonly ConcurrentDictionary<string, List<Entry>> _pools = new();
    private readonly Random _rng = new();
    private readonly double _jitterPct;
    private readonly double _backoffBase;
    private readonly int _backoffMaxMultiplier;

    public TargetPools(double jitterPct = 0.2, double backoffBase = 2.0, int backoffMaxMultiplier = 8)
    {
        _jitterPct = Math.Clamp(jitterPct, 0, 1);
        _backoffBase = Math.Max(1.0, backoffBase);
        _backoffMaxMultiplier = Math.Max(1, backoffMaxMultiplier);
    }

    public TargetPools Add(string kind, string targetKey, int weight, TimeSpan minInterval, DateTimeOffset? now = null)
    {
        var list = _pools.GetOrAdd(kind, _ => new List<Entry>());
        list.Add(new Entry
        {
            Key = targetKey,
            Weight = Math.Max(1, weight),
            MinInterval = minInterval <= TimeSpan.Zero ? TimeSpan.FromSeconds(1) : minInterval,
            NextDue = now ?? DateTimeOffset.UtcNow,
            FailureCount = 0
        });
        return this;
    }

    public (string targetKey, TimeSpan delay)? TryGetNext(string kind, DateTimeOffset now)
    {
        if (!_pools.TryGetValue(kind, out var list) || list.Count == 0)
            return null;

        // eligible entries due now or earlier
        var eligible = list.Where(e => e.NextDue <= now).ToList();
        if (eligible.Count == 0)
        {
            // nothing due; return earliest next due as delay hint
            var next = list.MinBy(e => e.NextDue)!;
            var delay = next.NextDue - now;
            if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;
            return (next.Key, delay);
        }

        // weighted random pick among eligible
        var totalWeight = eligible.Sum(e => e.Weight);
        var pick = _rng.Next(totalWeight);
        Entry chosen = eligible[0];
        int acc = 0;
        foreach (var e in eligible)
        {
            acc += e.Weight;
            if (pick < acc) { chosen = e; break; }
        }

        // compute next due with jitter and backoff multiplier
        var baseDelay = chosen.MinInterval;
        var jitter = baseDelay.TotalMilliseconds * _jitterPct;
        var jitterOffsetMs = jitter <= 1 ? 0 : _rng.NextDouble() * jitter * ( _rng.Next(2) == 0 ? -1 : 1 );
        var backoffMult = Math.Min(_backoffMaxMultiplier, Math.Pow(_backoffBase, Math.Clamp(chosen.FailureCount, 0, 16)));
        var nextDelay = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * backoffMult + jitterOffsetMs);
        if (nextDelay < TimeSpan.FromMilliseconds(100)) nextDelay = TimeSpan.FromMilliseconds(100);
        chosen.NextDue = now + nextDelay;

        return (chosen.Key, baseDelay);
    }

    public void Report(string kind, string targetKey, bool ok)
    {
        if (!_pools.TryGetValue(kind, out var list)) return;
        var e = list.FirstOrDefault(x => x.Key == targetKey);
        if (e is null) return;
        if (ok) e.FailureCount = 0; else e.FailureCount = Math.Min(e.FailureCount + 1, 32);
    }
}
