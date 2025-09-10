# Copilot Instructions for Pingu-chan 🐧💢

## Project Overview
Pingu-chan is a cross-platform network diagnostics and monitoring toolkit, designed as a tsundere penguin. It provides a core diagnostics library, a CLI for monitoring, optional GUI (Avalonia), and remote access (gRPC/WebSocket). The focus is on issue detection and diagnosis, not packet sniffing or network discovery. Data is exported in simple formats (CSV/JSONL/SQLite) for analysis and AI ingestion. The probe/check model is extensible for new features.

## Tech Stack & Architecture
- **Languages:** C# 12, .NET 8 LTS (upgradeable to .NET 9)
- **Platforms:** Windows, Linux, macOS
- **Packaging:** Single-file, self-contained binaries
- **Core Projects:**
  - `PinguChan.Core`: Diagnostics library (probes, collectors, rules, event bus, storage, notifications)
  - `PinguChan.Cli`: Command-line interface (System.CommandLine)
  - `PinguChan.Gui`: Avalonia UI (optional, later)
  - `PinguChan.Remote`: HTTP/JSON web service + WebSocket (optional, later) and an optional embedded web UI
  - `PinguChan.Tests`: Unit and integration tests
- **Config:** YAML/JSON (`pingu.yaml`)

## Repository Structure
- `/src/PinguChan.Core/`
- `/src/PinguChan.Cli/`
- `/src/PinguChan.Gui/` (optional)
- `/src/PinguChan.Remote/` (optional)
- `/tests/PinguChan.Tests/`
- `DESIGN.md`, `README.md`, `LICENSE`, `pingu.yaml`

## Key Libraries & Tools
- System.CommandLine (CLI)
- Serilog (logging)
- YamlDotNet (config)
- DnsClient (DNS, optional)
- Open.NAT (UPnP/NAT-PMP, optional)
- Microsoft.Data.Sqlite (optional)
- Grpc.AspNetCore / WebsocketListener (remote, optional)
- Avalonia (GUI, optional)

## Build, Test, and Validation
- **Build:**
  - Use .NET 8 SDK. Always run `dotnet restore` before `dotnet build`.
  - From repo root, prefer building the solution or all projects together.
- **Test:**
  - Run all tests from repo root: `dotnet test`.
  - Aim for fast, deterministic tests; mock network when practical.
- **Run:**
  - CLI (when available): `dotnet run --project src/PinguChan.Cli`.
- **CI/CD:**
  - Matrix build/test on Windows and Linux. Artifacts: zipped CLI binaries.
- **Validation (developer checklist):**
  - Smoke: run a short session (e.g., `pingu run --duration 00:00:10`) and verify CSV/JSONL outputs exist.
  - Lint/format: follow default .NET conventions (no custom formatter committed yet).
  - Redaction: ensure tokens/URLs are not emitted in logs.

## Coding Standards & Best Practices
- Use clear, concrete names and interfaces.
- Prefer async/await and `Channel<T>` for concurrency.
- Minimize dependencies in Core; keep extras optional.
- Use C# 12 features (primary constructors, required members).
- Follow .NET and C# conventions for formatting and naming.
- Document all public APIs and interfaces.
- Use TODOs for incomplete features and future work.

## How Copilot uses these instructions
- These repository-wide instructions are always applied in Copilot Chat when working in this repo.
- Additional, path-specific instructions in `.github/instructions/*.instructions.md` are applied when editing matching paths and are merged with these.
- Optional prompt files in `.github/prompts/*.prompt.md` can be attached in Copilot Chat for task-specific guidance (VS Code: enable `"chat.promptFiles": true` in workspace settings to browse prompts).

## Guidance for Copilot Agents
- Trust these instructions for build, test, and validation steps. Only search if something is missing or demonstrably incorrect.
- Minimize exploration; use the provided repo structure and commands.
- Document any errors and workarounds found during build/test.
- Always run `dotnet restore` before building or testing.
- Validate changes with unit tests and CI workflows.

## Additional Notes
- No packet payload capture; only metadata (timestamps, hostnames, timings).
- Remote endpoints are opt-in via config; no listeners by default.
- Sensitive data (tokens, webhook URLs) must be redacted in logs.
- User-data disclaimer in README.

---
For advanced context, use path-specific instructions in `.github/instructions/` and reusable prompt files in `.github/prompts/` (attach them in Copilot Chat when needed).
