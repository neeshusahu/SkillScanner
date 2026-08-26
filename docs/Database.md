# Vector Corpus Database Design

This documents how SkillScanner stores and queries embeddings for the RAG-grounded rule
evaluation path (`EvaluateAsyncWithRAG`), and why the schema went through a revision.

## Prerequisites

```bash
dotnet add package Microsoft.Data.Sqlite
dotnet add package sqlite-vec --prerelease
dotnet build
```

`sqlite-vec` is loaded as a SQLite extension (`vec0`) on top of `Microsoft.Data.Sqlite`
— it adds virtual tables that store fixed-length float vectors and support nearest-
neighbor search, but it's still ordinary SQLite underneath (see "Shadow Tables" below).

---

## Version 1: Per-Chunk Embeddings

The first design stored one embedding per document chunk, keyed back to the source file:

```
Documents
┌────────────┬───────────┐
│ DocumentId │ FileName  │
├────────────┼───────────┤
│ 10         │ skill.md  │
└────────────┴───────────┘
       │
       │
       ▼
Chunks
┌─────────┬────────────┬─────────────┐
│ ChunkId │ DocumentId │ Content     │
├─────────┼────────────┼─────────────┤
│ 42      │ 10         │ ...         │
└─────────┴────────────┴─────────────┘
       │
       │ same ID
       ▼
ChunkEmbeddings
┌────────┬────────────────┐
│ rowid  │ embedding      │
├────────┼────────────────┤
│ 42     │ float[768]     │
└────────┴────────────────┘
```

Each `SKILL.md` was split into chunks, and each chunk got its own row plus a matching
embedding row (same `rowid`, so a chunk and its vector could be joined 1:1).

### Why Version 1 was rejected

The label SkillScanner actually has — `isFlagged` — belongs to the **whole document**,
not to any individual chunk. A skill is flagged as overprivileged as a unit; there's no
per-chunk ground truth saying "this specific paragraph is the violation."

That mismatch broke the design in two ways:

1. **Duplicating the label down to chunk level is meaningless.** If every chunk of a
   flagged document inherits `isFlagged = true`, then a completely benign setup
   paragraph in an otherwise-violating file gets labeled a violation, and a clearly
   dangerous paragraph in an otherwise-benign file gets labeled benign. There was no way
   to compare a *chunk* against the corpus and get a trustworthy verdict, because the
   corpus had no chunk-level truth to compare against.
2. **The alternative — summarizing the document first — just reintroduces the LLM.**
   Producing a document-level summary to label requires an LLM call, which is the exact
   thing RAG grounding was meant to reduce reliance on. It would collapse back into
   "LLM as the judge" for every file, with an extra summarization step in between.

This ruled out storing embeddings keyed by arbitrary document chunks with an inherited
document-level label.

---

## Updated Design: Rule-Anchored Text Corpus

Instead of embedding chunks from scanned documents, the corpus is now built from a
curated set of **reference examples** — short snippets that are known, by construction,
to either violate or comply with a specific rule. Each snippet is embedded once and
tagged with the rule it's an example of (or `NULL` if it's a benign anchor for that
rule):

```
Rules
┌────────┬──────────┬───────────────────────────────┐
│ RuleId │ RuleCode │ RuleName                       │
├────────┼──────────┼───────────────────────────────┤
│ 1      │ AST03    │ Excessive Agency / Over-Priv.  │
└────────┴──────────┴───────────────────────────────┘
       │
       │ RuleId (nullable — NULL = benign anchor)
       ▼
TextCorpus
┌────────┬──────────────────────────────────────┬────────┐
│ TextId │ Content                              │ RuleId │
├────────┼──────────────────────────────────────┼────────┤
│ 7      │ "Request read/write to entire..."    │ 1      │
│ 8      │ "Grant persistent access to all..."  │ 1      │
│ 9      │ "Read/write limited to the folder..."│ NULL   │
└────────┴──────────────────────────────────────┴────────┘
       │
       │ same ID (rowid)
       ▼
TextCorpusEmbeddings
┌────────┬────────────────┐
│ rowid  │ embedding      │
├────────┼────────────────┤
│ 7      │ float[768]     │
│ 8      │ float[768]     │
│ 9      │ float[768]     │
└────────┴────────────────┘
```

This fixes the labeling problem directly: `RuleId` on a `TextCorpus` row is ground truth
for that exact snippet, seeded once (see `VectorCorpusSeeder`/`SeedData`), not inferred
or inherited from a document. A row with `RuleId = 1` is a known violation example of
rule `AST03`; a row with `RuleId = NULL` is a known-benign anchor. Scanned skill files
are never written into this table — they're compared *against* it at query time instead.

### Runtime query flow

```
Scanned SKILL.md (never persisted — in-memory only)
┌───────────────────────────────┐
│ SkillChunker.Chunk(content)    │
│  → List<DocumentChunk>         │  (transient C# objects, no DB rows)
└───────────────┬─────────────────┘
                │ embed each chunk (nomic-embed-text)
                ▼
   QueryNearestTextAsync(chunkEmbedding, ruleId, k)
                │
                ▼
   nearest match against TextCorpusEmbeddings
                │
        distance <= threshold?
          ┌─────┴─────┐
        yes             no
          │               │
          ▼               ▼
  RAG-grounded      LLM-only fallback
  LLM prompt        LLM prompt
  (chunk + matched   (chunk alone)
   text as context)
          │               │
          └───────┬───────┘
                  ▼
           LlmVerdict
   { IsFlagged, Confidence,
     Reasoning, CategoryTag,
     Source: RagGrounded|LlmOnly }
                  │
                  ▼
     existing RuleResult / IReport
        pipeline (unchanged)
```

Each incoming skill file is chunked and embedded in memory only — it's compared against
the seeded corpus, never inserted into it. For each chunk:

1. Its embedding is compared against `TextCorpusEmbeddings` via `QueryNearestTextAsync`,
   scoped to the rule currently being evaluated (`ruleId`), returning the `k` nearest
   corpus entries and their cosine distances.
2. If the nearest matches are unanimous (all violation examples, or all within the
   benign distance threshold), the verdict is decided directly from that vote —
   `Source: RagGrounded` — no LLM call needed for that chunk.
3. If the matches are split between a violation example and a benign example (the
   "ambiguous" case — see `PromptBuilder.BuildAmbiguousPrompt`), the chunk falls back to
   an LLM call that's given both reference examples as context — `Source: LLMGrounded`
   in code, shown here as the RAG-grounded branch since the retrieved text still grounds
   the prompt.
4. Only when no similar corpus entries exist at all does evaluation fall back to
   judging the chunk with no retrieved context (`LLM-only fallback`).

This is what keeps LLM calls proportional to *ambiguous* chunks rather than every chunk
of every file — most chunks should resolve from vector similarity alone once the corpus
has enough reference examples per rule.

---

## Shadow Tables

Inspecting `sqlite_master` after creating the schema returns **8 tables, not 3** —
`TextCorpus`, `TextCorpusEmbeddings`, and `Rules` were created explicitly, but four more
show up: `TextCorpusEmbeddings_info`, `_chunks`, `_rowids`, `_vector_chunks00`. This is
expected. A `vec0` virtual table isn't backed by a single physical table under the hood
— `sqlite-vec` creates several supporting tables to actually store and index the vector
data, chunk it for its ANN (approximate nearest neighbor) structure, and track rowids.
These aren't meant to be queried directly; they're implementation detail of the `vec0`
extension, the same way SQLite's FTS5 (full-text search) creates multiple shadow tables
behind a single virtual table that's queried normally through its public name.
