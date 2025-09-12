# DEV_TASKS.v2 — Feedback-driven plan (final)

Purpose: translate the feedback into actionable milestones while factoring in what we’ve already built (UTC timestamps, JSONL sink, triggers/rules, filters, etiquette).

What’s already covered (do not redo):
- JSONL sink exists; we emit UTC timestamps in ISO-8601.
- Trigger engine and rules (streak + quorum) exist with debounce/cooldown and action stubs.
- Per-sink log levels + `--verbosity` for console; allow/deny target filters.

## Global principles
- [ ] Single log stream: `netwatch.jsonl`, one JSON object per line (CSV remains optional; output is already configurable).
- [ ] Uniform envelope: `ts` (UTC ISO-8601), `host`, `event`, `level`, `extra{}`.
- [ ] Attach Wi‑Fi link context when on WLAN: `ssid,bssid,band,channel,rssiDbm,txRateMbps` (via a cached provider; decorate samples from cache).
- [ ] Maintain light in‑memory state: rolling windows, streaks, and a “current incident”.
- [ ] Ethics: be polite to third parties (jitter/backoff/floors); collect freely on local system/network within user consent.

Defaults & privacy
- Safe by default: collect only non-sensitive fields unless explicitly enabled.
- Detailed collection allowed when config or CLI flag enables it (for private diagnostics). Redact where reasonable.

Acceptance
- Every record conforms to the envelope; unknowns omitted or null (no literal "unknown").
- One JSONL stream contains samples/findings/snapshots.
- When on WLAN, samples include `extra.net{...}`.

## M1 — Startup baseline (once; on resume/iface change)
- [ ] At the beginning of every run, collect a comprehensive baseline sequentially (to avoid spikes) and emit `event:"startup"` with structured sections: `system,power,wifi,network`.
- [ ] Respect safe vs detailed mode: always collect core fields; include sensitive/deep fields only when allowed by config/CLI.
- [ ] Detect resume from suspend and primary interface change; re‑emit the baseline snapshot then.

Linux-first scope (best‑effort Windows/macOS):
- System: kernel/distro, CPU model, RAM MB.
- Power: AC online, battery %, Wi‑Fi power save.
- Wi‑Fi: driver/firmware hints, regdomain.
- Network: ifaces, default route/gateway, DNS config.

Acceptance: structured fields (arrays/objects), minimal raw blobs; baseline always present at start of run.

## M2 — Periodic probes (steady loop)
- [ ] ICMP: gateway + public + nextHop on configurable intervals (etiquette-friendly defaults; jitter/backoff) → ok, ms (+ Wi‑Fi context).
- [ ] DNS: A/AAAA to public resolvers every 5s → resolver, domain, ok, ms, answerIPs.
- [ ] HTTP: 204 endpoints every 10s → status, ttfbMs, totalMs.
- [ ] Env telemetry every 15–30s → cpu%, mem used/total, AC/battery, wifiPowerSave.

Acceptance: `event:"sample"`, `extra.kind` ∈ {ping,dns,http,env}; etiquette honored by scheduler.

## M3 — Background discoveries
- [ ] Next‑hop discovery (ISP edge): run immediately at startup (so data is available without waiting), then hourly, and on route change (TTL‑capped tracepath/traceroute).
- [ ] Wi‑Fi link context cache: refresh small stats on a short cadence; attach cached values under `extra.net{}` to every sample while on WLAN.

Acceptance: emit `event:"snapshot"`, `topic:"nextHop"`; include discovered IP. Probe nextHop transiently (outside rotating pools) until we add an explicit ephemeral-pool feature.

## M4 — Wi‑Fi link context on samples
- [ ] Linux provider using `iw` + `/proc/net/wireless` (map freq→band/channel).
- [ ] Windows/macOS: best‑effort stubs; guard with one‑time WARN.

Acceptance: on WLAN, include `iface,ssid,bssid,band,channel,rssiDbm,txRateMbps`; otherwise omit `extra.net`.

## M5 — Rules, findings, incidents
- [ ] Embed streak/quorum context and path state (gateway/nextHop) in findings.
- [ ] Layer localization decision tree → class in `message` (LAN/Wi‑Fi/AP vs ISP edge vs remote/app).
- [ ] Incident lifecycle (optional): open/update/close with summary.

Acceptance: `event:"finding"` includes `ruleId,severity,message,context{streakMax,window{..},path{gatewayOk,nextHop,nextHopOk},targets[]}`; incidents carry `incidentId` and summary on close.

## M6 — Conditional diagnostics (on rule fire)
- [ ] Gateway/nextHop fail → `snapshot: arp_dhcp` with parsed ARP + DHCP.
- [ ] HTTP‑only fail → `snapshot: mtu_sweep` (`mtuFound`, `blackholeSuspected`).
- [ ] DNS failures → `snapshot: dns_compare` (router vs public).
- [ ] Wi‑Fi roam/signal suspicion → `snapshot: wifi_link` (freeze details; detect BSSID change or ≥10 dB drop).

Acceptance: `event:"snapshot"`, `extra.topic` ∈ {arp_dhcp,mtu_sweep,dns_compare,wifi_link}; `extra.data{}` is structured.

## M7 — Housekeeping
- [ ] Re‑discover nextHop hourly and on route change (first run already covered at startup).
- [ ] Daily optional snapshots: regdomain, radio wifi/rfkill, ethtool stats (Ethernet). Also captured at startup if enabled.

## M8 — Schema hardening
- [ ] Finalize envelope/field names; document in `docs/METADATA.md`.
- [ ] Omit unknowns or use nulls; avoid free‑text blobs.
- [ ] Add minimal schema shape tests.

## (Removed) Sink unification / UX
Output selection is already configurable; no further action needed. JSONL remains the source of truth, CSV optional.

## M10 — Platform guards & CI
- [ ] Guard non‑Linux features; WARN once per run on unsupported platforms.
- [ ] CI: Windows/Linux matrix build + tests; macOS best‑effort manual.

## M11 — Tests & E2E
- [ ] Unit: samples with Wi‑Fi context, findings with quorum/streak/path, snapshot parsers.
- [ ] Smoke: short run writes JSONL; contains startup, samples, and a finding when simulated.

Backlog (optional)
- [ ] Glob support in filters (e.g., `*.google.com`).
- [ ] Enrich action payloads (OS hints/iface names/gateway vendor).
- [ ] Optional SQLite export for offline analysis.
