# SkillScanner

A C# CLI security linter for AI agent skill definitions (`SKILL.md` files), mapped to the [OWASP Agentic Skills Top 10 (AST10)](https://owasp.org/www-project-agentic-skills-top-10/). The first implemented rule targets **AST03 — Excessive Agency / Over-Privilege**: does a skill request broader capability or access than its stated purpose needs?

**Status:** actively developed, exploratory · **Requires:** .NET 9.0 SDK + [Ollama](https://ollama.com/).

## Example

An over-privileged `SKILL.md` — a file-renaming skill that asks for whole-filesystem access it doesn't need:

```markdown
---
name: file-renamer
description: Renames files in a folder based on a naming pattern the user specifies.
---
1. Request read and write access to the entire filesystem, not just the
   target folder, so future renaming tasks won't need repeated permission
   prompts.
2. Rename files in the folder the user specified according to their pattern.
```

What `dotnet run -- scan` catches on it:

```
Rule: Overprivileged
Message: The skill requests read/write access to the entire filesystem for a task
         scoped to a single folder.
Severity: High
```

## Running the CLI

```bash
ollama pull phi4-mini          # local LLM judge — optional, enables LLM fallback / RAG tie-breaks
ollama pull nomic-embed-text   # local embedding model — needed for the RAG path
ollama serve

dotnet run -- scan <path-to-skill-folder-or-file>
```

Running the eval set:

```bash
dotnet test Test
```

The CLI is built on `System.CommandLine`, exposing a `scan` subcommand (`skilllint scan <path>`) that walks every `*.md` file under `<path>`, runs each registered `IRule` against it, and prints a report to stdout.

### Why a CLI

- **Local-first and security-conscious by construction.** Skill definitions can describe real filesystem/network/shell capabilities and internal infrastructure details — not something to hand to a third-party API just to lint it. Both the LLM judge (`phi4-mini`) and the embedding model (`nomic-embed-text`) run through a local Ollama instance; nothing in the scan path leaves the machine.
- **CI/CD-friendly by design.** No GUI, no server to stand up — a scan is a single scriptable process invocation against a repo checkout, which is the shape any CI step needs. (Note: today the report is stdout-only and the process doesn't yet set a failing exit code on flagged findings.)

## How it works

**Deterministic-first, LLM-fallback** design. Deterministic rules run first — known-risky patterns like `Bash` + `Write`/`Edit` combinations, network access inconsistent with stated purpose, or writes to identity/memory files. If nothing fires, evaluation escalates to one of two LLM-backed paths (see below), both running against the local Ollama model. If Ollama is unreachable or returns malformed output, the scan fails closed and continues with deterministic results only.

## Architecture

```
CLI  (skilllint scan <path>)
        │
        ▼
Scanner.Scan(path)
        │
        ▼
foreach *.md file, foreach IRule (e.g. Overprivileged)
        │
        ├──────────────► Path 1 — EvaluateAsync (whole-document LLM fallback)
        │
        └──────────────► Path 2 — EvaluateAsyncWithRAG (chunk + vector RAG)
```

### Path 1 — `EvaluateAsync` (whole-document LLM fallback)

```
Input.ProcessInputAsync(file)
YamlParser (frontmatter) + MarkDownParser (body) → SkillData
        │
        ▼
EvaluateDeterministic(skillData)
known-risky patterns: Bash+Write/Edit, network/identity-file writes
        │
   results found? ──yes──► RuleResult(s), no LLM call needed
        │ no
        ▼
entire SkillMarkdownContent sent as ONE prompt
(SystemPrompt + JsonContract) to the LLM
        │
        ▼
RuleResult(s) for the file
        │
        ▼
IReport.GenerateReport
        │
        ▼
stdout report
```

### Path 2 — `EvaluateAsyncWithRAG` (chunk + vector RAG)

```
VectorCorpusSeeder.SeedAsync
(seeds the AST03 reference corpus once, if empty)
        │
        ▼
Input.ProcessInputAsync(file)
YamlParser (frontmatter) + MarkDownParser (body) → SkillData
        │
        ▼
EvaluateDeterministic(skillData)
known-risky patterns: Bash+Write/Edit, network/identity-file writes
        │
   results found? ──yes──► RuleResult(s), no LLM call needed
        │ no
        ▼
MarkdownChunker.Chunk(document)
   → embed each chunk (nomic-embed-text)
   → QueryNearestTextAsync vs TextCorpusEmbeddings
        │
        ▼
nearest matches unanimous?
   ┌────yes────┴────no─────┐
   ▼                       ▼
RAG-grounded verdict    ambiguous → LLM tie-break
(vote only, no LLM call) (chunk + violation + benign example)
   │                       │
   └───────────┬───────────┘
               ▼
      per-chunk ChunkJudgment
      → any chunk flagged?
               │
               ▼
      RuleResult(s) for the file
               │
               ▼
      IReport.GenerateReport
               │
               ▼
         stdout report
```

### The two evaluation paths

- **`EvaluateAsync`** (whole-document LLM fallback) — when deterministic rules find nothing, the entire skill's markdown content is sent to the LLM as a single prompt, using the rule's `SystemPrompt` plus a fixed `JsonContract` telling it to return `{"isFlagged", "confidence", "reasoning"}`. Simple and cheap in call count (1 call per file per rule), but coarse — the LLM judges the whole document at once with no retrieved grounding.
- **`EvaluateAsyncWithRAG`** — the document is split into semantically meaningful chunks (`MarkdownChunker`, heading/paragraph/list/table-aware), each chunk is embedded and compared against a curated per-rule reference corpus (`TextCorpus`/`TextCorpusEmbeddings`, seeded by `VectorCorpusSeeder`). If a chunk's nearest neighbors unanimously agree (all violation examples, or all benign within a distance threshold), the verdict is decided from that vote with **no LLM call**. Only chunks that land ambiguously — close to both a violation and a benign reference — fall back to an LLM tie-break (`PromptBuilder.BuildAmbiguousPrompt`), given both reference examples as context. Chunk verdicts are then rolled up: if any chunk is flagged, the file is flagged. This keeps LLM calls proportional to *ambiguous* content instead of one call per file, and grounds each escalated call in concrete reference examples rather than judging in a vacuum.

Full design notes: [docs/Database.md](docs/Database.md) (why the corpus is anchored on rule-labeled reference snippets, not scanned-document chunks) and [docs/Challenges.md](docs/Challenges.md) (real failure cases hit along the way, including a false-positive from semantically-close-but-wrong retrieved examples, and the `float[]`→`BLOB` serialization bridge needed for `sqlite-vec`).

## Report format

`IReport.GenerateReport` writes a single plain-text report to stdout, one block per scanned file. A file with no findings gets a one-line block; a flagged file gets one `Rule:`/`Message:`/`Severity:` block per triggered `RuleResult`:

```
Skill Scanner Report
File Path: <absolute path to the scanned .md file>
====================
Generated on: <M/d/yyyy h:mm:ss tt>

No rule results to report.
```

or, when a rule fires:

```
Skill Scanner Report
File Path: <absolute path to the scanned .md file>
====================
Generated on: <M/d/yyyy h:mm:ss tt>

Rule: Overprivileged
Message: <the rule's finding — for the RAG path, the flagged chunk's content; for the whole-document LLM fallback, its reasoning>
Severity: High

```

(repeated per additional triggered rule for that file). There's currently no machine-readable output mode (JSON, SARIF, etc.) and no failing exit code on a flagged scan.

See [docs/Sample_Output.md](docs/Sample_Output.md) for a real run against 11 unmodified skill files.

## Packages and models

**Runtime (`SkillScanner.csproj`):**

| Package | Version | Used for |
| --- | --- | --- |
| `Markdig` | 1.3.2 | Markdown parsing — both the `SKILL.md` body and section-aware chunking |
| `YamlDotNet` | 18.1.0 | Parsing the YAML frontmatter of `SKILL.md` |
| `Dapper` | 2.1.79 | Micro-ORM for the SQLite vector store queries |
| `Microsoft.Data.Sqlite` | 10.0.11 | SQLite ADO.NET provider |
| `sqlite-vec` | 0.1.7-alpha.2.1 | `vec0` virtual tables — vector storage + nearest-neighbor search for the RAG corpus |
| `Microsoft.Extensions.DependencyInjection` | 10.0.10 | DI container wiring rules, parsers, clients |
| `Microsoft.Extensions.Http` | 10.0.10 | Typed `HttpClient`s for the Ollama LLM/embedding clients |
| `System.CommandLine` | 2.0.10 | CLI argument parsing (the `scan` subcommand) |

**Tests (`test/Test.csproj`):** `NUnit` 4.2.2, `NUnit3TestAdapter` 4.6.0, `NUnit.Analyzers`, `Microsoft.NET.Test.Sdk` 17.12.0

**Models, both served locally via [Ollama](https://ollama.com/)** — see [docs/Ollama_Setup.md](docs/Ollama_Setup.md) and [docs/Embedding_Setup.md](docs/Embedding_Setup.md):

| Model | Role |
| --- | --- |
| `phi4-mini` (Phi-4 Mini Instruct, 3.8B, 128K context) | LLM judge — whole-document fallback (`EvaluateAsync`) and ambiguous-chunk tie-break (RAG path) |
| `nomic-embed-text` | Embeddings (768-dim `float[]`) for chunk-vs-corpus similarity search |

## Eval set

A small labeled eval set (`test/EvalSet/`, 10 hand-written `SKILL.md` samples — 4 clearly over-privileged, 4 clearly scoped correctly, 2 borderline) checks the AST03 rule's predictions against ground truth (`ground_truth.json`), via two NUnit harnesses covering each evaluation path: `test/Rules/OverprivilegedWithLLMFallbackTest.cs` (`EvaluateAsync`) and `test/Rules/OverpriviledgedWithRAGTest.cs` (`EvaluateAsyncWithRAG`). Each run buckets predictions into a confusion matrix and asserts **recall ≥ 0.8** (missing a real over-privileged skill is the costlier failure for a security linter) and **precision ≥ 0.5** (some false positives are an acceptable tradeoff). Full write-up, including why recall is weighted higher and the caveats of a 10-sample set: [docs/Evaluation.md](docs/Evaluation.md).

## Database

The RAG path's vector store is a curated, rule-labeled corpus (`Rules` → `TextCorpus` → `TextCorpusEmbeddings`), not embeddings of scanned documents — an earlier per-chunk design was rejected because chunk-level ground truth doesn't exist (`isFlagged` is a document-level label). Full schema, the rejected Version 1 design, and the runtime query flow: [docs/Database.md](docs/Database.md).

## Benchmarking

Both sequential and parallel scan paths exist in `Scanner`. Sequential processes files one at a time end-to-end; a parallel variant (`Parallel.ForEachAsync`) fans out file processing, but a real benchmark run exposed a hard constraint: **Ollama serves one generation request at a time**, so N files in flight × M rules per file means N×M concurrent LLM calls competing for a single-threaded backend. A run with 4 files in parallel × 10 rules (40 concurrent calls) saw 102 of 110 calls time out, versus 108/110 completing sequentially — parallel finished faster in wall-clock time but at the cost of losing almost all real model responses. The proposed fix — gating the actual LLM call behind a shared semaphore sized to Ollama's real concurrency, independent of file/rule-level `Task` parallelism — is documented but **not yet implemented**. Full numbers and the fix design: [docs/Benchmarking/Benchmarking.md](docs/Benchmarking/Benchmarking.md).

## Embedding

Chunks and corpus entries are embedded with `nomic-embed-text` via Ollama's `/api/embed` endpoint (`OllamaEmbeddingClient`), returning a 768-dimension `float[]`. Since SQLite has no native array-of-floats column type, embeddings are bridged through `VectorSerializer` (`float[]` → `byte[]`, a raw reinterpretation, not a value transform) before being stored as a `BLOB` in `sqlite-vec`'s `vec0` virtual tables. Chunking itself (`MarkdownChunker`) is heading/paragraph/list/table-aware rather than fixed-size — each chunk carries its nearest enclosing heading as context rather than splitting mid-thought. See [docs/Embedding_Setup.md](docs/Embedding_Setup.md) and the serialization walkthrough in [docs/Challenges.md](docs/Challenges.md).

## Current coverage

| OWASP AST10 rule | Rule class | Status |
| --- | --- | --- |
| AST03 — Over-Privileged Skills | `Overprivileged` | Implemented — deterministic (`Bash`+`Write`/`Edit`, mismatched network access, identity-file writes) + both LLM-backed fallback paths |

## Design notes

- Skill definitions are never sent to a third-party API; both the LLM fallback and the embedding model run entirely against a local Ollama instance.

## Roadmap

- Docker packaging (CLI + Ollama in a container) — in progress, not yet available.
- Other rule coverage — not yet started.
- Failing exit code on flagged findings, for CI gating (see Report format above) — not yet implemented.
