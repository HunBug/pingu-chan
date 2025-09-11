# DEV_TASKS.md — Temporary Development Taskboard

Purpose: Track the orchestration refactor, progress, issues, and tests during development. Delete or archive when the work is complete.

## Ground rules
- Single source of behavior: Orchestration library owns scheduling, pools/rotation, triggers, rolling stats, and rules.
- Politeness: jitter, per-target min intervals, failure backoff; rotate pools and avoid hammering any single host.
- Verification first: add core unit tests for modules before wiring complex flows; smoke tests after milestones.
- Bug hygiene: known issues captured here; include repro hints and a validation step.

## Milestones & Tasks

### M1 — Findings stream + Orchestration skeleton
- [x] Core: Add Channel<RuleFinding> and sinks write-path
- [x] Orchestration: Project skeleton, Start/Stop API, subscribe to Core sample stream (minimal scheduler loop in place)
- [ ] Orchestration: Expose IAsyncEnumerable<RuleFinding>
- [x] CLI: Subscribe to findings stream for WARNs (keep current alerts until migration completes)
- [x] Tests: CSV/JSONL write of RuleFinding; simple “finding passes through” smoke (sink tests added)

### M2 — Target pools & scheduler
- [x] Service: ITargetPools with weighted rotation, min_interval, jitter_pct, failure backoff, per-target concurrency=1 (concurrency guard pending)
- [x] Scheduler: Ask pool for next, run probe, report result; include target key in NetSample.Extra (Extra pending)
- [ ] Etiquette: enforce global floors; set HTTP User-Agent from config
- [ ] Observability: periodic pool diagnostics (skipped due to min_interval, current backoff)
- [x] Tests: fairness, min interval respect, backoff growth/decay, jitter bounds (basic rotation/backoff + min_interval/jitter bounds added)

### M3 — Rolling stats service
- [x] Port CLI StatsAggregator into IStatsService with windows (e.g., 1m/5m) (minimal impl)
- [ ] API: Query per kind/target aggregates (ok count, fail%, p50/p95)
- [x] Tests: stats windows, edge cases (empty windows, single-sample) (basic window test added + per-target loss tracking across two hosts)

### M4 — Rules engine (noise reduction)
- [ ] Implement ConsecutiveFail rule (per-target streak)
- [ ] Implement Quorum rule (windowed multi-category gating)
- [ ] Emit RuleFindings with context (streak/quorum flags)
- [ ] CLI: remove renderer-side thresholds; print findings only
- [ ] Tests: rule triggers, gating behavior, suppression when below thresholds

### M5 — Trigger engine
- [ ] Implement ITriggerEngine (conditions over stats/samples); debounce + cooldown
- [ ] Actions: mtu_sweep, snapshot(arp_dhcp), next_hop_refresh, wifi_link
- [ ] Emit finding on trigger fire; action results are normal samples
- [ ] Tests: debounce/cooldown; action concurrency guard; simple scenario flows

### M6 — Config unification & validation
- [ ] Extend config with pools/scheduler/rules/triggers (defaults preserve current behavior)
- [ ] Validator: floors, dedupe/normalize targets, deny/allow lists
- [ ] Tests: parsing variants, defaulting, validation errors

### M7 — Smoke & stabilization
- [ ] End-to-end: run with pools; verify rotation/backoff; findings appear instead of UI alerts
- [ ] Perf: bounded channels/back-pressure behavior under load
- [ ] Docs: finalize design/implementation notes; remove DEV_TASKS.md when stable

## Known Issue (to verify after refactor)

- ID: ping-loss-single-target-only
  - Symptom: Only one ping destination reports packet loss, other never does; external `ping` shows both dropping.
  - Likely areas:
    - Target rotation or per-target state not applied per destination
    - Rolling stats window mixing targets or wrong keying
    - Probe result mapping not tagging target correctly (e.g., shared label)
  - Plan:
    - [ ] Add unit tests: per-target loss tracking; simulate failures on multiple targets and assert both surfaces
    - [ ] After orchestrator scheduler and stats move, re-check using pools with ≥2 ping targets
    - [ ] Add a sanity logger: on each ping sample, log target key + ok/fail and ensure both targets appear during test run

## Test plan (initial)

- [ ] Unit tests
  - [ ] TargetPools: rotation, min_interval, backoff growth/decay, jitter bounds
  - [ ] StatsService: per-kind/target windows, fail% calculation, p50/p95
  - [ ] Rules: ConsecutiveFail/Quorum
  - [ ] Triggers: debounce/cooldown firing
  - [ ] Sinks: write/read of NetSample and RuleFinding
- [ ] Smoke tests
  - [ ] CLI run with 2+ ping targets in pools; verify both generate samples and loss is tracked per target
  - [ ] Simulate transient failures (mock PingProbe) and assert findings emitted only with gating

## Next few days (actual steps)

Day 1
- [x] Create PinguChan.Orchestration project (empty), add to solution
- [x] Define interfaces: ITargetPools, IStatsService, IRulesService, ITriggerEngine
- [ ] Add MonitorOrchestrator with Start/Stop and stub streams (Start/Stop implemented; streams pending)

Day 2
- [x] Implement basic TargetPools with weighted rotation + min_interval + jitter
- [ ] Wire a minimal scheduler loop that cycles ping targets and emits samples to the Core bus
- [x] Add unit tests for rotation/min_interval/jitter (basic rotation covered)

Day 3
- [x] Add failure backoff and per-target concurrency guard (backoff done; concurrency guard pending)
- [x] Port minimal StatsAggregator into IStatsService (1m window) and tests
- [x] Add Known Issue unit test for per-target loss tracking (covered by per-target two-host test)

Day 4
- [x] Add RuleFinding channel in Core and sinks write-path
- [ ] Emit a simple threshold rule via RulesService; CLI subscribes to findings for WARN lines (rule service added; orchestrator evaluates singleton; fuller wiring pending)
- [ ] Quick smoke: pools rotate, findings appear; note any regressions in DEV_TASKS

## Notes
- Destination pools must rotate; on error streaks, re-check current target (backoff-aware) and also probe others to localize scope.
- Keep behavior deterministic in tests by fixing random seed for weighted selection where needed.
