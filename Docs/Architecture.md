# SkillScanner — Architecture

C# / .NET 9 console app (SkillScanner). Pipeline:

```text
Input → Parse → Chunk → Embed → Rule Evaluation (RAG + LLM) → Report
```

---

## Chunking

### `IMarkdownChunker.cs` — interface

```csharp
List<DocumentChunk> Chunk(MarkdownDocument document)
```

Contract for splitting a parsed Markdown AST into chunks.

### `MarkdownChunker.cs` — class, implements `IMarkdownChunker`

Walks Markdig blocks (headings/paragraphs/lists/tables) with a stack, groups content under its owning heading, and flushes a `DocumentChunk` per heading boundary.

### `DocumentChunk.cs` — class (value object)

Properties:

- `Content`
- `MainHeading`
- `ChunkStartOffset`
- `ChunkEndOffset`
- `List<Section> OriginalData`

Represents one chunked segment of a document with source-span offsets.

### `Section.cs` — class

Intermediate representation of one Markdown block and the `MarkdownChunker` working unit.

Properties:

- `Block`
- `ContainerBlock`
- `Level`
- `Content`
- `SpanStart`
- `SpanEnd`
- `IsHeading`

---

## Embeddings

### `IEmbeddingClient.cs` — interface

```csharp
Task<float[]> GetEmbeddingAsync(string text)
```

Contract for producing vector embeddings.

### `OllamaEmbeddingClient.cs` — class, implements `IEmbeddingClient`

Calls Ollama's `/api/embed` using the `nomic-embed-text` model for a real 768-dimensional embedding.

### `EmbeddingClient.cs` — class, implements `IEmbeddingClient` Fake for testing

Fake/deterministic embedding (hash-seeded), used historically to exercise storage code paths.

---

## ExceptionHandler

### `IExceptionHandler.cs` — interface

```csharp
void Handle(Exception exception)
```

### `ConsoleExceptionHandler.cs` — class, implements `IExceptionHandler`

Prints exception messages to the console in red; top-level catch target from `Program.cs`.

---

## Inputs

### `IInput.cs` — interface

```csharp
Task<SkillData?> ProcessInputAsync(string path)
```

### `Input.cs` — class, implements `IInput`

Depends on:

- `IParser<SkillData>`
- `IParser<MarkdownDocument>`

Reads a `.md` file → splits YAML frontmatter / body (`SkillDocument`) → parses metadata to `SkillData` and body to a Markdig AST.

Acts as the file-ingestion entry point.

---

## LLMClient

### `ILLMClient.cs` — interface

```csharp
Task<LLMVerdict> GetResponseAsync(
    string systemPrompt,
    string userPrompt,
    CancellationToken cancellationToken)
```

### `OllamaClient.cs` — class, implements `ILLMClient`

Calls Ollama's `/api/generate` using `phi4-mini` with JSON format, deserializes the response into `LLMVerdict`, and tags the source as `LLMGrounded`.

### `LLMVerdict.cs` — defines 3 types

#### `LLMVerdict`

Properties:

- `IsFlagged`
- `Confidence`
- `Reasoning`
- `Source`

#### `OllamaResponse`

Raw `/api/generate` envelope wrapper.

#### `VerdictSource`

Enum:

- `RagGrounded`
- `LLMGrounded`

---

## Mapping

### `IMapper.cs` — generic interface

```csharp
T Map(
    Dictionary<string, object> source,
    Dictionary<string, string> mapping)
```

### `ReflectionMapper.cs` — generic class

```csharp
ReflectionMapper<T> where T : new()
```

Implements `IMapper<T>`.

Uses reflection and `PropertyInfo.SetValue` to populate a new `T`.

### `SkillMetaDataMapping.cs` — static class

Property mappings:

```text
"name"          → Name
"description"   → Description
"compatibility" → Compatibility
```

---

## Models

### `SkillData.cs` — class

Central domain object:

- `Name`
- `Description`
- `Compatibility`
- `Permission`
- `SkillMarkdownContent`
- `SkillMarkdown` (AST)

Flows through parsing → rules → LLM.

### `SkillDocument.cs` — class

Contains:

- `SkillMetadata` — raw YAML text
- `SkillContent` — raw Markdown text

### `RuleResult.cs` — class

Contains:

