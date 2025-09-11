# Destination Pools (Curated Test Targets)

This catalog provides example targets to use for rotation. Use them responsibly: keep low rates, add jitter, and prefer your own or local infrastructure.

## Principles
- Prefer local/first-party targets (gateway, ISP DNS, your own websites/APIs).
- Rotate across categories; avoid concentrating load on one company.
- Add jitter and per-target rate limits; back off on sustained failures.
- Respect Terms of Service. Remove any target if asked.

## Categories and Examples

### 1) Gateway & Local Infrastructure
- Special token: `gateway` (default route IP)
- Router UI page (HTTP HEAD): e.g., `http://192.168.1.1/`
- Local DNS resolver IP(s): from OS config

### 2) Anycast Public Resolvers (Ping)
- `1.1.1.1` (Cloudflare)
- `8.8.8.8` (Google Public DNS)
- `9.9.9.9` (Quad9)
- `208.67.222.222` (OpenDNS)

### 3) Anycast Public Resolvers (DNS)
- Resolvers: `1.1.1.1`, `8.8.8.8`, `9.9.9.9`, `208.67.222.222`
- Domains to resolve: `github.com`, `wikipedia.org`, `example.com`

### 4) Lightweight HTTP Endpoints
- `https://www.google.com/generate_204` (tiny 204)
- `https://1.1.1.1/` (Cloudflare welcome)
- `https://www.cloudflare.com/cdn-cgi/trace` (text; higher cost)
- `https://httpbin.org/status/204` (tiny 204)
- `https://example.com/` (small HTML)

### 5) CDN/Edge Sites (varied paths)
- `https://cdn.jsdelivr.net/` (static CDN)
- `https://raw.githubusercontent.com/` (GitHub raw)
- `https://aka.ms/` (Microsoft shortener)

Note: Prefer HEAD or fetch minimal resources where possible.

## Rotation Strategy
- Build pools per probe kind with weights and per-target min interval.
- Randomize target order each cycle; avoid repeated hits to the same host.
- Apply jitter (±10–30%) and per-target exponential backoff on failures.
- Limit concurrent requests to the same domain/IP.

## Example YAML (Proposed)
```yaml
pools:
  ping:
    - host: gateway
      weight: 3
      min_interval: 5s
    - host: 1.1.1.1
      weight: 2
      min_interval: 10s
    - host: 8.8.8.8
      weight: 2
      min_interval: 10s
    - host: 9.9.9.9
      weight: 1
      min_interval: 15s
  dns:
    resolvers: [1.1.1.1, 8.8.8.8]
    domains:
      - name: github.com
        weight: 2
      - name: wikipedia.org
        weight: 1
  http:
    - url: https://www.google.com/generate_204
      method: HEAD
      weight: 3
      min_interval: 1m
    - url: https://httpbin.org/status/204
      method: HEAD
      weight: 1
      min_interval: 2m
    - url: https://example.com/
      method: HEAD
      weight: 1
      min_interval: 5m
scheduler:
  jitter_pct: 0.2
  failure_backoff: { base: 2.0, max_multiplier: 8 }
```

## Legal & Contact
If you control a listed endpoint and prefer not to be included, open an issue and we’ll remove it from the examples.
