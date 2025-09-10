# Pingu-chan MVP Implementation Plan (Core + Minimal CLI)

Focus: deliver a usable, cross-platform monitoring/diagnostics tool quickly. Build the Core first, then a minimal CLI that runs diagnostics and monitoring.

## MVP status snapshot
- Core runtime: Done (bounded Channel bus, per-probe loops, graceful stop, drain on shutdown)
- Probes: Done (Ping, DNS, HTTP)
- Sinks: Done (CSV, JSONL with flush). File logs: Done (optional `sinks.logs`)
- CLI: Done (run/diagnose, duration/once; two-line TUI status; per-sample logs; Ctrl+C shutdown)
- Config: Done (YAML/JSON loader; `intervals`, `targets`, `sinks`) — extended options deferred
- Capability discovery: Partial (basic scanner + CLI output)
- Stats: Done (live rollups shown in TUI)
- Tests: Partial (CSV sink, config loader)
- Deferred: additional collectors (Wi‑Fi/UPnP), rules engine, notifiers, DI host, log rotation, remote/API

## 1. Guiding Principles
- Cross-platform first: Windows, Linux, macOS.
- Modular and extensible: DI-driven, interface-first design for probes, collectors, rules, sinks, notifiers.
- Deterministic and observable: rich logs, environment capture, and periodic summaries.
- Safe defaults: no remote listeners by default; metadata only (no payload capture).
- Minimal external deps in Core; use standard .NET logging/formatting.

## 2. Logging & Output Strategy
Status: Done (console TUI + optional file logs + CSV/JSONL). Rotation deferred.

- Standard .NET logging abstraction: `LoggerHelper` forwards to an external `ILogger` when configured.
- Startup/diagnose logs: environment + config + capabilities in diagnose; monitoring logs are per-sample.
- Outputs implemented:
  - Console TUI (two bottom status lines updated live; logs scroll above without overwriting).
  - File logs (when `sinks.logs` is set) with timestamped, human-readable lines.
  - Data sinks CSV/JSONL with flush-on-write for tailing.
- Periodic summaries replaced by live TUI status; no extra 30s summary lines.

## 3. Config
Status: Done for MVP essentials; extended options deferred.

- Default path: `pingu.yaml` (YAML). CLI flag `--config` to override. JSON fallback supported.
- Implemented sections: `intervals` (ping/dns/http), `targets` (ping/dns/http), `sinks` (csv/jsonl/logs).
- Deferred sections: `monitor`, `log` (level/flush), `notify`, `remote`.
- Validation: friendly messages; sensible defaults when fields are omitted.

## 4. Core Abstractions
Status: Probes/sinks/event bus/host done; collectors/rules/notifiers deferred.
- Probes (active): `IProbe` → `ExecuteAsync(ct): NetSample`
- Collectors (passive): `ICollector` → `CollectAsync(ct): IReadOnlyCollection<NetSample>`
- Rules: `IRule` → `Evaluate(window, now): RuleFinding?`
- Result sinks: `IResultSink` (`WriteAsync(NetSample)` / `WriteAsync(RuleFinding)`) with flush policies.
- Notifiers: `INotifier` → `NotifyAsync(RuleFinding)`
- Event bus: `Channel<NetSample>` fan-out to sinks; another channel for `RuleFinding`.
- Scheduler: hosted-service-like scheduler for periodic execution with jitter and cancellation.

## 5. Component Self-Checks (Capability Discovery)
Status: Partial
- Purpose: enumerate available tools per OS, detect missing dependencies/privileges, provide install hints.
- Interface: `ICapability` with `Id`, `Description`, `RequiresSudo`, `CheckAsync(): CapabilityReport` where report includes `Available`, `ImplementedOnOs`, `ExternalDepsOk`, `Hints`.
- Examples:
  - Raw ICMP availability (Windows Ping vs Linux ping utility vs .NET Ping fallback).
  - `iw`/`nmcli` presence (Linux Wi-Fi info).
  - `traceroute`/`tracepath` availability.
  - Permissions: sudo/admin required? Current user elevated?
- Output: printed in diagnose; export to file deferred.

## 6. OS-specific Adapters
- Strategy: interface-first + per-OS implementations behind factories.
- Factory resolves by runtime OS (RuntimeInformation) and presence checks.
- Example: `IWifiInfoProvider`: Windows (Native WiFi API), Linux (parse `iw`/`nmcli`), macOS (airport CLI).
- When not implemented on current OS: record `ImplementedOnOs=false`, include hints.

## 7. Diagnostics (One-shot)
Status: Partial (env + capabilities + one-shot probes; Markdown export deferred)
- Command: `diagnose` (CLI)
- Steps:
  1) Capture environment + capabilities.
  2) Run quick bundle: Ping to gateway + 1.1.1.1 + 8.8.8.8; DNS resolve `github.com` via resolvers; HTTP GET `generate_204`.
  3) Summarize: loss/latency, DNS failure rate, HTTP TTFB.
  4) Export CSV/JSONL. Writing a short Markdown summary to stdout/file is planned.

