---
applyTo: "tests/PinguChan.Tests/**/*.cs"
---

# Instructions for PinguChan.Tests

- Scope: unit/integration tests for probes, collectors, rules, config loader, and E2E smoke.
- Determinism:
	- Prefer fakes/mocks for network; bound timeouts; avoid external flakiness.
	- Rule tests should use synthetic windows and fixed timestamps.
- Coverage:
	- Config loader: valid/invalid cases; defaults and overrides.
	- Probes/collectors: success/failure/timeout; cancellation propagation.
	- Rules: threshold crossings, jitter/variance, correlation logic when added.
- E2E:
	- Short `run --duration 00:00:10` smoke; assert CSV/JSONL file creation and basic schema.
- CI:
	- Tests must pass cross-platform (Windows/Linux). Keep OS-specific assumptions guarded.
