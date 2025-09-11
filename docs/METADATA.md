# Metadata schema

This document defines the typed metadata embedded in `NetSample.Extra` and the common keys in `RuleFinding.Context`.

## NetSample.Extra (JSON, typed via SampleMeta)

- pool: string — logical pool name (e.g., "ping", "dns", "http")
- key: string — scheduler key (e.g., target host or URL identifier)
- tags: object — optional free-form map for probe-specific hints (resolver, iface, etc.)

Example:
```json
{"pool":"ping","key":"1.1.1.1"}
```

See `PinguChan.Core.Models.SampleMeta` and `SampleMetaCodec` for serialization.

## RuleFinding.Context (JSON object)

Common keys:
- target: string — human-readable target value (e.g., host or URL)
- pool: string — logical pool that produced the samples
- key: string — scheduler key (should match SampleMeta.key)
- streak: number — consecutive fail count (for ConsecutiveFail)

Additional keys are rule-specific and may be added over time.
