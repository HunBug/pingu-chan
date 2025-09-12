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
    public bool InFlight { get; set; }
        public DateTimeOffset LastUpdated { get; set; }
    }

    private readonly ConcurrentDictionary<string, List<Entry>> _pools = new();
    private readonly Random _rng = new();
    private readonly double _jitterPct;
    private readonly double _backoffBase;
    private readonly int _backoffMaxMultiplier;
    private readonly TimeSpan _decayHalfLife;

    public TargetPools(double jitterPct = 0.2, double backoffBase = 2.0, int backoffMaxMultiplier = 8, TimeSpan? decayHalfLife = null)
    {
        _jitterPct = Math.Clamp(jitterPct, 0, 1);
        _backoffBase = Math.Max(1.0, backoffBase);
        _backoffMaxMultiplier = Math.Max(1, backoffMaxMultiplier);
        _decayHalfLife = decayHalfLife is { } d && d > TimeSpan.Zero ? d : TimeSpan.FromMinutes(5);
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
            FailureCount = 0,
            LastUpdated = now ?? DateTimeOffset.UtcNow
        });
        return this;
    }

    public (string targetKey, TimeSpan delay)? TryGetNext(string kind, DateTimeOffset now)
    {
        if (!_pools.TryGetValue(kind, out var list) || list.Count == 0)
            return null;

        // apply decay to failure counts based on idle time
        foreach (var e in list)
        {
            if (e.FailureCount > 0)
            {
                var elapsed = now - e.LastUpdated;
                if (elapsed > TimeSpan.Zero)
                {
                    var halves = elapsed.TotalSeconds / _decayHalfLife.TotalSeconds;
                    if (halves > 0)
                    {
                        var decayed = (int)Math.Floor(e.FailureCount * Math.Pow(0.5, halves));
                        if (decayed < e.FailureCount)
                            e.FailureCount = decayed;
                    }
                }
            }
        }

        // eligible entries due now or earlier
        var eligible = list.Where(e => e.NextDue <= now && !e.InFlight).ToList();
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
    chosen.InFlight = true; // guard parallelism per target
        chosen.LastUpdated = now;

    // When a key is assigned now, return zero delay (caller should execute immediately)
    return (chosen.Key, TimeSpan.Zero);
    }

    public void Report(string kind, string targetKey, bool ok)
    {
        if (!_pools.TryGetValue(kind, out var list)) return;
        var e = list.FirstOrDefault(x => x.Key == targetKey);
        if (e is null) return;
    if (ok) e.FailureCount = 0; else e.FailureCount = Math.Min(e.FailureCount + 1, 32);
    e.InFlight = false;
        e.LastUpdated = DateTimeOffset.UtcNow;
    }

    public IReadOnlyList<TargetPoolDiagnostic> GetDiagnostics(string kind)
    {
        if (!_pools.TryGetValue(kind, out var list) || list.Count == 0) return Array.Empty<TargetPoolDiagnostic>();
        return list.Select(e => new TargetPoolDiagnostic(e.Key, e.NextDue, e.FailureCount, e.InFlight, e.MinInterval)).ToList();
    }
}
