# S1 R4.1 adversarial security review

## Review identity

- Review type: second, distinct adversarial security/code/evidence review after I-01 correction
- Implementation subject: `4e76c9f55a787ea8f51ba81d4c870743d41f9d24`
- Implementation tree: `b09c181930048c7a15b652fe2af17c46a5db5bde`
- Reviewed evidence: `docs/audits/2026-08-30/S1-package-closure-r4-1.json` and `docs/audits/2026-08-30/evidence/S1-R4-1-verification-index.json`
- Focus: cleanup skipping and precedence, cancellation, Job escape, reparse/TOCTOU, environment authority, diagnostic privacy, stale binaries, and false evidence

## Verdict

**Pass with explicit external verification blocks.** Critical: `0`. Important: `0`.

The candidate-controlled adversarial gate passes. Missing native stages, an actual staged/signed package, authoritative visual activation, a controlled GGUF model, GPU authorization, and Intel-native/performance execution remain non-passes and are correctly recorded as blockers/nonclaims.

## Corrected finding disposition

### I-01: Public OpenVINO failures retained incompletely redacted worker stderr

**Resolved.** `infrastructure/GraniteEdgeAI.OpenVino.WorkerClient/OpenVinoWorkerClient.cs:573-632` still drains the bounded stderr task and preserves the typed `StandardErrorTruncated` fact, but constructs the public `OpenVinoWorkerClientException` with `RetainedStandardError` fixed to `string.Empty`. Worker-authored content therefore cannot cross the public failure boundary.

The hostile fixture at `tests/ProcessFixtures/GraniteEdgeAI.OpenVino.ProtocolTestWorker/Program.cs:86-103` emits repeated forward-slash path, host, username, provider, and token text beyond the configured retention bound. `tests/IntegrationTests/GraniteEdgeAI.OpenVino.WorkerProcess.Tests/ProtocolContainmentTests.cs:550-565` proves that the stream is drained, truncation remains `true`, and the public retained text is empty. A fresh Release build succeeded with zero warnings/errors and the focused public-boundary regression passed `1/1` on this exact subject.

## Critical findings

None.

## Important findings

None.

## Adversarial category results

- **Cleanup skipping and precedence:** the shared cleanup coordinator attempts each owned stage independently and records bounded typed failure facts. Process-tree cleanup precedes session/handle/Job/environment cleanup where ordering affects safety; one cleanup exception does not skip later owned stages.
- **Cancellation:** caller cancellation remains distinguishable from timeout and retains the caller token when cleanup also fails. Cleanup-integrity facts are preserved without exposing raw primary exception text.
- **Job escape:** GGUF quantization creates suspended, assigns and verifies the Job before resume, then proves the Job empty. GGUF runtime and Model Inspection use creation-time Job-list assignment. No breakaway permission is enabled; hostile child coverage verifies zero residual descendants.
- **Reparse and TOCTOU:** the operation environment holds root and per-operation custody, validates final paths and file identities, rejects reparse points and alternate streams, and deletes through non-delete-sharing handles. Replacement between identity observation and custody acquisition fails closed. Entry, depth, byte, and elapsed-time limits make cleanup bounded and observable.
- **Environment authority:** production capture is a closed allowlist. Native routes receive canonical Windows roots plus private operation TEMP/TMP; only verified managed routes opt into validated .NET roots. Credentials, tokens, proxy variables, profiles, shell variables, and ambient PATH are not inherited.
- **Diagnostic privacy:** OpenVINO now publishes no worker stderr text. Model Inspection retains only bounded redacted text, while public failure messages and cleanup facts remain stable and path-free. No changed exception path echoes local roots.
- **Stale binaries:** regenerated evidence binds exact first-party bytes/SHA-256 and subject identity. The stale subjects named in the verification index are rejected, and `staleFirstPartyIdentityMatches` is zero.
- **Package closure:** the regenerated closure binds subject `4e76c9f55a787ea8f51ba81d4c870743d41f9d24` and tree `b09c181930048c7a15b652fe2af17c46a5db5bde`. It contains 200 unique evaluated members, matching the verification index. Its committed normalized size is `84447` bytes and its SHA-256 is `2e38e8da4014c489a3e1b626643e095f82e4a80969a75884f074d3579603b104`, exactly matching the durable blob identity. It explicitly states that evaluated membership is not an actual staged or signed package.
- **False evidence:** managed totals reconcile at `410 = 410 = 410 + 0 + 0`. Integration arithmetic reconciles, including the two OpenVINO failures and native-stage skips. The prior timing-sensitive Model Inspection run is disclosed, followed by focused and final full passing reruns. Package, native, visual, controlled-model, GPU, and performance blocks are not presented as passes.
- **Repository hygiene:** the correction diff passes `git diff --check`. No implementation or evidence artifact was modified by this review.

## Final reviewer statement

At immutable subject `4e76c9f55a787ea8f51ba81d4c870743d41f9d24`, tree `b09c181930048c7a15b652fe2af17c46a5db5bde`, I-01 is closed through the real public boundary. This second adversarial review finds zero Critical issues and zero Important issues. The verdict is **Pass with explicit external verification blocks**.
