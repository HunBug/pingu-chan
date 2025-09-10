# pingu-chan

Pingu-chan 🐧💢 is a cross-platform network diagnostics & monitoring toolkit (C#/.NET). It focuses on detecting and diagnosing issues (ping/DNS/HTTP), exporting simple data (CSV/JSONL), and providing actionable summaries. No packet payload capture; only metadata and timings.

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

Diagnostics mode (environment + quick one-shot probes):

```bash
dotnet run --project src/PinguChan.Cli -- diagnose
```

## CLI basics

- --config <path>  Path to YAML/JSON config (default: autodetect pingu.yaml/yml/json in CWD)
- --duration <ts>  Total run time (e.g., 10s, 1m, 00:00:30). Omit to run until Ctrl+C.
- --once           Run a single iteration of each probe and exit.
- diagnose         Print capability report + one-shot probe run.

During run, the CLI renders a 2-line live summary (loss/latency for ping; DNS failure rate and p95; HTTP TTFB p50/p95). It also writes results to configured sinks.

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
```

JSON config is also supported with the same schema.

## Outputs

- CSV: appended with header, suitable for spreadsheets and quick inspection.
- JSONL: one JSON object per line, easy to ingest into tools or databases.

Default file locations are relative to the executable’s directory. Use absolute paths if you want to write elsewhere.

## Notes

- Platforms: Windows, Linux, macOS (.NET 9). No listeners by default.
- Sensitive values (tokens/URLs) are not logged beyond hostnames and basic timings.
- See `DESIGN.md` for architecture and roadmap.
