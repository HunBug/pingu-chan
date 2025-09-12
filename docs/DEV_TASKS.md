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
 - [x] Orchestration: Expose IAsyncEnumerable<RuleFinding>
- [x] CLI: Subscribe to findings stream for WARNs (keep current alerts until migration completes)
- [x] Tests: CSV/JSONL write of RuleFinding; simple “finding passes through” smoke (sink tests added)

### M2 — Target pools & scheduler
 - [x] Service: ITargetPools with weighted rotation, min_interval, jitter_pct, failure backoff, per-target concurrency=1
 - [x] Scheduler: Ask pool for next, run probe, report result; include target key in NetSample.Extra
- [x] Etiquette: enforce global floors; set HTTP User-Agent from config
 - [x] Observability: periodic pool diagnostics (snapshot API exists; periodic emission pending)
  - Note: Added `--dump-pools` CLI flag to print a diagnostics snapshot on demand.
- [x] Tests: fairness, min interval respect, backoff growth/decay, jitter bounds (basic rotation/backoff + min_interval/jitter bounds added)

### M3 — Rolling stats service
- [x] Port CLI StatsAggregator into IStatsService with windows (e.g., 1m/5m) (minimal impl)
- [x] API: Query per kind/target aggregates (ok count, fail%, p50/p95)
- [x] Tests: stats windows, edge cases (empty windows, single-sample) (basic window test added + per-target loss tracking across two hosts)

### M4 — Rules engine (noise reduction)
 - [x] Implement ConsecutiveFail rule (per-target streak)
 - [x] Implement Quorum rule (windowed multi-category gating)
 - [x] Emit RuleFindings with context (streak/quorum flags)
- [x] CLI: remove renderer-side thresholds; print findings only
- [x] Tests: rule triggers, gating behavior, suppression when below thresholds

### M5 — Trigger engine
- [ ] Implement ITriggerEngine (conditions over stats/samples); debounce + cooldown
- [ ] Actions: mtu_sweep, snapshot(arp_dhcp), next_hop_refresh, wifi_link
- [ ] Emit finding on trigger fire; action results are normal samples
- [ ] Tests: debounce/cooldown; action concurrency guard; simple scenario flows

### M6 — Config unification & validation
 - [x] Extend config with pools/scheduler/rules (triggers later; defaults preserve current behavior)
- [x] Validator: floors, dedupe/normalize targets, deny/allow lists (deny/allow TBD)
- [x] Tests: parsing variants, defaulting, validation errors

### M7 — Smoke & stabilization
- [x] End-to-end: run with pools; verify rotation/backoff; findings appear instead of UI alerts (smoke test added)
- [x] Perf: bounded channels/back-pressure behavior under load (channel tests added)
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
  - [x] TargetPools: rotation, min_interval, backoff growth/decay, jitter bounds
  - [x] StatsService: per-kind/target windows, fail% calculation (p50/p95 pending)
  - [ ] Rules: ConsecutiveFail/Quorum (partial: Quorum covered)
  - [ ] Triggers: debounce/cooldown firing
  - [ ] Sinks: write/read of NetSample and RuleFinding
- [ ] Smoke tests
  - [ ] CLI run with 2+ ping targets in pools; verify both generate samples and loss is tracked per target
  - [ ] Simulate transient failures (mock PingProbe) and assert findings emitted only with gating

## Next few days (prioritized from milestones)

Day 1 — M2 etiquette & observability
- [ ] Enforce global floors across kinds to cap aggregate rate (respect min floors regardless of per-target intervals)
- [ ] Set HTTP User-Agent from config (default descriptive UA)
- [ ] Periodic pool diagnostics emission (e.g., every 60s to logs), complementing `--dump-pools`

Day 2 — M3 stats API extensions
- [ ] Extend stats API to expose p50/p95 latencies per kind/target window
- [ ] Keep existing counts/ok/fail% and add unit tests for p50/p95 edge cases

Day 3 — M4 rules test coverage
- [ ] Add rule trigger tests (ConsecutiveFail and Quorum) including below-threshold suppression and window boundaries

Day 4 — M6 validation and M7 smoke
- [ ] Config validator: floors, dedupe/normalize targets, deny/allow lists (lightweight)
- [ ] Tests for parsing variants, defaults, and validation errors
- [ ] E2E smoke: run with pools and verify rotation/backoff; findings replace UI alerts in logs

Day 5 — M7 perf/back-pressure and docs
- [ ] Validate bounded channels/back-pressure under load (synthetic burst); adjust if needed
- [ ] Finalize design/implementation notes; plan to remove this file after stabilization

## Notes
- Destination pools must rotate; on error streaks, re-check current target (backoff-aware) and also probe others to localize scope.
- Keep behavior deterministic in tests by fixing random seed for weighted selection where needed.
