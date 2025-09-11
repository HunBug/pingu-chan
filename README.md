# pingu-chan

Pingu-chan 🐧💢 is a cross-platform network diagnostics & monitoring toolkit (C#/.NET). It focuses on detecting and diagnosing issues (ping/DNS/HTTP), exporting simple data (CSV/JSONL), and providing actionable summaries. No packet payload capture; only metadata and timings.

What’s new in direction: we’ll keep the Core small and add a separate orchestration library (runtime) to own scheduling, destination pools/rotation, triggers, rolling stats, and rules. All frontends (CLI/GUI/Remote) will share that runtime so behavior is consistent.

## Quick start

Build and run the CLI from source:

```bash
dotnet build -v minimal
dotnet run --project src/PinguChan.Cli -- --duration 00:00:10
```

Use a config file (recommended):

```bash
dotnet run --project src/PinguChan.Cli -- --config pingu.yaml --duration 00:01:00
```

Diagnostics mode (environment + quick one-shot probes & collectors):

```bash
dotnet run --project src/PinguChan.Cli -- diagnose
# optional: request sudo for extended diagnostics on Linux/macOS
dotnet run --project src/PinguChan.Cli -- --sudo diagnose
```

## CLI basics

- --config <path>  Path to YAML/JSON config (default: autodetect pingu.yaml/yml/json in CWD)
- --duration <ts>  Total run time (e.g., 10s, 1m, 00:00:30). Omit to run until Ctrl+C.
- --once           Run a single iteration of each probe and exit.
- diagnose         Print capability report + one-shot probes and collectors.
- --sudo           On Linux/macOS, try to cache sudo at startup (optional).

During run, the CLI renders a 2-line live summary (loss/latency for ping; DNS failure rate and p95; HTTP TTFB p50/p95). It also writes results to configured sinks. If `sinks.logs` is set, human-readable logs are also written to that file.

## Configuration (pingu.yaml)

- Create `pingu.yaml` in the working directory or pass `--config path/to/pingu.yaml`.
- Omitted fields use sane defaults. Durations accept `2s`, `500ms`, `1m`, or `hh:mm:ss`.

Example:

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
	# optional: write readable logs to a file too
	logs: pingu.log
	# if true (default), add _YYYYMMDD_HHMMSS to filenames so each run creates new files
	appendTimestamp: true
```

JSON config is also supported with the same schema.

## Outputs

- CSV: appended with header, suitable for spreadsheets and quick inspection.
- JSONL: one JSON object per line, easy to ingest into tools or databases.
- Logs (optional): human-readable lines (same as console), if `sinks.logs` is configured.
	- When rolling-window health crosses thresholds (e.g., ping loss >= 5% over 1m), WARN markers are printed and also saved to CSV/JSONL as RuleFinding records to help locate glitches by timestamp.

Default file locations are relative to the executable’s directory. Use absolute paths if you want to write elsewhere.

## Diagnostics collected

In diagnose mode, in addition to one-shot probes (Ping/DNS/HTTP), Pingu-chan collects:
- Network interfaces: name/type/status, MAC, IPs, gateway, DNS.
- Routes snapshot: default routes and routing table.
- Firewall state: ufw/firewalld (Linux), netsh (Windows), socketfilterfw (macOS).
- Traceroute: hops to 8.8.8.8 (trimmed in logs; full text in JSONL `extra`).
- Internet heuristic: checks for default gateway and DNS presence.
- Gateway probe: pings default gateway (if present) for latency/loss.
- MTU probe: best-effort path MTU estimate via DF pings (infrequent).

These are printed to console/logs and exported to sinks with a compact JSON payload in the `extra` field.

## Notes

- Platforms: Windows, Linux, macOS (.NET 9). No listeners by default.
- Sensitive values (tokens/URLs) are not logged beyond hostnames and basic timings.
- `--sudo` is optional; without it, privileged diagnostics simply report FAIL but normal monitoring continues.
- See `DESIGN.md` for architecture and `IMPLEMENTATION.md` for the concise implementation plan and guidelines.

## Responsible use & network etiquette

Pingu-chan is polite. Please be too.

- Prefer local/first-party destinations: your default gateway and configured DNS resolver are ideal for frequent checks.
- Keep rates low and add jitter: e.g., ping every 5–30s per target with ±20% jitter; DNS every 10–60s; HTTP every 1–5 minutes.
- Rotate public targets: don’t hammer a single company. Use a pool with weights and random rotation.
- Back off on failure: exponential backoff (e.g., 2x up to a cap) reduces burst load during outages.
- Use lightweight requests: HTTP HEAD or a tiny 204 endpoint; include a descriptive User-Agent.
- Run traceroute sparingly: e.g., once every 15–60 minutes; it’s noisy and can trip IDS.
- Respect ToS and policies: in corporate or shared networks, get permission and provide an allow/deny list.
- Redact and log responsibly: avoid secrets in hostnames/URLs; Pingu-chan logs only metadata and timings.

See `docs/DESTINATION_POOLS.md` for curated test destinations and rotation guidance.

## Implementation plan (concise)

We consolidated planning into a single short document. Read `IMPLEMENTATION.md` for:
- What’s already implemented at a glance
- Basic coding and product guidelines
- High-level steps to implement the new orchestration runtime, destination pools/rotation, triggers, and rules

This replaces scattered planning docs while keeping them available for historical context.

## Documentation index

- docs/DESIGN.md — Architecture reference. Describes the Core vs Orchestration split, key components, interfaces, and data flow. Benefit: shared mental model and stable contracts for contributors across CLI/GUI/Remote.
- docs/IMPLEMENTATION.md — Concise plan and guidelines. Summarizes what’s done, coding/product guidelines, and the phased plan for orchestration, pools/rotation, triggers, and rules. Benefit: aligns sequencing and acceptance criteria; speeds up reviews and onboarding.
- docs/DESTINATION_POOLS.md — Curated test destinations and rotation etiquette. Benefit: responsible usage, reproducible monitoring, and reduced load on third‑party services.
- docs/DEV_TASKS.md — Temporary development taskboard. Tracks refactor milestones, issues, and test coverage during implementation. Benefit: shared progress and priorities for the team; disposable after stabilization.

Archived planning docs live under docs/archive/ for historical context.
