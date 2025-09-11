Moved to archive. See `docs/archive/IMPLEMENTATION_PLAN_NEXT.md`.

## Objectives
- Reduce false alarms via consecutive/quorum gating.
- Add localization via next-hop health and Wi‑Fi context.
- Trigger MTU sweep on symptom; add IPv6 split checks.
- Burst snapshots and environment metrics.
- Optional unified JSONL mode.

## Phases

### Phase A — Noise reduction
- Add consecutive-fail gating for warnings (per-target streak).
- Add quorum gating across categories in a short window.
- Include streak/quorum annotations in rolling summaries and findings.

### Phase B — Localization & context
- Maintain next-hop identity from traceroute and monitor it.
- Decorate samples with Wi‑Fi context when on WLAN.

### Phase C — Targeted diagnostics
- Conditional MTU sweep when HTTP fails and ICMP is OK; record result as a finding.
- Add IPv6 ping and AAAA DNS checks; present split health.

### Phase D — Snapshots & environment
- Emit compact ARP/DHCP snapshots on burst events.
- Periodic environment samples (cpu/mem/battery/power) and one startup snapshot.

### Phase E — Unified log mode (optional)
### Phase F — Trigger-based measurements
- Add a lightweight TriggerEngine in the CLI that evaluates configured trigger conditions over recent samples/stats and schedules actions.
- Initial actions: mtu_sweep, snapshot(arp_dhcp), next_hop_refresh, wifi_link.
- Respect debounce and cooldown; log firings and write findings with minimal context.
- Provide a single JSONL output mode consolidating samples/findings/snapshots/startup under a common envelope.

## Risks & Mitigations
- Over-suppression: tune thresholds conservatively; provide config overrides.
- Context accuracy: treat Wi‑Fi/next-hop context as best-effort; degrade gracefully.
- Log volume: keep snapshots conditional; support rotation.

## Acceptance Criteria
- WARNs gated by streak and optionally quorum; annotations present.
- Next-hop health available and influences findings.
- Conditional MTU sweep recorded.
- IPv6 split checks visible in summaries.
- Burst snapshots captured; environment samples present.
- Unified mode available and documented (optional).