- `RuleType`
- `Message`
- `IsFlagged`

Also declares:

- `RuleResultType` — `Success`, `Failure`, `Warning`
- `RuleSeverity` — `Low`, `Medium`, `High`

### `RuleType.cs` — class

Static metadata identifying a rule:

- `Name`
- `Id`
- `Description`
- `Severity`

---

## Output

### `IReport.cs` — interface

```csharp
GenerateReport(Dictionary<string, List<RuleResult>>)
GenerateReportFile(...)
```

### `Report.cs` — class, implements `IReport`

`GenerateReport` formats results by rule, message, and severity per file and writes them to the console.

`GenerateReportFile` is currently an unimplemented stub.

---

## Parser

### `IParser.cs` — generic interface

```csharp
Task<T> ParseAsync(string? data)
```

### `MarkDownParser.cs` — class, implements `IParser<MarkdownDocument>` 

Uses a Markdig pipeline with `UsePipeTables()` to parse Markdown tables into an AST.

### `YamlParser.cs` — class, implements `IParser<SkillData>` 

Depends on `IMapper<SkillData>`.

Deserializes YAML using camelCase → dictionary → maps to `SkillData` through `SkillMetaDataMapping`.

### `MarkDownParserWithChunks.cs` — class unused 

Implements:

```csharp
IParser<IEnumerable<DocumentChunk>>
```

Parses and chunks in one step. Superseded by `RuleBase` calling `IMarkdownChunker` directly.

---

## Repository — Vector Store

**SQLite + sqlite-vec + Dapper**

### `IVectorRepository.cs` — interface

```csharp
InitializeSchemaAsync()
InsertRuleAsync(...)
InsertTextCorpusEntryAsync(...)
SearchSimilarTextAsync(float[], long?, int topK = 3)
```

### `VectorRepository.cs` — class, implements `IVectorRepository`

Depends on `IDbConnection` (SQLite).

Responsibilities:

- Create tables
- Upsert rules
- Upsert corpus entries
- Upsert embeddings
- Execute transactional writes
- Perform KNN cosine-similarity search

### `VectorSerializer.cs` — static class

```csharp
ToBytes(float[])
FromBytes(byte[])
```

Packs/unpacks vectors for `vec0` blob storage using `MemoryMarshal.Cast`.

### `TextCorpusMatch.cs` — record

Represents a similarity-search result:

- `TextId`
- `RuleId`
- `Content`
- `Distance`

### `SeedData.cs` — static class

Contains the hardcoded `OverprivilegedCorpus`:

- 8 violation examples
- 3 benign examples

Used for rule `AST03`.

### `VectorCorpusSeeder.cs` — static class

```csharp
SeedAsync(
    IVectorRepository repository,
    IEmbeddingClient embeddingClient)
```

Initializes the schema, inserts the rule row, embeds seed corpus entries, and inserts them into the vector store.

Runs once per `Scanner.Scan`.

### `SqlQueries/`

Contains:

- `InsertQueries.cs`
- `Schema.cs`
- `SearchQueries.cs`

These hold raw SQL constants consumed by `VectorRepository`.

The schema includes a `vec0` virtual table with:

```sql
embedding float[768] distance_metric=cosine
```

---

## SkillRule

### `IRule.cs` — interface

```csharp
EvaluateAsync(...)
EvaluateAsyncWithRAG(...)
CountCalls()
```

### `RuleBase.cs` — abstract class, implements `IRule`

Depends on:

- `ILLMClient`
- `IVectorRepository`
- `IEmbeddingClient`
- `IMarkdownChunker`

Uses the Template Method pattern.

Common evaluation flow:

1. Run deterministic checks.
2. If necessary, fall back to plain LLM evaluation or RAG + LLM evaluation.
3. For RAG:
   - Chunk the Markdown.
   - Embed chunks.
   - Perform KNN vector search.
   - Apply distance-threshold decision logic.
   - Send ambiguous chunks to the LLM for resolution.

Tracks static success/failure counters.

### `Overprivileged.cs` — class, extends `RuleBase`

Current concrete rule.

- Rule ID: `1`
- Severity: `High`

Deterministic checks include:

- Network-access permission text
- Write-access mentions
- `memory.md`
- `soul.md`
- `curl`
- `bash`

