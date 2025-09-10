# Write a New Rule

Goal: implement a rule that evaluates a time window of `NetSample`s and emits an optional `RuleFinding`.

Steps
1) Create `YourRule` implementing `IRule` under `src/PinguChan.Core/Rules/YourRule.cs`.
2) In `Evaluate(window, now)`, filter relevant samples (kind/target/time range). Require a minimum count to avoid noise.
3) Compute threshold/variance/correlation as needed; return `null` if below threshold.
4) Add YAML entry in `pingu.yaml` (`rules:`) with `id`, `kind`, `window`, `threshold_*`, and `severity`.
5) Add unit tests in `tests/PinguChan.Tests/` for boundary conditions (just below/above threshold) and empty/short windows.
6) Document the rule’s purpose and tunables in README.

References
- Interfaces and examples: [DESIGN.md](../../DESIGN.md)
