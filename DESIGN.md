It’s intentionally “Copilot-friendly”: concrete names, clear interfaces, and lots of TODOs.

---

# DESIGN.md — Pingu-chan 🐧💢

*A tsundere penguin network diagnostics & monitoring toolkit (C# / .NET)*

> “I-It’s not like I care about your packet loss or anything…” — Pingu-chan

## Goals

* Cross-platform **core library** for diagnostics (no UI assumptions).
* Small **CLI** for always-on monitoring (human & CI friendly).
* Optional **GUI** (cross-platform) with anime flair (Avalonia).
* Optional **remote access** (gRPC/WebSocket) for dashboards & headless nodes.
* Focus on **issues detection/diagnosis**, not packet sniffing or discovery.
* Simple data formats (CSV/JSONL) for later analysis & ChatGPT ingestion.
* Extensible **probe/check** model; add features without changing the base.

## Tech baseline

* **.NET**: target **.NET 8 LTS** (upgrade path to .NET 9).
* **C#**: C# 12 features (primary constructors, required members).
* **Cross-platform**: Windows / Linux / macOS.
* **Packaging**: single-file self-contained binaries for common targets.

---

## High-level Architecture

```
+--------------------------------------------+
| PinguChan.Core (class lib)                 |
|  - Schedulers (HostedService-style)        |
|  - Probes (Ping, DNS, HTTP, UPnP, etc.)    |
|  - Collectors (link stats, driver logs)    |
|  - Checks/Rules Engine                     |
|  - Result & Event Bus                      |
|  - Storage & Export (CSV/JSONL/SQLite)     |
|  - Notifications (OS + webhooks)           |
|  - Config (YAML/JSON)                      |
+------------------------+-------------------+
                         |
            +------------+------------+
            |                         |
+-----------v----------+   +----------v-----------+
| PinguChan.Cli        |   | PinguChan.Gui        |
| (System.CommandLine) |   | (Avalonia UI)        |
| - run/daemon         |   | - Live graphs        |
| - one-shot diagnose  |   | - “tsundere” states  |
| - export/report      |   | - rule mgmt          |
+-----------+----------+   +----------+-----------+
            |                         |
+-----------v-----------+   +---------v-----------+
| PinguChan.Remote      |   | Integrations        |
| - gRPC service        |   | - Webhook/Slack     |
| - WebSocket streaming |   | - GitHub issue bot  |
+-----------------------+   +---------------------+
```

---

## Core Concepts

### Probes (active tests)

Small, periodic checks that produce samples:

* **PingProbe**: ICMP to targets (loss, latency).
* **DnsProbe**: resolve domains via specific resolvers.
* **HttpProbe**: GET head endpoints (status, TTFB).
* **UpnpProbe**: discover via SSDP & query NAT/UPnP (Open.NAT).
* **GatewayProbe**: ping default gateway. (Implemented)
* **MtuProbe**: DF pings with payload sweep to find path MTU. (Implemented, best-effort)

### Collectors (passive/local facts)

Snapshot local state:

* **NetIfCollector**: interfaces, IPs/MAC, gateway, DNS. (Implemented)
* **RouteCollector**: route table snapshot. (Implemented)
* **FirewallCollector**: firewall state (ufw/firewalld/netsh/socketfilterfw). (Implemented)
* **TracerouteCollector**: trace to 8.8.8.8 with hop list. (Implemented)
* **InternetConnectivityCollector**: gateway + DNS presence heuristic. (Implemented)
* **OsLogCollector**: (optional) tail iwlwifi/syslog (Linux) or EventLog (Windows).
* **WifiCollector**: SSID/BSSID, band, channel, signal. (Windows: Native WiFi API; Linux: parse `iw`/`nmcli` fallbacks.)

### Checks / Rules

A simple rule engine evaluates probe/collector output:

* Thresholds (e.g., loss > 5% last 1m).
* Trend/variance (jitter burst).
* Correlators (loss + AP roam = likely Wi-Fi problem).
* Degradation detectors (HTTP TTFB ↑ while ICMP ok = DNS/CDN problem).

Configurable, declarative rules via YAML/JSON.

---

## Data Model (Core)

```csharp
namespace PinguChan.Core.Models;

public enum SampleKind { Ping, Dns, Http, Upnp, Gateway, Mtu, Collector }
public enum Severity { Info, Warning, Critical }

public record NetSample(
    DateTimeOffset Timestamp,
    SampleKind Kind,
    string Target,
    bool Ok,
    double? Milliseconds,
    string? Extra // JSON blob (resolver, status code, iface, rssi, etc.)
);

public record RuleFinding(
    DateTimeOffset Timestamp,
    string RuleId,
    Severity Severity,
    string Message,
    IReadOnlyDictionary<string, object>? Context = null
);
```

### Storage formats

* **CSV** (human friendly): `netwatch.csv` columns: `ts,kind,target,ok,ms,extra`
* **JSONL** (machine friendly): each `NetSample` as a line
* **SQLite** (optional): `samples(ruleid, ts, kind, target, ok, ms, extra)`

---

## Core Interfaces

```csharp
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
```

### Event Bus

* In-process channel (e.g., `Channel<NetSample>`) that fan-outs to sinks (file, SQLite, WebSocket broadcast, GUI).

---

## Configuration

`pingu.yaml` (default search in working dir, else env var)

```yaml
intervals:
  ping: 2s
  dns: 5s
  http: 10s
  collect: 30s

targets:
  ping:
    - 1.1.1.1
    - 8.8.8.8
    - gateway     # special token -> default route
  dns:
    resolvers: [1.1.1.1, 8.8.8.8]
    domains: [google.com, github.com]
  http:
    - https://www.google.com/generate_204
    - https://1.1.1.1/

rules:
  - id: loss_1m_warn
    kind: ping
    window: 1m
    threshold_loss_pct: 5
    severity: Warning
  - id: loss_1m_crit
    kind: ping
    window: 1m
    threshold_loss_pct: 20
    severity: Critical
  - id: dns_fail_1m
    kind: dns
    window: 1m
    threshold_fail_pct: 10
    severity: Warning

sinks:
  csv: netwatch.csv
  jsonl: netwatch.jsonl
  sqlite: pingu.db

notify:
  # pick one or many
  console: true
  webhook:
    url: https://example.com/hook
  slack:
    webhook_url: ...
```

---

## CLI (PinguChan.Cli)

Command style: **System.CommandLine**

```
pingu
  run                           # start monitoring (foreground)
    --config pingu.yaml
    --duration 00:10:00         # optional
    --once                      # one full cycle & exit
    --export csv|jsonl|sqlite   # override sinks
    --quiet                     # no console stats
  --sudo                      # Linux/macOS: cache sudo at startup (optional)
  diagnose                      # run a “bundle” of quick tests and print summary
  stats                         # print rolling stats from local files
    --window 1m|1h|1d
  report                        # generate a markdown report
  version
```

### Console output style (live)

Two-line live status refreshed each second: PING loss/avg per target, DNS fail%/p95, HTTP TTFB p50/p95. Logs scroll above.

---

## Remote (optional module)

* **Web service (HTTP/JSON)**: query latest samples, rolling stats, and control basic actions. Designed to be consumed by the remote CLI and GUI clients.
* **WebSocket**: live stream of samples/findings for dashboards and real-time views.
* **Optional embedded web UI**: a small built-in web GUI served by the remote service for quick remote access without installing the full GUI.
* **Auth & safety**: simple token in dev; pluggable auth/CORS later. No listeners by default; opt-in via config.
* **Config**: enable/disable via YAML; bind address/port configurable.

---

## GUI (PinguChan.Gui — later)

* **Avalonia UI** (cross-platform).
* Panels:

  * Live status (faces: 😐 / 💢 / 😳 / 😭).
  * Rolling graphs (loss/latency, DNS failures).
  * Rule findings timeline (click → details).
  * Interface info (SSID, BSSID, RSSI, channel).
* Theme: pastel ice-blue, chibi penguin avatar in corner reacting to health.
* Connect to local bus or remote WebSocket.

---

## Notifications

* **Console** (always available).
* **System notifications** (optional):

  * Windows: `Microsoft.Toolkit.Uwp.Notifications` (toast).
  * Linux: `notify-send` shell bridge.
  * macOS: `terminal-notifier` bridge.
* **Webhooks/Slack**: simple JSON POST on `RuleFinding`.

---

## Storage / Export

* **CSV** and **JSONL** by default.
* Optional **SQLite** via `Microsoft.Data.Sqlite`.
* Rotation policy (by size or time) to avoid huge files.

## Logging approach (current)

Core uses a minimal LoggerHelper to forward logs to a simple logger implemented by the CLI TUI. When configured via `sinks.logs`, logs are also mirrored to a file. Core avoids heavy logging dependencies.

---

## Destination pools, rotation, and etiquette (proposed)

To be a good network citizen and improve the quality of measurements, probes should select from destination pools using rotation, jitter, and backoff with per-target limits.

### Config schema additions

```yaml
pools:
  ping:
    - host: gateway         # special token → default route
      weight: 3
      min_interval: 5s
    - host: 1.1.1.1
      weight: 2
      min_interval: 10s
    - host: 8.8.8.8
      weight: 2
      min_interval: 10s
    - host: 9.9.9.9
      weight: 1
      min_interval: 15s
  dns:
    resolvers: [1.1.1.1, 8.8.8.8]
    domains:
      - name: github.com
        weight: 2
      - name: wikipedia.org
        weight: 1
  http:
    - url: https://www.google.com/generate_204
      method: HEAD
      weight: 3
      min_interval: 1m
    - url: https://httpbin.org/status/204
      method: HEAD
      weight: 1
      min_interval: 2m
    - url: https://example.com/
      method: HEAD
      weight: 1
      min_interval: 5m

scheduler:
  jitter_pct: 0.2                   # ±20% jitter applied to intervals
  failure_backoff:
    base: 2.0                       # exponential backoff multiplier
    max_multiplier: 8               # cap the backoff
  per_target_concurrency: 1         # avoid parallel hits to same host
  rotate_strategy: weighted_random  # or round_robin

etiquette:
  traceroute_min_interval: 15m
  http_user_agent: "Pingu-chan/1.x (+https://github.com/HunBug/pingu-chan)"
  prefer_local_first: true          # gateway + resolver preferred scheduling
```

### Runtime behavior
- Build per-kind pools at startup (and when config is reloaded).
- Each iteration selects a target using weighted random (or RR), respecting per-target `min_interval`.
- Apply jitter to base intervals; on failure, apply exponential backoff per target up to the configured cap.
- Limit concurrent requests per host to 1 to avoid burstiness.
- For HTTP, prefer HEAD where possible and set a descriptive User-Agent from config.
- For traceroute, enforce a separate (coarse) scheduler with a high minimum interval.

### Hot reload (optional)
- Watch the config file; on change, atomically swap pool definitions and scheduler parameters without dropping in-flight tasks.
- Preserve per-target backoff state across reloads by keying on host/URL.

### Observability
- Emit periodic scheduler diagnostics: last rotation order, targets skipped due to `min_interval`, current backoff per target.
- Include pool name and target key in `NetSample.Extra` for offline analysis.

### Safety rails
- Absolute floor for intervals (e.g., ping ≥ 1s, DNS ≥ 2s, HTTP ≥ 30s) even if misconfigured lower.
- Deny-list and allow-list hooks in config to block/force certain hosts.

See `docs/DESTINATION_POOLS.md` for curated examples and guidance.

---

## Implementation Plan (Milestones)

### M0 — Repo scaffolding

* Solution with 3 projects:

  * `PinguChan.Core`
  * `PinguChan.Cli`
  * `PinguChan.Tests`
* Add `DESIGN.md`, `README.md`, MIT `LICENSE`.
* GitHub Actions: build + test on win/linux.

### M1 — Minimum viable monitor

* Core models & event bus.
* **PingProbe**, **DnsProbe**, **GatewayProbe**.
* CSV/JSONL sinks.
* Rolling console stats (1m/1h/1d).
* YAML config loader.
* Basic rules (loss%/fail% thresholds) + console notifier.

### M2 — Diagnostics & collectors

* **NetIfCollector** (cross-platform).
* **WifiCollector** (with OS fallbacks).
* **HttpProbe** (generate\_204).
* Markdown **report** generator (“diagnose” command).

### M3 — Stability & insights

* Burst loss detector (runs test with DF MTU sweep).
* Correlation rules: loss + roam + RSSI dip → likely Wi-Fi layer.
* Export to SQLite + simple `pingu stats` from DB.

### M4 — Remote / GUI foundations

* WebSocket/gRPC live stream of samples/findings.
* Avalonia GUI skeleton with live charts & Pingu-chan avatar.

### M5 — Notifications & integrations

* Slack/Webhook notifier + example payloads.
* Optional Windows/macOS/Linux native notifications.

---

## Key Packages (NuGet)

* **System.CommandLine** (CLI)
* **Serilog** (logging) + Console/File sinks
* **YamlDotNet** (config)
* **DnsClient** (richer DNS, optional; otherwise raw UDP client)
* **Open.NAT** (UPnP/NAT-PMP; optional)
* **Microsoft.Data.Sqlite** (optional SQLite)
* **Grpc.AspNetCore** / **WebsocketListener** (remote; optional)
* **Avalonia** (GUI; optional)

*(Minimize dependencies in Core; keep extras optional.)*

---

## Example: PingProbe

```csharp
public sealed class PingProbe : IProbe
{
    private readonly string _target;
    public string Id { get; }
    public TimeSpan Interval { get; }

    public PingProbe(string target, TimeSpan? interval = null)
    {
        _target = target;
        Id = $"ping:{target}";
        Interval = interval ?? TimeSpan.FromSeconds(2);
    }

    public async Task<NetSample> ExecuteAsync(CancellationToken ct)
    {
        var t0 = DateTimeOffset.UtcNow;
        using var p = new System.Net.NetworkInformation.Ping();
        try
        {
            var reply = await p.SendPingAsync(_target, 1000);
            var ok = reply.Status == IPStatus.Success;
            var ms = ok ? (double?)reply.RoundtripTime : null;
            return new NetSample(t0, SampleKind.Ping, _target, ok, ms, null);
        }
        catch
        {
            return new NetSample(t0, SampleKind.Ping, _target, false, null, null);
        }
    }
}
```

---

## Example: Loss% Rule (1m)

```csharp
public sealed class LossThresholdRule : IRule
{
    public string Id { get; init; } = "loss_1m_warn";
    public Severity DefaultSeverity { get; init; } = Severity.Warning;
    public SampleKind Kind { get; init; } = SampleKind.Ping;
    public TimeSpan Window { get; init; } = TimeSpan.FromMinutes(1);
    public string Target { get; init; } = "1.1.1.1";
    public double ThresholdLossPct { get; init; } = 5;

    public RuleFinding? Evaluate(IReadOnlyList<NetSample> window, DateTimeOffset now)
    {
        var from = now - Window;
        var relevant = window.Where(s => s.Kind == Kind && s.Target == Target && s.Timestamp >= from).ToList();
        if (relevant.Count < 5) return null;
        var ok = relevant.Count(s => s.Ok);
        var loss = 100.0 * (relevant.Count - ok) / relevant.Count;
        return loss >= ThresholdLossPct
            ? new RuleFinding(now, Id, DefaultSeverity, $"Loss {loss:F1}% over {Window.TotalSeconds:F0}s", new Dictionary<string, object>{{"target",Target},{"loss_pct",loss},{"count",relevant.Count}})
            : null;
    }
}
```

---

## Testing Strategy

* **Unit tests** for probes with fakes (timeout/error paths).
* **Rule tests** with synthetic windows.
* **Config loader** tests (validation & defaults).
* **End-to-end smoke**: run `pingu run --duration 00:00:10` and assert files created.

GitHub Actions:

* Matrix build: `ubuntu-latest`, `windows-latest`.
* Artifacts: zipped CLI binaries.

---

## Security & Privacy

* No packet payload capture; metadata only (timestamps, hostnames, timings).
* Config opt-in for remote endpoints; no network listeners by default.
* Log redaction (tokens/webhook URLs).
* User-data disclaimer in README.

---

## Performance Notes

* Use async timers and `Channel<T>` fan-out.
* Keep probe intervals configurable; cap concurrency to avoid CPU spikes.
* Sinks buffered with back-pressure (bounded channel, drop oldest or block).

---

## Nice-to-haves (Later)

* Auto-diagnose playbooks (run MTU sweep if loss bursts detected).
* Per-iface binding (Wi-Fi vs Ethernet comparison).
* Multi-node federation (central dashboard).
* Plugin model via `AssemblyLoadContext` (drop-in probes/checks).

---

## Repo Structure

```
/src
  PinguChan.Core/
  PinguChan.Cli/
  PinguChan.Gui/            (later)
  PinguChan.Remote/         (later)
/tests
  PinguChan.Tests/
DESIGN.md
README.md
LICENSE
pingu.yaml (example)
```

---

## README one-liner (for now)

> **Pingu-chan** 🐧💢 — a tsundere penguin who totally doesn’t care about your network… while obsessively monitoring ping, DNS, and HTTP health, logging issues, and yelling when packets go missing.

