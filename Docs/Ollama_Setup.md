# Ollama Setup — Phi-4 Mini

SkillScanner uses **Microsoft Phi-4 Mini** through [Ollama](https://ollama.com/) for local LLM inference.

## Phi-4 Mini

The current Ollama `phi4-mini` model is the **Phi-4 Mini Instruct** model.

| Property               | Value              |
| ---------------------- | ------------------ |
| Model                  | `phi4-mini`        |
| Current tag            | `phi4-mini:latest` |
| Parameters             | 3.8B               |
| Model size             | ~2.5 GB            |
| Context capability     | 128K tokens        |
| Input                  | Text               |
| Runtime                | Ollama             |        |

The current `phi4-mini:latest` Ollama tag has digest `78fad5d182a7` and is listed at approximately 2.5 GB with a 128K context window.

### What does `latest` mean?

`latest` is an **Ollama model tag**, not a model architecture or a guarantee of a particular permanent version.

```bash
ollama pull phi4-mini:latest
```

is equivalent to:

```bash
ollama pull phi4-mini
```

for the current `latest` tag.

Ollama's registry currently maps `phi4-mini:latest` to a specific model digest. If reproducibility matters, record the digest or pin the specific tag you tested rather than relying only on `latest`.

---

## 1. Install / Pull the Model

Install Ollama, then pull Phi-4 Mini:

```bash
ollama pull phi4-mini:latest
```

Verify the downloaded model:

```bash
ollama list
```

Expected output will show the model, size, and other metadata.

---

## 2. Start Ollama

```bash
ollama serve
```

Ollama exposes its local API at:

```text
http://localhost:11434
```

SkillScanner communicates with the local Ollama API rather than loading the model directly.

---

## 3. Test the Model

```bash
curl http://localhost:11434/api/generate -d '{
  "model": "phi4-mini:latest",
  "prompt": "Hello from SkillScanner",
  "stream": false
}'
```

A successful response confirms that:

```text
Ollama
  ↓
Phi-4 Mini
  ↓
Local API
```

is working.

---

# Inspect Context Length

There are **three different concepts** to keep separate:

```text
Model capability
        ≠
Ollama runtime context
        ≠
Your request's configured context
```

Phi-4 Mini supports a **128K-token context window**.

Inspect the model:

```bash
ollama show phi4-mini
```

Inspect the generated Modelfile:

```bash
ollama show phi4-mini --modelfile
```

Look for:

```text
PARAMETER num_ctx
```

Ollama's Modelfile documentation currently lists `num_ctx` with a default of 2048 when no other runtime/default setting applies. However, Ollama's current server behavior also sets context defaults based on available VRAM: `<24 GiB` → 4K, `24–48 GiB` → 32K, and `>=48 GiB` → 256K. Therefore, **do not assume that the model's 128K capability is automatically allocated at runtime**.

---

# Quick Debug Tips

### Is the model installed?

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

### Inspect model details

```bash
ollama show phi4-mini
```

### Inspect the Modelfile

```bash
ollama show phi4-mini --modelfile
```

### Test generation

```bash
curl http://localhost:11434/api/generate -d '{
  "model": "phi4-mini:latest",
  "prompt": "Test",
  "stream": false
}'
```


