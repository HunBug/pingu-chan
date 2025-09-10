# Pingu-chan MVP Implementation Plan (Core + Minimal CLI)

Focus: deliver a usable, cross-platform monitoring/diagnostics tool quickly. Build the Core first, then a minimal CLI that runs diagnostics and monitoring.

## 1. Guiding Principles
- Cross-platform first: Windows, Linux, macOS.
- Modular and extensible: DI-driven, interface-first design for probes, collectors, rules, sinks, notifiers.
- Deterministic and observable: rich logs, environment capture, and periodic summaries.
- Safe defaults: no remote listeners by default; metadata only (no payload capture).
- Minimal external deps in Core; use standard .NET logging/formatting.

## 2. Logging & Output Strategy
- Use standard .NET logging (`ILogger<T>`) with scopes and structured logging.
- Log order at startup:
  1) Environment snapshot (OS, runtime, CPU, net interfaces, privileges).
  2) Resolved configuration (from `config.yml`).
  3) Self-diagnostics (component availability and checks).
  4) Begin diagnostics run; then monitoring.
- Outputs:
  - Console (colorful, themed, with in-place stats lines for the CLI).
  - File logs (structured text; frequent flush on writes).
  - Data sinks (CSV/JSONL; flush after writes to enable tailing during runtime).
- Periodic summaries:
  - Every 30s by default: write rollups to stdout and logs (configurable).

## 3. Config
- Default path: `config.yml` (YAML). CLI flag `--config` to override.
- Sections:
  - `intervals` (per-probe/collector and summaries),
  - `targets` (ping, dns, http),
  - `monitor` (enable/disable components),
  - `sinks` (csv/jsonl/sqlite),
  - `log` (level, file path, flush policy),
  - `notify` (console/webhook/slack),
  - `remote` (disabled by default).
- Validation: clear messages; no start if invalid.

## 4. Core Abstractions
- Probes (active): `IProbe` → `ExecuteAsync(ct): NetSample`
- Collectors (passive): `ICollector` → `CollectAsync(ct): IReadOnlyCollection<NetSample>`
- Rules: `IRule` → `Evaluate(window, now): RuleFinding?`
- Result sinks: `IResultSink` (`WriteAsync(NetSample)` / `WriteAsync(RuleFinding)`) with flush policies.
- Notifiers: `INotifier` → `NotifyAsync(RuleFinding)`
- Event bus: `Channel<NetSample>` fan-out to sinks; another channel for `RuleFinding`.
- Scheduler: hosted-service-like scheduler for periodic execution with jitter and cancellation.

## 5. Component Self-Checks (Capability Discovery)
- Purpose: enumerate available tools per OS, detect missing dependencies/privileges, provide install hints.
- Interface: `ICapability` with `Id`, `Description`, `RequiresSudo`, `CheckAsync(): CapabilityReport` where report includes `Available`, `ImplementedOnOs`, `ExternalDepsOk`, `Hints`.
- Examples:
  - Raw ICMP availability (Windows Ping vs Linux ping utility vs .NET Ping fallback).
  - `iw`/`nmcli` presence (Linux Wi-Fi info).
  - `traceroute`/`tracepath` availability.
  - Permissions: sudo/admin required? Current user elevated?
- Output: printed at startup, logged, and exported to a `capabilities.jsonl`.

## 6. OS-specific Adapters
- Strategy: interface-first + per-OS implementations behind factories.
- Factory resolves by runtime OS (RuntimeInformation) and presence checks.
- Example: `IWifiInfoProvider`: Windows (Native WiFi API), Linux (parse `iw`/`nmcli`), macOS (airport CLI).
- When not implemented on current OS: record `ImplementedOnOs=false`, include hints.

## 7. Diagnostics (One-shot)
- Command: `diagnose` (CLI)
- Steps:
  1) Capture environment + capabilities.
  2) Run quick bundle: Ping to gateway + 1.1.1.1 + 8.8.8.8; DNS resolve `github.com` via resolvers; HTTP GET `generate_204`.
  3) Summarize: loss/latency, DNS failure rate, HTTP TTFB.
  4) Export CSV/JSONL and write a short Markdown summary to stdout and file.

