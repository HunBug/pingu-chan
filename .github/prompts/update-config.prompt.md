# Update Configuration

Goal: make safe, verifiable changes to `pingu.yaml`.

Steps
1) Edit intervals/targets/rules/sinks/notify, mirroring the structure in DESIGN.md.
2) Keep durations reasonable (e.g., `2s`, `5s`, `10s`, `30s`) to avoid CPU spikes.
3) Validate with a short run: `pingu run --config pingu.yaml --duration 00:00:10` and confirm CSV/JSONL output.
4) Document new keys or defaults in README; avoid breaking schema.
5) Redact sensitive values (tokens/webhooks) in logs and examples.

References
- Configuration section: [DESIGN.md](../../DESIGN.md)
