# Sample Output — `dotnet run -- scan Skills/`

A real run of the CLI against 11 unmodified Azure documentation skill files
(`Skills/File_01.md`–`File_11.md`), exercising the actual `Scanner.Scan` →
`EvaluateAsyncWithRAG` path end to end (not the eval set — real, previously-unseen
content).

## Generated report (excerpt)

```
Skill Scanner Report
File Path: Skills/File_04.md
====================
Generated on: 8/21/2026 5:51:15 PM

Rule: Overprivileged
Message: Check following content: Azure Key Vault
Treat every secret, key, and certificate as sensitive.
[List]
1. Identify the vault, object type, caller identity, and required operation.
2. Review RBAC, access policies, network restrictions, and expiration settings.
3. Recommend least-privilege permissions and managed identities over static credentials.
4. Plan rotation and validation without exposing secret values.
Never print, copy, or store secret material in logs, source files, or chat.

Severity: High

File Path: Skills/File_10.md
====================
Generated on: 8/21/2026 5:51:15 PM

No rule results to report.
```

(`File_08`, `File_09`, `File_10`, `File_11` all resolved to "No rule results to report." — the
other 7 files were flagged, one `Rule:`/`Message:`/`Severity:` block each.)

## Why so many false positives (7 of 11 files flagged)

The flagged files include skills that are clearly scoped and well-behaved by design —
`File_01` (Azure Reliability: read-only assessment, requires approval before touching
production), `File_02` (AKS diagnosis: explicitly avoids cluster-wide changes, never
exposes secrets), `File_03` (App Insights: read-only telemetry analysis with privacy
safeguards), `File_04` (Key Vault: explicitly *forbids* printing/storing secret
material), `File_06` (Service Health: read-only impact assessment), `File_07` (Azure
Monitor: requires explicit approval before changing alerts). None of these request
elevated access, persistence, or broad scope — yet all seven were flagged as
`Overprivileged`. Two causes compound here:

**1. A missing symmetry check in the vote logic (`RuleBase.cs`, `EvaluateWithVectorSimilarityAndLLM`):**

```csharp
var allBenign    = similarChunks.All(chunk => chunk.RuleId==null && chunk.Distance<=0.5);
var allViolation = similarChunks.All(chunk => chunk.RuleId==RuleType.Id);
```

`allBenign` only counts as a real match when the distance is ≤ 0.5. `allViolation` has
**no distance check at all** — if the 3 nearest corpus neighbors merely happen to all
carry `RuleId == 1` (the AST03 violation rule), the chunk is declared a "Unanimous match
against violation reference examples" and flagged immediately, skipping the LLM
tie-break entirely, no matter how far those neighbors actually are in embedding space.

In every flagged file above, the reported distances were **0.44–0.59** — worse (farther)
than the 0.5 cutoff `allBenign` itself requires to count as a real match. These are weak,
borderline retrievals, not confident nearest-neighbor hits, but the code path that
decided them treats "all three happen to share a label" as equivalent to "all three are
actually close."

**2. A thin, generically-worded reference corpus.** The seeded `TextCorpus` violation
examples are short, generic statements like *"Request administrator or root privileges
for a task that does not require elevated access"* or *"Keep an elevated access session
active between invocations."* Real Azure documentation skills talk about permissions,
RBAC, and access constantly — because they're often skills *about* securing Azure
resources — so their embeddings land in the same general neighborhood as those violation
examples even when the actual content is the opposite (e.g. `File_04` explicitly telling
the agent to protect secrets, not request broader access to them). With no benign
reference examples that are similarly specific to "discussing permissions/security
correctly," there's nothing close enough to those chunks to pull the vote the other way.

Together: weak retrieval (nothing in the corpus is a *close* match to this kind of
content) plus a vote rule that doesn't require closeness for the violation branch means
mediocre-but-unanimous matches get treated as confident violations with no LLM
second opinion. Files that escaped flagging (`File_08`, `File_09`, `File_10`, `File_11`)
did so only because their 3 nearest neighbors happened to include a mix of RuleId values
(not unanimous) — chance, not because their content was meaningfully more or less
over-privileged than the flagged files.

### Fix direction

- Add the same distance cutoff to `allViolation` that `allBenign` already has, so a
  "unanimous" vote requires the matches to actually be close, not just same-labeled.
- Expand the benign side of the corpus with examples that specifically discuss
  permissions/access/security *correctly* (scoped, read-only, approval-gated), not just
  unrelated benign snippets — so legitimately security-conscious content has something
  close to anchor to.



## Run 3 — After adding threshold distance


```
File Path: .../Skills/File_05.md
====================
Rule: Overprivileged
Message: Check following content: Azure Sre Agent Skill
This skill provides expert guidance for Azure Sre Agent. Covers troubleshooting, best
practices, decision making, security, configuration, integrations & coding patterns,
and deployment. It combines local quick-reference content with remote documentation
fetching capabilities.
[... Category Index / Security / Configuration / Integrations / Deployment tables ...]
Severity: High

File Path: .../Skills/File_08.md
====================
No rule results to report.

File Path: .../Skills/File_09.md
====================
No rule results to report.

File Path: .../Skills/File_02.md
====================
Rule: Overprivileged
Message: Check following content: Azure Kubernetes Service
Diagnose AKS workloads with the smallest safe scope.
[List]
1. Identify the cluster, namespace, workload, and failure time window.
2. Inspect status, events, resource limits, networking, and recent rollout changes.
3. Separate application failures from cluster or platform failures.
4. Propose a rollback, scaling, or configuration change with validation steps.
Avoid cluster-wide changes and never expose Kubernetes secrets in diagnostic output.
Severity: High

File Path: .../Skills/File_06.md
====================
No rule results to report.

File Path: .../Skills/File_07.md
====================
No rule results to report.

File Path: .../Skills/File_03.md
====================
No rule results to report.
```

Same distance-threshold fix as Run 2, but **`File_09` (Azure Policy) flipped from
flagged to clean** compared to Run 2 — with `File_02` and `File_05` still flagged
(status of `File_01`, `File_04`, `File_10`, `File_11` not visible in this excerpt, which
was truncated at the top). Everything else matches Run 2's result exactly.

The likely explanation isn't the code — the threshold logic is unchanged from Run 2 —
it's that the corpus itself was clean this time. Run 2's `File_09` match set included
duplicate `TextId`s for the same violation phrase (from the unfixed seeder bug), which
means that run's "3 nearest neighbors" carried more weight toward the duplicated
violation phrase than they should have. With a clean, non-duplicated corpus, `File_09`'s
actual 3 nearest neighbors were different enough to break unanimity, sending it through
the LLM tie-break, which then correctly judged it as not over-privileged.

This is the concrete cost of the still-open seeder bug: whether a given scan gets a
clean corpus or a duplicate-contaminated one currently depends on whether someone
happened to delete `skillscanner.db` beforehand, and that has now visibly changed a
verdict (`File_09`) between two runs with identical rule logic. A security linter's
output shouldn't depend on incidental local state like that — fixing the seeder guard
(see above) would make results reproducible run to run, independent of the threshold fix.
