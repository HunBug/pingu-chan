---
applyTo: "src/PinguChan.Core/**/*.cs"
---

# Instructions for PinguChan.Core

- Purpose: core diagnostics library (probes, collectors, rules, event bus, sinks, notifiers, config).
- Contracts (from DESIGN.md):
	- `IProbe` → periodic `ExecuteAsync` returns one `NetSample`.
	- `ICollector` → periodic `CollectAsync` returns a set of `NetSample`s.
	- `IRule` → `Evaluate(window, now)` returns an optional `RuleFinding`.
	- `IResultSink`/`INotifier` → write/export results and notify on findings.
- Concurrency:
	- Prefer async/await; fan-out via `Channel<NetSample>`; cap concurrency to avoid CPU spikes.
	- Buffer sinks with back-pressure (bounded channels, drop oldest or block) as needed.
- Dependencies:
	- Keep Core minimal; optional packages behind interfaces (DnsClient, Open.NAT, Sqlite) live in adapters.
- Data formats:
	- CSV/JSONL by default; SQLite optional. Avoid breaking schema.
- Config:
	- YAML via YamlDotNet; validate intervals/targets; support special tokens like `gateway`.
- Safety & privacy:
	- No packet payload capture; metadata only. Redact tokens/URLs in logs.
- Performance:
	- Use configurable intervals; avoid tight loops; short timeouts with cancellation.
- Testing:
	- Unit tests for probes/collectors (timeout/error paths); rule tests with synthetic windows; config loader validation.
- Style:
	- C# 12 features (primary constructors/required members), XML docs on public APIs, .NET naming conventions.
- Reference: See `DESIGN.md` for models (`NetSample`, `RuleFinding`, enums) and examples.
