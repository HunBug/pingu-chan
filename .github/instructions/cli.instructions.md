---
applyTo: "src/PinguChan.Cli/**/*.cs"
---

# Instructions for PinguChan.Cli

- Purpose: System.CommandLine-based CLI for monitoring and diagnostics.
- Commands (from DESIGN.md):
	- `run` — start monitoring (foreground); options: `--config`, `--duration`, `--once`, `--export`, `--quiet`.
	- `diagnose` — quick tests and summary (markdown output supported).
	- `stats` — rolling stats from local files or SQLite; option `--window`.
	- `report` — generate markdown report.
	- `version` — print version/build info.
- UX:
	- Human- and CI-friendly output; periodic console summaries (~60s) as specified in DESIGN.md.
	- Non-zero exit codes for failure conditions (invalid config, probe init errors).
- Config:
	- Load YAML (`pingu.yaml`) from cwd by default; allow override via `--config`.
	- Respect sinks and notify settings; default to CSV/JSONL if not specified.
- Logging:
	- Serilog console logging; avoid leaking sensitive values; support `--quiet`.
- Safety:
	- Do not start remote listeners by default; remote features are opt-in via config.
- Tests:
	- Add CLI integration tests where feasible (parse/handler wiring, dry-run).
