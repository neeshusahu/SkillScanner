# Chunking Strategy

SkillScanner currently does **not use a dedicated chunking package**.

Markdown is parsed using **Markdig**, which produces a structured block hierarchy containing headings, paragraphs, lists, code blocks, tables, and other Markdown elements.

## Current Approach — Heading-Based Chunking

The current implementation creates a chunk around each Markdown heading and its associated content.

```text
Markdown
   ↓
Markdig Parser
   ↓
Block Hierarchy
   ↓
Heading + Content
   ↓
DocumentChunk
```

Each heading acts as a natural semantic boundary.

```text
## Permissions

<content>

        ↓

Chunk:
Heading: Permissions
Content: <content>
```

This preserves the relationship between a section heading and its content.

## Why Heading-Based Chunking?

Markdown headings already provide semantic structure. This approach:

- preserves section boundaries
- keeps related content together
- retains meaningful context
- avoids an additional chunking dependency

The resulting chunks are embedded and stored for semantic retrieval.

## Chunk Metadata

`DocumentChunk` retains structural and source information for traceability.

```text
DocumentChunk
├── Content
├── MainHeading
├── ChunkStartOffset
├── ChunkEndOffset
└── OriginalData
    └── Source sections
```

| Property | Description |
|---|---|
| `Content` | Markdown content included in the chunk |
| `MainHeading` | Heading that owns the chunk |
| `ChunkStartOffset` | Start position in the original document |
| `ChunkEndOffset` | End position in the original document |
| `OriginalData` | Original parsed sections and Markdown structure |

This metadata supports source tracing, debugging chunk boundaries, and providing precise context during retrieval.

## Exploring Alternative Strategies

The current approach is intentionally simple. Other strategies are being evaluated based on retrieval quality.

### Heading Hierarchy

Preserve parent-child context:

```text
# Security
   ├── ## Permissions
   │      └── ### File Access
   └── ## Network Access
```

### Chunk Size

Very large sections may need to be split while retaining their parent heading and source offsets.

### Overlap

Overlap between adjacent chunks may help preserve context across boundaries.

```text
Chunk 1: A B C D E
Chunk 2:         E F G H I
                  ↑
               overlap
```

## Current Status

```text
Markdig
   ↓
Markdown AST
   ↓
Heading-based chunks
   ↓
Chunk metadata
   ↓
Embedding
   ↓
SQLite-vec
   ↓
Semantic retrieval
```

**Current:** Heading-based chunking with source traceability.

**Exploring:** Heading hierarchy, chunk-size limits, and overlap.

The final strategy will be evaluated based on **retrieval quality**.
