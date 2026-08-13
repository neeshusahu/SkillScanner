# SkillScanner

A C# CLI security linter for AI agent skill definitions (`SKILL.md` files), mapped to the [OWASP Agentic Skills Top 10 (AST10)](https://owasp.org/www-project-agentic-skills-top-10/).

## Running it

```bash
ollama pull phi4-mini   # optional — enables LLM fallback
ollama serve
dotnet run -- scan path
```

Running the eval set:

```bash
dotnet test Test
```

## Why

Agentic AI skills grant AI agents real tool access — file writes, network calls, shell execution — but there's little tooling yet to check whether a skill requests more capability than its stated purpose needs. mcplint is a static analyzer for that gap, run entirely locally.

## How it works

**Deterministic-first, LLM-fallback** design:

1. **Deterministic rules run first** — known-risky patterns like `Bash` + `Write`/`Edit` combinations, network access inconsistent with stated purpose, or writes to identity/memory files.
2. **If nothing fires, a local LLM (Phi-4-mini via Ollama) runs as a fallback**, catching over-privilege that keyword matching can't.
3. **Fail-closed** — if Ollama is unreachable or returns malformed output, the scan continues with deterministic results only.

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

## Benchmarking

Both sequential and parallel scan paths exist in `Scanner`. Sequential processes one `SKILL.md` file at a time end-to-end (parse → deterministic rules → LLM fallback); a parallel variant (`Parallel.ForEachAsync`, configurable degree of parallelism) is being evaluated against it.

Parallel benchmarking against the local Ollama backend surfaced a real constraint: concurrent LLM calls compete for the same local inference process, so throughput doesn't scale linearly with concurrency the way deterministic-only rules would. This is an open tuning area — semaphore-gating the LLM tier specifically (while keeping deterministic rules fully parallel) is the current direction, not yet finalized.

No throughput numbers are published here yet — benchmarking is still in progress and the results depend heavily on local hardware and which Ollama model is loaded, so a single number wouldn't be representative.

Benchmarked output live in `Benchmarking/` — check there for the current sequential-vs-parallel comparison

## Eval set

A small labeled eval set (`Test/EvalSet/`) checks the AST03 rule's predictions against hand-labeled ground truth (`ground_truth.json`), run via an NUnit test (`Test/Rules/OverprivilegedTest.cs`).

- **Scope**: 10 `SKILL.md` samples — 4 clearly over-privileged, 4 clearly scoped correctly, 2 borderline (correct on paper, over-privileged in intent).
- **Metric**: recall (TP / (TP + FN)) is the primary gate — for a security linter, a missed over-privileged skill is worse than a false alarm. Precision is tracked but not currently asserted on, given the sample size.
- **Caveat**: 10 samples is enough to catch regressions and compare deterministic-only vs. LLM-fallback behavior on known cases, not enough to claim a general accuracy/recall rate. Treat any current number as a snapshot of this specific set, not a claim about real-world performance.
- **What it currently shows**: the LLM fallback tier catches both borderline cases that the deterministic rule alone does not — expected, since they were deliberately written without a literal pattern (no explicit "root," "all files," etc.) for keyword rules to match. This is early signal that the two tiers are catching different things, not yet a validated result.

Expanding the eval set (more samples, more categories, precision made a hard gate once negative-sample count is larger) is planned before any accuracy claims go in this README.

## Design notes

- Skill definitions are never sent to a third-party API; the LLM fallback runs entirely against a local model.

## Status

Actively developed — an exploration of deterministic/LLM-hybrid design for the emerging agentic-skill security space.