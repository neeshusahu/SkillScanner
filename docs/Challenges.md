# Challenge: False Positives from Semantically Similar Violation Examples

## Problem

The SkillScanner uses vector similarity to retrieve examples from a corpus of known violations and benign examples. The retrieved examples are then provided to an LLM to determine whether the target skill violates a security rule.

During evaluation, the following Azure Monitor content was incorrectly flagged as an `Overprivileged` violation:

> Use Azure Monitor to inspect metrics, logs, alerts, and resource health.
>
> 1. Identify the affected resource, time range, and symptom.
> 2. Prefer existing metrics and diagnostic data before enabling new collection.
> 3. Correlate alert history with deployments and configuration changes.
> 4. Propose alert thresholds with a clear signal, owner, and response action.
>
> Do not change production alerts or diagnostic settings without explicit approval.

The retrieved examples included:

- `Delete files anywhere on disk matching a pattern, without confirming each location with the user.`
- `Register a background process to run automatically on every system startup for a one-time task.`

Both examples were labeled as violations (`ruleId = 1`) and had cosine distances around `0.50`.

The LLM subsequently produced:

```text
LLMVerdict: Unanimous match against violation reference examples.
```

---

# Challenge: Persisting C# `float[]` Embeddings into sqlite-vec

## Problem

Embeddings come back from the embedding model as a C# `float[]` (768 elements for the
model in use), but there's no direct way to write that array into `sqlite-vec`'s
`TextCorpusEmbeddings`/`ChunkEmbeddings` virtual tables through ADO.NET.

`sqlite-vec`'s storage column doesn't understand C# `float[]` — it only understands raw
bytes, and something has to bridge that gap before the value can be written to disk.

## Why the gap exists

The layering, concretely:

- **`float[]` in C#** — a managed array; each float is 4 bytes, but the array itself is
  a .NET object with its own memory layout, type metadata, and garbage-collector
  bookkeeping — not just "768 numbers sitting in a row" from ADO.NET's perspective.
- **SQLite's `BLOB` column type** — SQLite (and every ADO.NET provider, including
  `Microsoft.Data.Sqlite`) only knows how to store a handful of primitive types in a
  database file: `INTEGER`, `REAL`, `TEXT`, `BLOB`, `NULL`. There's no native
  "array of floats" column type in SQLite itself — `vec0`'s `float[768]` syntax is
  `sqlite-vec`'s own extension-level abstraction, but underneath it still has to
  persist that data as a `BLOB` (raw bytes) in the actual SQLite file format, because
  that's the only mechanism SQLite has for storing binary data.
- **`byte[]`** — this is the one C# type that maps directly onto SQLite's `BLOB` type.
  When you pass a `byte[]` as a Dapper/ADO.NET parameter, the provider knows exactly
  how to write it to disk as-is.

So the chain is: `float[]` (what the embedding model gives you) → `byte[]` (the only
thing SQLite/`sqlite-vec` can actually store) → written to disk.

## Fix

`VectorSerializer.ToBytes` bridges that gap — it doesn't change the values, it just
reinterprets the same 3072 bytes (768 floats × 4 bytes) as a flat byte array instead of
a typed float array, so ADO.NET has something it knows how to bind as a `BLOB`
parameter.