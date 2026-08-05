# SkillScanner

A C# CLI security linter for AI agent skill definitions (`SKILL.md` files), mapped to the [OWASP Agentic Skills Top 10 (AST10)](https://owasp.org/www-project-agentic-skills-top-10/).

## Why

Agentic AI skills grant AI agents real tool access — file writes, network calls, shell execution — but there's little tooling yet to check whether a skill requests more capability than its stated purpose needs. mcplint is a static analyzer for that gap, run entirely locally.

## How it works

**Deterministic-first, LLM-fallback** design:

1. **Deterministic rules run first** — known-risky patterns like `Bash` + `Write`/`Edit` combinations, network access inconsistent with stated purpose, or writes to identity/memory files.
2. **If nothing fires, a local LLM (Phi-4-mini via Ollama) runs as a fallback**, catching over-privilege that keyword matching can't.
3. **LLM findings are never treated as certain** — capped at `Medium` severity (below deterministic `High`) and clearly labeled, so a probabilistic guess is never visually indistinguishable from a confirmed match.
4. **Fail-closed** — if Ollama is unreachable or returns malformed output, the scan continues with deterministic results only.

## Architecture

- **`IRule`** — the contract every rule implements. `Scanner` depends only on this, so new rules never touch the scan engine.
- **`RuleBase`** — Template Method base class owning the deterministic→LLM-fallback orchestration. Rules that don't need a fallback implement `IRule` directly instead.
- **`ILlmClient`** / **`OllamaLlmClient`** — rule-agnostic abstraction over the LLM backend. Takes a system prompt + content, returns a generic `LlmVerdict`, reusable across all rules.

```
Scanner.Scan(path)
  → parse SKILL.md
  → foreach rule: EvaluateDeterministic → (if empty) TryEvaluateLlm
  → GenerateReport
```

## Current coverage

**AST03 (Excessive Agency / Over-Privilege)** — implemented. Detects `Bash` + `Write`/`Edit` combinations, mismatched network access requests, and writes to identity/memory files.

## Running it

```bash
ollama pull phi4-mini   # optional — enables LLM fallback
ollama serve
dotnet run -- scan path/to/SKILL.md
```

## Design notes

- LLM calls use `temperature = 0` and a fixed seed to minimize run-to-run variance (not perfectly deterministic — local inference has some floating-point variance).
- Skill definitions are never sent to a third-party API; the LLM fallback runs entirely against a local model.

## Status

Actively developed — an exploration of deterministic/LLM-hybrid design for the emerging agentic-skill security space.
