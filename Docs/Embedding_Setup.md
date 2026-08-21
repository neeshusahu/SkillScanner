# Embedding Model Setup — Nomic Embed Text

SkillScanner uses **Nomic Embed Text** through Ollama to generate embeddings for semantic search and RAG.

## Nomic Embed Text

`nomic-embed-text` is an open-source embedding model from **Nomic AI**. Unlike Phi-4 Mini, it is not a conversational or reasoning model. Its purpose is to transform text into numerical vectors where semantically similar text is positioned closer together in the embedding space.

| Property             | Value                 |
| -------------------- | --------------------- |
| Model                | `nomic-embed-text`    |
| Provider             | Nomic AI              |
| Parameters           | ~137M                 |
| Embedding dimensions | 768                   |
| Runtime              | Ollama                |
| Use in SkillScanner  | Semantic search / RAG |
| Output               | `float[768]` vector   |

The 768-dimensional output matches SkillScanner's SQLite-vec embedding schema:

```sql
embedding float[768]
```

---

## 1. Install / Pull the Model

```bash
ollama pull nomic-embed-text
```

Verify:

```bash
ollama list
```

You should see `nomic-embed-text` in the installed models.

---

## 2. Start Ollama

If Ollama is not already running:

```bash
ollama serve
```

The local API is available at:

```text
http://localhost:11434
```

Both Phi-4 Mini and the embedding model are served through the same Ollama instance.

```text
SkillScanner
     │
     └──────────────► Ollama :11434
                         │
                 ┌───────┴────────┐
                 ▼                ▼
            Phi-4 Mini      Nomic Embed Text
             /generate          /embed
                 │                │
                 ▼                ▼
             Judgment          Vector
```

---

## 3. Test the Embedding Model

Generate an embedding for a test sentence:

```bash
curl http://localhost:11434/api/embed -d '{
  "model": "nomic-embed-text",
  "input": "This skill can execute arbitrary commands."
}'
```

The response contains an embedding vector.

To verify its dimension:

```bash
curl -s http://localhost:11434/api/embed -d '{
  "model": "nomic-embed-text",
  "input": "test"
}' | jq '.embeddings[0] | length'
```

Expected result:

```text
768
```

This should match the vector table definition:

```sql
embedding float[768]
```

---

# Nomic Embed Text vs Phi-4 Mini

The two models have completely different responsibilities:

|                              | `nomic-embed-text`         | `phi4-mini`               |
| ---------------------------- | -------------------------- | ------------------------- |
| **Job**                      | Produce comparable vectors | Reason, judge, explain    |
| **Pipeline**                 | Embedding / retrieval tier | LLM evaluation tier       |
| **Input**                    | Text                       | Prompt + context          |
| **Output**                   | `float[768]`               | Text / structured verdict |
| **Can reason?**              | No                         | Yes                       |
| **Can explain a violation?** | No                         | Yes                       |

SkillScanner therefore uses them at different stages:

```text
Skill / Rule Corpus
       │
       ▼
Nomic Embed Text
       │
       ▼
768-dimensional vectors
       │
       ▼
SQLite-vec
       │
       ▼
Similarity Search
       │
       ▼
Relevant Rules / Examples
       │
       ▼
Phi-4 Mini
       │
       ▼
Final Evaluation
```

---

# Embedding Model Alternatives

Other embedding models available through Ollama include:

| Model                    | Dimensions | Tradeoff                                                |
| ------------------------ | ---------: | ------------------------------------------------------- |
| `all-minilm`             |        384 | Smaller and faster; lower dimensional representation    |
| `mxbai-embed-large`      |       1024 | Larger vectors; potentially stronger retrieval quality  |
| `bge-m3`                 |       1024 | Strong multilingual/retrieval capabilities              |
| `snowflake-arctic-embed` |     Varies | Multiple model sizes with quality/performance tradeoffs |

The embedding model should be selected based on **retrieval quality, latency, memory, and vector-storage requirements**.

### Important

Changing the embedding model requires re-embedding the corpus.

For example:

```text
nomic-embed-text
      │
      ▼
float[768]
```

cannot simply be replaced with:

```text
mxbai-embed-large
      │
      ▼
float[1024]
```

Existing vectors from different embedding models are not directly comparable.

Therefore, changing the model generally requires:

```text
Change model
     ↓
Update vector dimension/schema if required
     ↓
Re-embed corpus
     ↓
Rebuild vector index
```

---

# Quick Debug Tips

### Is the embedding model installed?

```bash
ollama list
```

### Is Ollama running?

```bash
ollama serve
```

### Is the API reachable?

```bash
curl http://localhost:11434
```

### Test embedding generation

```bash
curl http://localhost:11434/api/embed -d '{
  "model": "nomic-embed-text",
  "input": "test"
}'
```

### Check embedding dimension

```bash
curl -s http://localhost:11434/api/embed -d '{
  "model": "nomic-embed-text",
  "input": "test"
}' | jq '.embeddings[0] | length'
```

Expected:

```text
768
```

### Verify against SQLite-vec

The model output dimension must match:

```sql
embedding float[768]
```

If the dimensions don't match, the embedding cannot be inserted into the existing vector table.
