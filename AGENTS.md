# Agent Guidance for Pingu-chan

- Trust `.github/copilot-instructions.md` and path-specific instructions under `.github/instructions/`.
- Prefer minimal exploration; only search when instructions are incomplete or clearly wrong.
- Always run `dotnet restore` before build/test. Validate with `dotnet test` from repo root.
- For quick smoke, run `pingu run --duration 00:00:10` and check CSV/JSONL outputs.
- Do not introduce packet payload capture. Redact tokens/webhook URLs in logs.
- Keep Core minimal; extras optional behind interfaces. Use C# 12 features. Document public APIs.
- Use prompt files in `.github/prompts/` to bootstrap common tasks.
