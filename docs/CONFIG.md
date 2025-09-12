# Configuration Reference

This document lists key configuration fields relevant to orchestration and rules. All fields are optional; omitted values use safe defaults.

Durations accept "2s", "500ms", "1m", or standard TimeSpan format "hh:mm:ss".

## intervals
- ping: Frequency for ping probes. Default: 2s
- dns: Frequency for DNS probes. Default: 5s
- http: Frequency for HTTP probes. Default: 10s

## targets
- ping: List of IPs or hostnames. Example: ["1.1.1.1", "8.8.8.8"]
- dns: List of domains or resolvers to query. Example: ["github.com"]
- http: List of URLs for lightweight HTTP checks. Example: ["https://www.google.com/generate_204"]

## sinks
- csv: Output CSV path, relative to executable by default. Default: netwatch.csv
- jsonl: Output JSONL path. Default: netwatch.jsonl
- logs: Optional text log path. If set, console-style logs are also written here.
- appendTimestamp: If true (default), appends _YYYYMMDD_HHMMSS to sink filenames per run.

## orchestration.scheduler
Controls destination pools and scheduling etiquette.

- jitterPct: Random jitter applied to per-target min intervals. Default: 0.2 (±20%)
- backoffBase: Exponential backoff base when a target fails consecutively. Default: 2.0
- backoffMaxMultiplier: Cap for backoff multiplier. Default: 8
- decayHalfLife: Half-life to gradually decay failure streaks when a target recovers or is idle. Default: 00:05:00
- globalFloorPing/globalFloorDns/globalFloorHttp: Minimum spacing between any two probes of the same kind across all targets. Default: 0s (disabled)
- diagnosticsInterval: If set > 0, periodically logs pool diagnostics. Default: 00:01:00

## rules
Basic noise-reduction rules that emit findings when health crosses thresholds.

- consecutiveFailThreshold: Issue a finding after N consecutive failures for the same target. Default: 3
- quorum:
  - window: Rolling window length used to compute failure rates. Default: 00:01:00
  - failThreshold: Failure rate threshold (0..1). Example: 0.5 for 50%. Default: 0.5
  - minSamples: Minimum sample count required before evaluating threshold. Default: 5

## Example YAML
```yaml
intervals:
  ping: 2s
  dns: 5s
  http: 10s

targets:
  ping: ["1.1.1.1", "8.8.8.8"]
  dns: ["github.com"]
  http: ["https://www.google.com/generate_204"]

sinks:
  csv: netwatch.csv
  jsonl: netwatch.jsonl
  logs: pingu.log
  appendTimestamp: true

orchestration:
  scheduler:
    jitterPct: 0.2
    backoffBase: 2.0
    backoffMaxMultiplier: 8
    decayHalfLife: 00:05:00
  globalFloorPing: 0s
  globalFloorDns: 0s
  globalFloorHttp: 0s
  diagnosticsInterval: 00:01:00

rules:
  consecutiveFailThreshold: 3
  quorum:
    window: 00:01:00
    failThreshold: 0.5
    minSamples: 5

http:
  userAgent: "Pingu-chan/0.1 (netwatch)"
```

## Notes
- These options affect runtime etiquette and noise-reduction. Keep intervals modest and enable jitter to avoid bursts.
- Backoff reduces load during outages; decay helps recover faster after stability returns.
- Rule findings are exported to sinks like other samples for later analysis.