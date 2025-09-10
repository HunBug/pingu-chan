---
applyTo: "pingu.yaml"
---

# Instructions for pingu.yaml

- Purpose: YAML configuration for intervals, targets, rules, sinks, notifications.
- Schema (see DESIGN.md "Configuration"):
	- `intervals.*` (e.g., `2s`, `5s`, `10s`, `30s`).
	- `targets.ping` (IPs or `gateway`), `targets.dns` (resolvers/domains), `targets.http` (URLs).
	- `rules` with `id`, `kind`, `window`, `threshold_*`, `severity`.
	- `sinks` (csv/jsonl/sqlite paths) and `notify` options.
- Conventions:
	- Keep durations readable; avoid extreme intervals that cause CPU spikes.
	- Use resolvers/domains you control where possible to reduce test flakiness.
- Validation:
	- Run `pingu run --config pingu.yaml --duration 00:00:10` and verify outputs.
	- On errors, prefer explicit messages; do not start remote listeners by default.
- Security:
	- Redact tokens/webhook URLs in logs and reports.
