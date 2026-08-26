Evaluation · MD
# Evaluation Technique
 
This project evaluates the `Overprivileged` rule (the only rule with LLM/RAG paths so
far) against a small, hand-labeled eval set, using [NUnit](https://nunit.org/) as the
test runner and a precision/recall confusion matrix as the scoring method.
 
 
## Known limitations (read this first)
 
- **n = 10 is a smoke-test-sized sample, not a statistically powered eval.** A single
  fixture flipping changes recall/precision by 10–25 percentage points. Treat pass/fail
  here as "didn't regress obviously," not as a precise accuracy measurement.
- **The RAG path (`EvaluateAsyncWithRAG`) currently predicts `isFlagged = true` for
  every file in the eval set, including all 4 benign ones (TN = 0).** Its 100% recall
  and 60% precision numbers below are a byproduct of never predicting negative, not
  evidence the rule discriminates benign from over-privileged content. **The RAG path's
  recall figure is not comparable to the LLM-fallback path's recall figure until TN > 0
  is achieved.** See [Observed run — RAG path](#observed-run--rag-path-overpriviledgedwithragtest-temperature-0)
  below for the full breakdown. This is the highest-priority thing to fix or explain
  before adding a second rule on top of the RAG path, since a miscalibrated corpus or
  similarity threshold here would likely repeat for every future rule that reuses it.
## Test framework
 
NUnit (`Test.csproj`: `NUnit`, `NUnit3TestAdapter`, `Microsoft.NET.Test.Sdk`). Each eval
"test class" is really a small benchmark harness: a `[OneTimeSetUp]`/`[SetUp]` method
seeds a throwaway SQLite + `sqlite-vec` database, runs the rule against every fixture in
the eval set, and buckets the results into a confusion matrix. The `[Test]` methods
then assert that the resulting recall/precision clear a minimum bar.
 
Two harnesses exist, exercising the two evaluation paths on `RuleBase`:
 
| Test class | Rule method under test | What it evaluates |
|---|---|---|
| `Rules/OverprivilegedWithLLMFallbackTest.cs` | `EvaluateAsync` | deterministic checks, then a straight LLM judgment on the whole document if none fire |
| `Rules/OverpriviledgedWithRAGTest.cs` | `EvaluateAsyncWithRAG` | deterministic checks, then per-chunk vector similarity against the `AST03` corpus (seeded by `VectorCorpusSeeder`), falling back to an LLM verdict only when a chunk lands ambiguously between a violation and a benign anchor |
 
Both harnesses spin up an isolated `vectest_{guid}.db` per run and delete it in
`[OneTimeTearDown]`, so runs don't share state or leak files.
 
## Eval dataset
 
`Test/EvalSet/` contains 10 synthetic skill definitions (`SKILL.md`-style files) plus a
`ground_truth.json` that pairs each filename with its expected label:
 
| Bucket | Count | Files | Expected `isFlagged` |
|---|---|---|---|
| malicious | 4 | `mal_01_full_fs_access`, `mal_02_unscoped_delete`, `mal_03_unnecessary_root`, `mal_04_persistent_broad_access` | `true` |
| benign | 4 | `ben_01_scoped_rename`, `ben_02_scoped_cache_clean`, `ben_03_scoped_font_install`, `ben_04_oneoff_resize` | `false` |
| borderline | 2 | `border_01_vague_related_scope`, `border_02_convenience_framing` | `true` |
 
Each `ground_truth.json` entry also carries a `notes` field explaining *why* it's
labeled the way it is (e.g. "requests full filesystem access for a narrow single-folder
task"), so the dataset doubles as documentation for what "overprivileged" means in this
project. The `.md` files themselves are copied to the test output directory via
`<None Include="EvalSet\**\*.*">` in `Test.csproj`, and each test loads them with
`Path.Combine(EvalSetDirectory, eval.Filename)` relative to `AppContext.BaseDirectory`.
 
The borderline cases exist specifically to exercise the RAG path's ambiguous branch —
files designed to sit close to both a known-violation and a known-benign anchor in
embedding space, forcing an LLM tie-break (see `PromptBuilder.BuildAmbiguousPrompt`)
rather than a clean nearest-neighbor match.
 
## Scoring: precision and recall
 
For every fixture, the rule's prediction (`RuleResult.IsFlagged`, or `false` if no
result came back) is compared against `ground_truth.json`'s `isFlagged` and classified
into one of four buckets (`CalculateMetrics()` in each test class):
 
- **True positive (TP)** — predicted flagged, ground truth flagged
- **False positive (FP)** — predicted flagged, ground truth benign
- **True negative (TN)** — predicted benign, ground truth benign
- **False negative (FN)** — predicted benign, ground truth flagged
From these:
 
```
Recall    = TP / (TP + FN)   // of the truly overprivileged skills, how many did we catch?
Precision = TP / (TP + FP)   // of the skills we flagged, how many were truly overprivileged?
```
 
Each harness asserts both metrics clear a minimum threshold:
 
- **Recall ≥ 0.8** — via `RecallExtensions.MeetsThreshold(recall, 0.8)`. Missing a real
  overprivileged skill is the costlier failure mode for a security rule, so recall is
  held to a higher bar.
- **Precision ≥ 0.5** — checked inline in `OverpriviledgedRuleWithRAG_PrecisionRate_OnEvalSet`.
  A lower bar reflects that some false positives (over-flagging) are an acceptable
  tradeoff against missing real violations.
Results are also written to `TestContext` (e.g. `Recall: 80.00% (TP=4, FN=1)`) so the
raw confusion-matrix counts are visible in test output, not just the pass/fail verdict.
 
Note that these thresholds check recall and precision independently — neither one
alone catches a degenerate "always flag" classifier, which would clear the recall bar
trivially and can still clear the precision bar if the benign bucket is small enough
(see the RAG path result below). A minimum specificity or TN > 0 check would be needed
to catch that failure mode directly; see [Known limitations](#known-limitations-read-this-first).
 
## Additional details
 
- **n = 10** is a smoke-test-sized sample, not a statistically powered eval — a single
  fixture flipping changes recall/precision by 10–25 percentage points. Treat pass/fail
  here as "didn't regress obviously," not as a precise accuracy measurement.
- Both harnesses call a local Ollama instance (`http://localhost:11434`) and a live
  `sqlite-vec` corpus — these are integration tests, not unit tests, and require Ollama
  running locally with the expected models pulled.
## Observed run — LLM-fallback path (`OverprivilegedWithLLMFallbackTest`)
 
Two runs are worth recording, since they isolate the effect of pinning the LLM's decode
temperature.
 
**Run 1 — default temperature (~0.8, unset).** First trustworthy run after fixing a
counter double-counting bug (`Setup()` was `[SetUp]`, re-running the full eval — and
re-incrementing the confusion-matrix fields without resetting them — before each of the
two `[Test]` methods on the shared fixture instance; switched to `[OneTimeSetUp]` to
match `OverpriviledgedWithRAGTest`):
 
```
Precision: 80.00% (TP=4, FP=1)
Recall:    66.67% (TP=4, FN=2)
 
Total Tests: 2
Passed: 1   (Precision — threshold ≥ 0.5)
Failed: 1   (Recall — threshold ≥ 0.8)
```
 
**Run 2 — `temperature: 0`**, added to `OllamaClient`'s request body (nested under
`options`, per Ollama's `/api/generate` schema — a top-level `temperature` field is
silently ignored):
 
```
Precision: 62.50% (TP=5, FP=3)
Recall:    83.33% (TP=5, FN=1)
(TN=1, FN=1, TP=5, FP=3)
 
Total Tests: 2
Passed: 2   (Precision ≥ 0.5, Recall ≥ 0.8)
Failed: 0
```
 
In both runs TP + FN = 6, matching the eval set's 6 ground-truth-positive files (4
malicious + 2 borderline) exactly, and TP+FP+TN+FN = 10, matching the file count —
confirming the confusion-matrix counters are no longer double-counted.
 
Pinning temperature to 0 flipped which threshold is at risk: recall improved (missing
only 1 of 6 over-privileged fixtures instead of 2) but precision dropped (3 false
positives instead of 1) — greedy decoding made the model lean further toward flagging
ambiguous cases as violations rather than dismissing them. Both thresholds clear in
Run 2, but this is a single sample from a 10-file set; treat the direction of the shift
(temperature 0 trades some precision for recall here) as the useful signal, not the
exact percentages, given how much a single fixture flipping moves these numbers.
 
Note **TN = 1** here — the LLM-fallback path did predict at least one file as benign,
unlike the RAG path below. Its recall/precision numbers reflect actual discrimination,
not a constant-positive classifier.
 
## Observed run — RAG path (`OverpriviledgedWithRAGTest`), `temperature: 0`
 
```
Precision: 60.00% (TP=6, FP=4)
Recall:    100.00% (TP=6, FN=0)
(TN=0, FN=0, TP=6, FP=4)
 
Total Tests: 2
Passed: 2   (Precision ≥ 0.5, Recall ≥ 0.8)
Failed: 0
```
 
TP+FP+TN+FN = 10 and TP+FN = 6, consistent with the file count and the 6 ground-truth
positives, as expected.
 
**TN = 0 is the number that matters here, not the 100% recall.** TP + FP = 10 means the
RAG path predicted `isFlagged = true` for *every single file in the set*, including all
4 benign ones. Recall is 100% only because nothing was ever predicted negative — this is
a constant-positive classifier, not a rule correctly discriminating benign from
over-privileged content. At `temperature: 0` this run's precision (60%) still clears
the 0.5 bar, but a rule that flags everything would pass this specific threshold
regardless of accuracy — the eval set doesn't yet have a check that would catch
"always flag" as a degenerate failure mode.
 
**This means the RAG path's 100% recall is not comparable to the LLM-fallback path's
83.33% recall above** — one reflects actual discrimination on 10 files, the other
reflects the absence of any negative predictions. Read them side by side as "LLM-fallback
discriminates imperfectly" vs. "RAG path doesn't yet discriminate at all on this set,"
not as "RAG path outperforms LLM-fallback on recall."
 
Worth adding a check for TN > 0 (or a minimum specificity) once the benign bucket grows
past 4 samples, and worth investigating directly before extending the RAG path to a
second rule — candidates to check first: whether `VectorCorpusSeeder` is actually
seeding benign reference examples (not just violation examples) into the corpus, and
whether the nearest-neighbor distance threshold for "unanimously benign" is too tight
to ever be satisfied in practice.
