# pingu-chan

Pingu-chan 🐧💢 is a cross-platform network diagnostics & monitoring toolkit (C#/.NET). It focuses on detecting and diagnosing issues (ping/DNS/HTTP), exporting simple data (CSV/JSONL/SQLite), and providing actionable rules and notifications.

Highlights
- Core diagnostics library with probes/collectors/rules and an in-process event bus.
- CLI for always-on monitoring and quick diagnosis.
- Optional remote access as a web service (HTTP/JSON) consumable by remote CLI or GUI, plus live WebSocket streaming.
- Optional small embedded web UI served by the remote service for quick remote access.

See `DESIGN.md` for architecture and roadmap.
