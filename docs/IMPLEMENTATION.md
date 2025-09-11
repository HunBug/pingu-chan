# IMPLEMENTATION.md — Concise Guide

This single doc replaces scattered planning files. It captures what’s done, the basic guidelines, and the high‑level plan to implement the new architecture and features.

## What’s implemented (snapshot)
- Core models: `NetSample`, `RuleFinding` and basic sinks (CSV, JSONL).
- Probes/collectors (initial set): Ping, DNS, HTTP, Gateway, MTU, NetIf, Routes, Firewall, Traceroute, Internet heuristic.
- CLI live summary with WARN markers when windows cross thresholds; WARNs persisted as `RuleFinding`.
- Config: YAML/JSON; `sinks.appendTimestamp` adds per-run timestamps to filenames.
- Responsible-use etiquette and curated destination pools catalog.

## Basic guidelines
- Separation: keep Core minimal; put orchestration into a separate `PinguChan.Orchestration` library.
- Cross‑frontend consistency: CLI/GUI/Remote must subscribe to the same Core/Orchestration streams and share the same config.
- Async & channels: prefer async/await, bounded `Channel<T>`, and back‑pressure.
- Politeness: jitter, rotation, per‑target min intervals, and failure backoff.
- Redaction & safety: do not log secrets; no listeners by default; opt‑in remote endpoints.
- Tests first for public behavior; small fast unit tests for rules/rotation/trigger gating.

## High‑level plan (phased)

1) Findings stream in Core
- Add `Channel<RuleFinding>` and write path to sinks.
- Adjust CLI to consume findings for WARN lines (remove UI‑side alert logic later).

2) Orchestration library skeleton
- New project: `PinguChan.Orchestration` owning runtime lifecycle (Start/Stop), scheduler, and services.
- Expose streams: `IAsyncEnumerable<NetSample>` (from Core bus) and `IAsyncEnumerable<RuleFinding>`.
- Provide service interfaces: `ITargetPools`, `IStatsService`, `IRulesService`, `ITriggerEngine`.

3) Destination pools & scheduler
- Weighted pools, per‑target `min_interval`, global jitter, per‑target exponential backoff, per‑host concurrency=1.
- Observability: periodic pool diagnostics and target keys in `NetSample.Extra`.

4) Rolling stats service
- Move/port CLI `StatsAggregator` into orchestration as `IStatsService` with windows (e.g., 1m/5m/1h).
- Provide basic aggregates per category/target; expose to rules/triggers.

5) Rules engine (noise reduction)
- Implement ConsecutiveFail and Quorum gates; emit `RuleFinding` with context.
- Migrate existing threshold WARNs into rules; CLI just prints findings.

6) Trigger engine
- Conditions over samples/stats; debounce + cooldown.
- Actions: `mtu_sweep`, `snapshot(arp_dhcp)`, `next_hop_refresh`, `wifi_link`.
- Emit findings when a trigger fires; action results are normal samples.

7) Config unification
- Extend config with `pools`, `scheduler`, `rules`, `triggers`, keeping backward compat and defaults sane.
- One parser/validator in Orchestration; frontends pass the same typed options.

8) Cleanup and consolidation
- Remove duplicated renderer‑side alert code; rely on findings stream.
- Trim docs to README + DESIGN + IMPLEMENTATION (+ curated pools); archive historical docs.

## Acceptance criteria
- Same config → same behavior across CLI/GUI/Remote.
- WARNs come from findings produced by rules; CLI does no custom alerting logic.
- Pools/rotation/jitter/backoff active; politeness safeguards enforced.
- Triggers fire with debounce/cooldown and produce samples; findings logged with reasons.
- Config extensions optional; if omitted, behavior matches current.

## Next steps (immediate)
- Create `PinguChan.Orchestration` project skeleton and wire to Core bus.
- Implement findings stream and migrate WARN path to it.
- Port StatsAggregator into `IStatsService` and add basic tests.

## References
- `DESIGN.md` for architecture overview.
- `docs/DESTINATION_POOLS.md` for curated targets and rotation guidance.