## 8. Monitoring (Ongoing)
Status: Done (with live TUI status)
- Command: `run` (CLI)
- Each monitor tool runs in its own task; emit samples to bus.
- Frequency per tool configurable; independent cancellation and error handling.
- Live status replaces periodic summary lines. TUI shows ping loss/avg, DNS failure rate p95, HTTP TTFB p50/p95 per target.
- Rolling windows maintained in-memory by a lightweight `StatsAggregator` for UI; rules evaluation deferred.

## 9. Minimal CLI UX (Cute + Static Stats)
Status: Done
- Use ANSI sequences to render 1-2 static status lines at the bottom (like tqdm). Above, normal logs stream via `Console.WriteLine`.
- Color and theme: pastel palette, a tiny penguin prefix, emojis for state (😐 💢 😳 😭).
- `--quiet` planned; logs already can go to file.
- `--once` runs one full cycle and exits (handy for CI).
- If a robust tqdm-like .NET library exists with in-place rendering and concurrent writes, prefer it; otherwise implement a simple renderer (double-buffered lines + write-through logs).

## 10. Initial Probes/Collectors (MVP)
Status: Probes done (Ping/DNS/HTTP + Gateway/MTU); baseline collectors implemented; rules deferred
- Probes:
  - PingProbe (targets: gateway, 1.1.1.1, 8.8.8.8).
  - DnsProbe (domains via resolvers).
  - HttpProbe (204 endpoints; TTFB).
  - GatewayProbe (default route ping) — done
  - MtuProbe (best-effort path MTU) — done
- Collectors:
  - NetIfCollector (interfaces, IPs/MAC, gateway, DNS) — done
  - RouteCollector (route table snapshot) — done
  - FirewallCollector (ufw/firewalld/netsh/socketfilterfw) — done
  - TracerouteCollector (to 8.8.8.8) — done
  - InternetConnectivityCollector (gateway + DNS heuristic) — done
- Rules:
  - Loss threshold (1m warn/crit), DNS fail rate (1m warn), HTTP TTFB degradation.

## 11. Storage & Flushing
Status: Done for CSV/JSONL; rotation/SQLite deferred
- CSV/JSONL sinks with force-flush on each write (or small buffer with `AutoFlush` true) to enable tailing.
- Optional SQLite later.
- Rotation policy (by size/time) as a follow-up.

## 12. Dependency Injection
Status: Deferred (manual wiring in CLI for MVP)
- Use `Microsoft.Extensions.DependencyInjection`.
- Register interfaces for probes/collectors/rules/sinks/notifiers/capabilities.
- Use named registrations or options for per-target probes.
- Provide factories for OS-specific adapters; resolve by runtime checks.

## 13. Configuration Options (MVP)
Status: Implemented subset
- `intervals`: ping/dns/http
- `targets`: ping/dns/http
- `sinks`: csv path, jsonl path, logs file path (optional)
- Planned: `collect` and `summary` intervals, `log` level/flush, `notify`, `remote`

## 14. Privilege Awareness
- Check if elevated/admin; if a component requires sudo and not available, include a hint:
  - e.g., “This check needs root to send raw ICMP on Linux. Run with sudo or set CAP_NET_RAW.”
- Detect distro specifics (Debian/Arch variants) for install hints when feasible (best-effort).
 - CLI supports `--sudo` to cache sudo credentials at startup (Linux/macOS). Diagnostics continue without sudo if denied.

## 15. Threading & Back-pressure
- Each probe/collector runs in an async loop with `Task.Delay` and cancellation.
- Channel-based bus (bounded) to sinks; on pressure, either drop oldest or back off (configurable later).
- Logging to console/file via logger; stats renderer updated on a timer.

## 16. Quality Gates
Status: Build/test pass; smoke verified
- Build: `dotnet restore && dotnet build`.
- Test: `dotnet test` (mock network where possible) — basic tests in place (CSV sink, config loader).
- Smoke: `run --duration 00:00:10` creates CSV/JSONL, shows live TUI status, logs scroll without overwrites.

## 17. Milestones
- M0 (Done): Project scaffolding, config loader, TUI logging, CSV/JSONL sinks
- M1 (Partial): Ping/DNS/HTTP probes (done), collectors/rules (pending), CLI `run` with TUI (done)
- M2 (Partial): `diagnose` command (done), capability discovery/self-checks (basic), report export (pending)
- M3 (Planned): rotation policy, jitter handling, richer Wi-Fi info; optional notify

## 18. Open Questions
- Which tqdm-like .NET library is acceptable? If none, proceed with in-house renderer.
- Preferred color theme/palette for CLI (we can prototype and iterate).
- Minimum viable distro-detection granularity for install hints (os-release parsing?).

## 19. Suggested Enhancements
- Add a `stats` command that reads CSV/JSONL to print summaries (no monitoring).
- Add a Markdown report generator for `diagnose`.
- Add opt-in `--bind` for CLI to expose a minimal read-only HTTP status endpoint later.

---

## Next steps (proposed)
- Add log-level flag/env and `--quiet` mode while keeping file logs
- Implement MTU and gateway health probes; add simple rules (ping loss, DNS fail rate)
- Add file rotation (size/time) for CSV/JSONL/logs
- Export capability report to JSONL during `diagnose`
- Expand tests (probe timeouts, DurationParser edges, config variants)
