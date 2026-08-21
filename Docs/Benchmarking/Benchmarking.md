# Sequential vs. Parallel Scan Benchmark

Both runs scanned the same 11-file `azure-sre-agent` corpus through the `Overprivileged`
rule's LLM fallback path (`EvaluateAsync`), simulating 10 rule evaluations per file
(`Enumerable.Range(1, 10)` in the commented-out `ScanParallel`, standing in for a future
10-rule rule set). That's **11 files × 10 "rules" = 110 LLM calls** either way — the two
runs differ only in how those 110 calls are scheduled against Ollama.

## Results

| Metric | Sequential (`Sequential.md`) | Parallel, `MaxDegreeOfParallelism = 4` (`ParallelOuput.md`) |
| --- | ---: | ---: |
| Total LLM calls | 110 | 110 |
| Completed (non-timeout) | **108** | **2** |
| Timed out (20s client timeout) | 2 | **102** |
| Unflagged (default on failure) | 68 | 6 |
| Wall-clock time | **21m 22s** | **9m 57s** |
| Effective concurrency | 1 (serial) | up to 4 files × 10 calls = **40 in-flight** |

Parallel finished in under half the wall-clock time — but at the cost of 102 of 110
calls timing out. Only 2 calls actually got a real model response; everything else fell
back to the default unflagged/failed result. The sequential run, by contrast, completed
108/110 calls and only timed out twice (both on the same large `Skill.md` file). Raw
throughput improved, but correctness collapsed — the parallel run is not a usable
tradeoff as written.

## Why parallel makes it worse, not just faster

Ollama's HTTP API can serve **one generation request at a time** per model instance;
concurrent requests queue up server-side rather than running concurrently. `ScanParallel`
sets `MaxDegreeOfParallelism = 4` at the *file* level, and each file then issues calls
for every rule (10, in this simulation) with no additional gating. That means the actual
fan-out of concurrent LLM calls is:

```
files in flight × rules per file = concurrent LLM calls
        4        ×      10       =        40
```

Worst case — if `MaxDegreeOfParallelism` were raised to process all 10 eval files at
once — that's **10 × 10 = 100 concurrent calls** hitting a backend that can only run one
at a time. The other 39 (or 99) calls in the log above sit queued behind Ollama's single
worker, and because each call has its own fixed 20s client-side timeout
(`OllamaClient`), most of them time out waiting in that queue before Ollama ever gets to
them — which is exactly the timeout cascade visible in `ParallelOuput.md` starting at
line 36. Parallelizing the *file processing* doesn't parallelize the *LLM backend*; it
just creates a queue longer than the timeout can tolerate.

## Proposed fix: rate-limit LLM calls independently of file/rule concurrency

The file-level and rule-level work (parsing, lookups) can stay
parallel — that part isn't bottlenecked by Ollama. Only the actual LLM inference call
needs to be serialized to match what Ollama can really do concurrently. The fix is to
gate calls at the LLM client itself, not at the file loop:



