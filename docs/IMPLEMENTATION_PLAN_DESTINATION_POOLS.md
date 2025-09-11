# Implementation Plan: Destination Pools, Rotation, and Etiquette

Scope: Documentation and planning only (no code changes in this milestone). Defines config schema, scheduler behavior, and validation needed to support polite monitoring at scale with many test destinations and on-the-fly rotation.

## Goals
- Provide large pools of test destinations per probe kind (ping/dns/http).
- Rotate targets fairly with weights, per-target minimum intervals, and jitter.
- Apply per-target failure backoff to reduce load during outages.
- Allow hot reload of pools from config without restart.
- Enforce etiquette constraints (interval floors, traceroute limits, UA string).

## Assumptions
- Current CLI and Core already support reading YAML and running probes on intervals.
- We’ll extend the config but keep backward compatibility with existing `targets`.
- Rotation and backoff will be handled in the scheduling layer above probes.

## High-level Design
- New config sections: `pools`, `scheduler`, `etiquette` (see DESIGN.md for schema).
- A `TargetPool` component per probe kind encapsulates selection and state:
  - Maintains list of `PoolEntry` with fields: key, weight, minInterval, lastRunAt, backoffMultiplier.
  - Methods: `TryGetNext(now) -> (entry|null, waitFor)` and `ReportResult(entry, ok)`.
- Scheduler loop asks pool for next entry, waits `waitFor` if needed, applies jitter, executes probe with that target, then reports result.

## Phased Milestones

### Phase 1 — Config & Validation (docs-first)
- Extend `docs/DESTINATION_POOLS.md` with curated lists (done).
- Add schema and behavior to `DESIGN.md` (done).
- Plan YAML parsing and validation rules:
  - Back-compat: if `pools` absent, fall back to `targets` as today.
  - Validate min interval floors and normalize durations.
  - Deduplicate/normalize hosts/URLs.

### Phase 2 — Scheduler Scaffold (planning)
- Introduce `ITargetPool` interface and a simple in-memory implementation.
- Weighted random selection with reservoir shuffle per window.
- Enforce per-target `min_interval` and `per_target_concurrency`.
- Apply jitter ±`jitter_pct`.
- Failure backoff: per-target exponential with cap, decay after sustained success.

### Phase 3 — Hot Reload (planning)
- File watcher for `pingu.yaml`; debounce changes (e.g., 300ms).
- Rebuild pools atomically; migrate state (lastRunAt, backoff) by key.
- Emit an info log with diff summary (added/removed/changed entries).

### Phase 4 — Etiquette Enforcement (planning)
- Global floors: ping ≥ 1s; DNS ≥ 2s; HTTP ≥ 30s; traceroute ≥ 15m.
- User-Agent override for HTTP from `etiquette.http_user_agent`.
- Deny/allow lists for hosts/domains.

### Phase 5 — Observability & Tests (planning)
- Expose pool stats via logs and `extra` fields in samples.
- Unit tests: selection fairness, minInterval respect, backoff behavior, jitter bounds, hot reload state carry-over.
- Smoke tests: run with sample pool config and verify rotation and rate limits via timestamps.

## Acceptance Criteria
- Backward compatibility: existing configs continue to work.
- With a pool defined, the scheduler rotates targets and enforces min intervals.
- Failures trigger backoff; repeated successes decay toward baseline.
- Etiquette floors cannot be undercut by misconfig.
- Docs updated; examples available; tests cover core behaviors.

## Risks & Mitigations
- Risk: Overly aggressive defaults → throttle with conservative floors and jitter.
- Risk: Hot reload churn → debounce and atomic swap; preserve per-target state.
- Risk: Target ownership ethics → provide docs, disclaimers, and easy deny-listing.

## Out of Scope (this milestone)
- Code changes to probes/scheduler (documented only).
- GUI/Remote updates.
- New notifiers or storage backends.
