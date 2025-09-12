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
 - [x] Observability: periodic pool diagnostics + `--dump-pools`; demoted per-run scheduler logs to debug
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
- [x] Implement ITriggerEngine (conditions over samples) with debounce + cooldown, per-action concurrency guard
- [x] Actions: mtu_sweep, snapshot(arp_dhcp), next_hop_refresh, wifi_link (stubs + platform guards)
- [x] Emit finding on trigger fire; action results are normal samples
- [x] Wire trigger engine into orchestrator tick and CLI; add orchestrator logs for armed/firing with cause details
- [x] Tests: action concurrency guard; simple scenario flows (debounce/cooldown covered)

### M6 — Config unification & validation
- [x] Extend config with pools/scheduler/rules; add sinks.consoleLogLevel and sinks.fileLogLevel (defaults: info)
- [x] Validator: floors, dedupe/normalize targets
- [x] Validator: deny/allow lists support
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
    - [x] Add unit tests: per-target loss tracking; simulate failures on multiple targets and assert both surfaces
    - [x] After orchestrator scheduler and stats move, re-check using pools with ≥2 ping targets
    - [x] Add a sanity logger: on each ping sample, log target key + ok/fail and ensure both targets appear during test run

## Test plan (initial)

- [ ] Unit tests
  - [x] TargetPools: rotation, min_interval, backoff growth/decay, jitter bounds
  - [x] StatsService: per-kind/target windows, fail% calculation (p50/p95 pending)
  - [x] Rules: ConsecutiveFail/Quorum
  - [x] Triggers: debounce/cooldown firing
  - [x] Sinks: write/read of NetSample and RuleFinding
- [ ] Smoke tests
  - [ ] CLI run with 2+ ping targets in pools; verify both generate samples and loss is tracked per target
  - [ ] Simulate transient failures (mock PingProbe) and assert findings emitted only with gating

## Next few days (remaining, prioritized)

1) Docs polish and completeness
- [x] CONFIG.md: brief section on triggers/actions (current built-ins and logs)
- [x] README: short note on triggers behavior and log visibility
- [ ] Finalize design/implementation notes; plan to retire or archive DEV_TASKS.md

2) CI
- [ ] GitHub Actions: Windows/Linux matrix build + dotnet test

3) Nice-to-haves
- [ ] Enrich action payloads (include OS hints, iface names, gateway) while keeping Core minimal
- [ ] Expand filters to support simple globs (e.g., "*.google.com") if needed (optional)

## Notes
- Destination pools must rotate; on error streaks, re-check current target (backoff-aware) and also probe others to localize scope.
- Keep behavior deterministic in tests by fixing random seed for weighted selection where needed.
