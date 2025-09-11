# [Superseded] Core Refactor Plan — Unified Runtime & Config

Note: This plan has been consolidated into `IMPLEMENTATION.md`. This file remains for historical detail.

Goal: Ensure identical behavior across CLI, GUI, and Remote by moving orchestration (scheduling, triggers, pools, rules, and stats) into Core, with a stable public API and a single config schema. Frontends become thin hosts that wire IO (console/GUI/web) and optional extras.

## Why
- Reuse: Same configuration and decisions across all entry points.
- Consistency: One implementation of scheduling, triggers, rotation, and rules.
- Testability: Headless Core host with deterministic hooks.

## What moves into Core
- Runtime host: scheduler for probes/collectors, writer, and lifecycle.
- Target pools & rotation: weighted pools, min-intervals, jitter/backoff.
- Trigger engine: conditions over recent samples/stats and action scheduling.
- Rolling stats service: rolling windows for summaries and rules.
- Rules engine: consecutive/quorum, roam correlation, HTTP-but-ICMP-OK, etc.
- Event bus: publish/subscribe for samples and findings (already present for samples).
- Config parsing/validation for all of the above.

## Public API (Core)
- `MonitorRuntime` (or extend `MonitorHost`):
  - `StartAsync(CancellationToken)` / `StopAsync()` / `DisposeAsync()`
  - `Configure(PinguConfig cfg)` or ctor overload receiving typed options.
  - Observables:
    - `IAsyncEnumerable<NetSample>` (existing bus)
    - `IAsyncEnumerable<RuleFinding>` (new findings stream)
    - `IObservable<Summary>` (optional) for GUI/remote live views
- Factories:
  - `IProbeFactory`, `ICollectorFactory`, `IRuleFactory`, `IActionFactory` (for triggers)
  - Default built-ins provided; frontends can add/replace.
- Services (interfaces):
  - `ITargetPoolService`, `ITriggerEngine`, `IRulesService`, `IStatsService`.

## Config (single schema)
- Keep current `intervals`, `targets`, `sinks`.
- Add optional: `pools`, `scheduler`, `etiquette`, `triggers`, `rules`.
- Core owns parsing and validation; frontends pass the same config.

## Frontend responsibilities
- CLI: console UI, file/console sinks, process lifetime, OS prompts.
- GUI: visual rendering, user interactions, bind to Core observables.
- Remote: HTTP/WebSocket endpoints mapping to Core streams and controls.

## Threading & Lifetime
- Core owns all threads/tasks for probes/collectors/triggers/rules.
- Frontends must not schedule probes directly; they subscribe to streams and call Start/Stop.
- Controlled back-pressure via bounded channels; configurable policy (drop oldest vs block).

## Migration Steps
1) Findings stream: add `Channel<RuleFinding>` and writer in Core; expose to sinks.
2) Move rolling `StatsAggregator` into Core as `IStatsService` and feed from bus.
3) Target pools/jitter/backoff: implement in Core, used by probe scheduler.
4) Trigger engine: move from CLI concept to Core service; wire configured triggers and actions.
5) Rules engine: add minimal rules (consecutive/quorum); emit findings.
6) Config: extend `PinguConfig` to include pools/scheduler/triggers/rules (back-compat preserved).
7) CLI refactor: remove renderer-side alerts; subscribe to `RuleFinding` for WARN lines; UI only.
8) Tests: unit tests for pools/trigger/rules and end-to-end smoke on Core host.

## Acceptance Criteria
- Same config yields same monitoring/diagnostics behavior in CLI, GUI, and Remote.
- CLI emits WARNs by consuming Core findings; no duplicated logic.
- CSV/JSONL receive both samples and findings from Core.
- Feature flags (pools/triggers/rules) optional; absent config behaves like today.

## Risks & Mitigations
- API churn: define minimal, stable interfaces; mark experimental features.
- Complexity creep: keep Core dependencies minimal; keep GUI/Remote optional.
- Back-compat: maintain current config fields; validate extensions with defaults.