### `PromptBuilder.cs` — static class

Builds the LLM disambiguation prompt:

```csharp
BuildAmbiguousPrompt(
    chunkContent,
    violationExample,
    benignExample)
```


### `ChunkJudgement.cs` — record

```csharp
ChunkJudgment
```

Contains:

- `Chunk`
- `Matches`
- `Verdict`

---

## Orchestration

### `Scanner.cs` — class

Depends on:

- `IReport`
- `IInput`
- `IEnumerable<IRule>`
- `ILLMClient`
- `IVectorRepository`
- `IEmbeddingClient`

`Scan`:

1. Seeds the vector corpus.
2. Enumerates `*.md` files.
3. Parses each file.
4. Runs every `IRule.EvaluateAsyncWithRAG`.
5. Aggregates `RuleResult` objects per file.
6. Generates the report.

### `Program.cs` — CLI entry point

Uses **System.CommandLine**.

Command:

```bash
skilllint scan <filePath>
```

Builds the DI container through `AddSkillScanner()`, resolves `Scanner` and `IExceptionHandler`, and executes the scan inside a `try/catch`.

### `DependencyInjection.cs` — static class

Provides:

```csharp
AddSkillScanner(this IServiceCollection)
```

Acts as the composition root.

---

# Dependency Injection Map

| Interface | Implementation | Lifetime |
|---|---|---|
| `IMapper<>` | `ReflectionMapper<>` | Singleton |
| `IReport` | `Report` | Transient |
| `IParser<SkillData>` | `YamlParser` | Transient |
| `IParser<MarkdownDocument>` | `MarkDownParser` | Transient |
| `IInput` | `Input` | Transient |
| `IExceptionHandler` | `ConsoleExceptionHandler` | Transient |
| `IRule` | `Overprivileged` (reflection-discovered) | Transient |
| `Scanner` | `Scanner` | Transient |
| `ILLMClient` | `OllamaClient` | Typed `HttpClient` |
| `IEmbeddingClient` | `OllamaEmbeddingClient` | Typed `HttpClient` |
| `IVectorRepository` | `VectorRepository` | Singleton |
| `IDbConnection` | `SqliteConnection` (`skillscanner.db`, vec0, WAL) | Singleton |
| `IMarkdownChunker` | `MarkdownChunker` | Transient |

`IRule` implementations are auto-discovered through reflection over the assembly. Adding a new `RuleBase` subclass therefore registers the rule automatically.

---

# Pipeline Flow

```text
Program.cs
    ↓
Scanner.Scan(path)
    │
    ├── 1. VectorCorpusSeeder.SeedAsync
    │       ├── Initialize schema
    │       ├── Seed AST03 corpus
    │       ├── Embed using IEmbeddingClient
    │       └── Store using IVectorRepository
    │
    └── 2. For each *.md file
            │
            ├── IInput.ProcessInputAsync
            │       ├── SkillDocument
            │       ├── YamlParser + ReflectionMapper
            │       └── MarkDownParser → Markdig AST
            │
            └── For each IRule
                    │
                    └── EvaluateAsyncWithRAG
                            │
                            ├── EvaluateDeterministic
                            │
                            └── EvaluateWithVectorSimilarityAndLLM
                                    │
                                    ├── IMarkdownChunker.Chunk
                                    ├── IEmbeddingClient.GetEmbeddingAsync
                                    ├── IVectorRepository.SearchSimilarTextAsync
                                    ├── Distance-threshold decision
                                    └── PromptBuilder + ILLMClient
                                          for ambiguous chunks
                            │
                            ↓
                       RuleResult(s)
                            │
                            ↓
                    IReport.GenerateReport
```

## Exception Flow

```text
Program.cs
    ↓
try
    ↓
Scanner.Scan()
    ↓
Exception
    ↓
IExceptionHandler.Handle()
    ↓
Console output (red)
```

## Current Architecture Notes

- `OllamaEmbeddingClient` is the active embedding implementation.
- `EmbeddingClient` is retained as a historical deterministic/fake implementation and is not registered.
- `MarkDownParserWithChunks` is unused and not registered; chunking is handled by `IMarkdownChunker`.
- `Report.GenerateReportFile` is currently a stub.
- `PromptBuilder` currently has a string interpolation issue that should be fixed.