## 8. Monitoring (Ongoing)
- Command: `run` (CLI)
- Each monitor tool runs in its own task; emit samples to bus.
- Frequency per tool configurable; independent cancellation and error handling.
- Periodic summaries (30s default) for loss%, jitter, DNS failure rate, HTTP TTFB p50/p95.
- Rolling windows maintained in-memory for rules evaluation.

## 9. Minimal CLI UX (Cute + Static Stats)
- Use ANSI sequences to render 1-2 static status lines at the bottom (like tqdm). Above, normal logs stream via `Console.WriteLine`.
- Color and theme: pastel palette, a tiny penguin prefix, emojis for state (😐 💢 😳 😭).
- `--quiet` hides periodic summaries; logs still go to file.
- `--once` runs one full cycle and exits (handy for CI).
- If a robust tqdm-like .NET library exists with in-place rendering and concurrent writes, prefer it; otherwise implement a simple renderer (double-buffered lines + write-through logs).

## 10. Initial Probes/Collectors (MVP)
- Probes:
  - PingProbe (targets: gateway, 1.1.1.1, 8.8.8.8).
  - DnsProbe (domains via resolvers).
  - HttpProbe (204 endpoints; TTFB).
- Collectors:
  - NetIfCollector (interfaces, speeds, up/down, basic Wi-Fi if feasible).
  - RouteCollector (default route, gateway address).
- Rules:
  - Loss threshold (1m warn/crit), DNS fail rate (1m warn), HTTP TTFB degradation.

## 11. Storage & Flushing
- CSV/JSONL sinks with force-flush on each write (or small buffer with `AutoFlush` true) to enable tailing.
- Optional SQLite later.
- Rotation policy (by size/time) as a follow-up.

## 12. Dependency Injection
- Use `Microsoft.Extensions.DependencyInjection`.
- Register interfaces for probes/collectors/rules/sinks/notifiers/capabilities.
- Use named registrations or options for per-target probes.
- Provide factories for OS-specific adapters; resolve by runtime checks.

## 13. Configuration Options (MVP)
- `intervals`: ping=2s, dns=5s, http=10s, collect=30s, summary=30s
- `targets`: ping (IPs + `gateway`), dns (resolvers + domains), http (URLs)
- `sinks`: csv path, jsonl path
- `log`: level (Info), file path, flush (true)
- `notify`: console: true (others later)
- `remote`: enabled: false (later)

## 14. Privilege Awareness
- Check if elevated/admin; if a component requires sudo and not available, include a hint:
  - e.g., “This check needs root to send raw ICMP on Linux. Run with sudo or set CAP_NET_RAW.”
- Detect distro specifics (Debian/Arch variants) for install hints when feasible (best-effort).

## 15. Threading & Back-pressure
- Each probe/collector runs in an async loop with `Task.Delay` and cancellation.
- Channel-based bus (bounded) to sinks; on pressure, either drop oldest or back off (configurable later).
- Logging to console/file via logger; stats renderer updated on a timer.

## 16. Quality Gates
- Build: `dotnet restore && dotnet build`.
- Test: `dotnet test` (mock network where possible).
- Smoke: `run --duration 00:00:10` creates CSV/JSONL and shows periodic summaries.

## 17. Milestones
- M0: Project scaffolding, config loader, logging, DI skeleton, CSV/JSONL sinks.
- M1: Ping/DNS/HTTP probes + NetIf/Route collectors + basic rules; CLI `run` + periodic summaries.
- M2: `diagnose` command + capability discovery/self-checks; privilege/distro hints.
- M3: Stabilization: rotation policy, jitter handling, richer Wi-Fi info; optional notify.

## 18. Open Questions
- Which tqdm-like .NET library is acceptable? If none, proceed with in-house renderer.
- Preferred color theme/palette for CLI (we can prototype and iterate).
- Minimum viable distro-detection granularity for install hints (os-release parsing?).

## 19. Suggested Enhancements
- Add a `stats` command that reads CSV/JSONL to print summaries (no monitoring).
- Add a Markdown report generator for `diagnose`.
- Add opt-in `--bind` for CLI to expose a minimal read-only HTTP status endpoint later.
