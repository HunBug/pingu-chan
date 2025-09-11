# [Superseded] Trigger-based Measurements — Design

Note: See `IMPLEMENTATION.md` for the current high-level plan and guidelines. This document is retained for additional design details.

Goal: Run selective measurements only when symptoms appear (e.g., MTU sweep when HTTP fails but ICMP OK), without changing current output formats. Keep today’s probes/collectors; add a small trigger engine that listens to rolling stats and recent samples.

## Concepts
- Trigger: a condition that evaluates recent events/stats and, when true, schedules an action.
- Action: a one-shot diagnostic (probe/collector) or snapshot executed immediately or with a debounce.
- Gating & Cooldown: prevent flapping and storms using consecutive-fail, quorum, debounce window, and per-trigger cooldown.
- Context: pass relevant context (targets, last window stats) into action to enrich results.

## Minimal Integration (now)
- Implement a lightweight `TriggerEngine` inside the CLI that:
  - Subscribes to in-process events: per-sample (via ForwardingSink) and rolling stats (from `StatsAggregator`).
  - Evaluates trigger conditions every tick (1s) and fires actions.
  - Invokes existing components ad-hoc (e.g., call an `MtuProbe.ExecuteAsync()` once) and forwards resulting samples to the same sinks (CSV/JSONL/logs).
- No changes to output formats; actions emit normal `NetSample` lines, optionally with an `extra` JSON blob for context.

## Later Integration (Core)
- Refactor to a Core-level `EventBus` (already present) with a `TriggerEngine` that listens to samples and publishes action outputs.
- Keep CLI wiring simple; advanced users can configure triggers in `pingu.yaml`.

## Configuration (YAML/JSON)
Suggested schema addition (compatible with existing config):

```yaml
triggers:
  # Example: conditional MTU sweep when HTTP fails but ICMP is OK
  - id: mtu_on_http_fail
    when:
      all:
        - category: http
          predicate: fail_pct
          window: 10s
          gte: 10
        - category: ping
          predicate: ok
          window: 10s
          gte: 1
    gate:
      consecutiveFail: 2        # optional extra gating per-target/category
      quorum:
        minCategories: 1        # extra dimension if needed, defaults disabled
    debounce: 5s                # don’t fire more often than this
    cooldown: 5m                # minimum gap between firings
    action:
      type: mtu_sweep
      target: 8.8.8.8           # or infer from last failing HTTP target
      params:
        maxProbe: 1500
        minProbe: 1200

  # Example: ARP/DHCP snapshot on burst loss
  - id: snapshot_on_burst
    when:
      any:
        - category: ping
          predicate: loss_pct
          window: 60s
          gte: 10
    debounce: 10s
    cooldown: 10m
    action:
      type: snapshot
      topic: arp_dhcp

  # Example: next-hop discovery refresh when gateway ok but public loss
  - id: next_hop_refresh
    when:
      all:
        - category: gateway
          predicate: ok
          window: 10s
          gte: 1
        - category: ping
          predicate: loss_pct
          window: 30s
          gte: 5
    debounce: 30s
    cooldown: 1h
    action:
      type: next_hop_refresh
```

Notes:
- `when`: a small boolean algebra (all/any) over category metrics (`ok`, `fail_pct`, `loss_pct`) in a window.
- `gate`: optional consecutive/quorum gates to suppress noise.
- `debounce`: per-trigger min interval to avoid rapid repeats.
- `cooldown`: longer-term limiter; both can be used.
- `action.type`: an enum of built-ins (see below). Unknown types ignored.

## Built-in Actions (initial set)
- `mtu_sweep`: run the existing MTU probe once for a specific target and emit result.
- `snapshot` (topic: `arp_dhcp`): emit compact network neighbor/lease data via existing collectors.
- `next_hop_refresh`: refresh cached next-hop identity (used by separate periodic probing).
- `wifi_link`: force a Wi‑Fi link context refresh and attach to following samples for a short period.

## Execution Model
- Trigger evaluation cadence: 1 Hz aligned with the renderer/status update.
- Action scheduling: fire-and-forget tasks with a bounded concurrency per action type (e.g., 1 for mtu_sweep).
- Safety: drop action if system is stopping; respect cancellation.

## Observability
- Log when a trigger fires with id and reason (window metrics) and write a corresponding finding to sinks.
- Include minimal context in the action’s `extra` JSON (e.g., `{ "trigger":"mtu_on_http_fail" }`).

## Acceptance Criteria
- Triggers can be defined in config; absent triggers → current behavior unchanged.
- On configured conditions, actions run and produce normal samples.
- Debounce/cooldown honored; no action storms.
- No new file formats; existing sinks receive all outputs.
