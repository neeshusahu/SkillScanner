<!-- Benchmark run: parallel-run. Up to four files execute concurrently. -->

# Parallel LLM Benchmark

`parallel-run`

## Verdict: Unsuccessful

The scan completed, but the parallel strategy is not usable with the current 20-second Ollama timeout: **102 of 110 requests timed out (92.7%)**. Only 8 responses completed, so the resulting findings are not representative of a full scan.

## Total LLM Calls and Time

| Metric | Value |
| --- | ---: |
| Total LLM calls attempted | **110** |
| Completed calls | **8** (7.3%) |
| Timed-out calls | **102** (92.7%) |
| Flagged completed responses | **2** |
| Unflagged completed responses | **6** |
| Total LLM time, summed across calls | **2,177.28s** (**36m 17.3s**) |
| Time spent in timed-out calls | **2,043.14s** (**34m 3.1s**) |
| Total wall-clock time | **597.04s** (**9m 57.0s**) |
| Average time per attempted call | **19.79s** |
| Fastest completed call | **10.96s** |
| Slowest completed call | **19.96s** |
| Longest timeout | **20.82s** |
| Configured timeout | **20s** |

> **Note on "time spent in timed-out calls":** this figure is app-side elapsed time before the `CancellationToken` gave up waiting — it is not necessarily wasted model compute. Ollama may have continued processing (or queueing) those requests server-side after the client abandoned them. The 2,043.14s figure should be read as "time our app waited," not "time the model spent computing." Confirming actual server-side behavior (e.g. via `ollama ps` or server logs during a run) would clarify whether queued requests were still consuming resources after client timeout.

## Parallelism Analysis

`ScanParallel` uses `MaxDegreeOfParallelism = 4`, while each file runs its ten evaluations sequentially. The run therefore issued up to four Ollama requests at once.

The total summed call time was **3.65×** the wall-clock duration, showing real overlap — the concurrency was genuinely happening at the app level. However, the high overlap came with severe contention: nearly every request reached the timeout ceiling. The likely cause is that the local Ollama model cannot serve four concurrent generations quickly enough; queued requests expire before receiving a response.

This should be confirmed against Ollama's own concurrency ceiling before further tuning: check `OLLAMA_NUM_PARALLEL` (`echo $OLLAMA_NUM_PARALLEL` / `launchctl getenv OLLAMA_NUM_PARALLEL`), or empirically test with two concurrent `curl` calls to `/api/generate` and compare latency to a single call. If Ollama is already serializing internally at 1, app-level concurrency of 2+ will reproduce this same failure at smaller scale — the fix is not "try fewer concurrent files," it's "match app-level concurrency to Ollama's real capacity, which may be 1."

## Per-File Elapsed Time

Each file was configured for 10 evaluations. The console output interleaves concurrent request logs, so it cannot reliably assign each individual completion or timeout to a specific file. These durations are per-file wall-clock elapsed times, not sums of that file's LLM call durations.

| File | Configured Calls | Elapsed Time |
| --- | ---: | ---: |
| `File_01-.md` | 10 | 189.39s |
| `File_02.md` | 10 | 193.08s |
| `File_03.md` | 10 | 195.44s |
| `File_04.md` | 10 | 200.10s |
| `File_05.md` | 10 | 200.18s |
| `File_06.md` | 10 | 200.35s |
| `File_07.md` | 10 | 201.44s |
| `File_08.md` | 10 | 200.09s |
| `File_09.md` | 10 | 197.40s |
| `File_10.md` | 10 | 200.33s |
| `File_11.md` | 10 | 200.14s |

> **Known instrumentation gap:** per-file success/timeout breakdown is not currently measurable — logging lacks a file identifier on each request line, so concurrent output cannot be reliably attributed. This is the priority fix before the next benchmark run: tag every log line (`[filename]` prefix or similar) so per-file completion/timeout rates can be reported directly instead of inferred from wall-clock elapsed time alone.

## Confounding Factor: Repeat-Evaluation Loop

Each file's "10 evaluations" reflects a `Range(1, 10)` loop that re-runs the same rule set 10 times per file, not 10 distinct sub-rule checks. This inflates total LLM calls 10× over what a single well-formed evaluation per rule would require, independent of the concurrency question. Removing or replacing this loop (single call, or a smaller majority-vote scheme e.g. 3 calls) would reduce total call volume and wall-clock time regardless of whether concurrency is fixed, and should be addressed before or alongside the concurrency work below.

## Recommendation

Do not treat this benchmark as a successful performance improvement. Before the next run:

1. **Confirm Ollama's real concurrency ceiling** (`OLLAMA_NUM_PARALLEL`, or empirical 2-concurrent-curl test) rather than assuming app-level concurrency of 1 or 2 is safe.
2. **Add per-file log tagging** so the next benchmark can report real per-file success/timeout rates, not just elapsed time.
3. **Address the ×10 repeat-evaluation loop** — this is likely a bigger lever on total runtime and call volume than concurrency tuning will ever be.
4. Only after 1–3, revisit concurrency: limit concurrent Ollama requests to the model's proven capacity — start with **one**, then test **two** — or increase the timeout only after measuring throughput and queueing behavior at that confirmed capacity.
