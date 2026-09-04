# TurboVec Production-Scale Final Evaluation v2

**Status:** Approved by the user on 4 September 2026

**Campaign ID:** `turbovec-production-scale-final-evaluation-v2`

**Experiment:** `EXP-TV-COMP-001`

**Governing request:** recovered Codex attachment `f686aeba-ca70-47ea-8c5d-9c0fc04da8d3/pasted-text.txt`, 19,718 bytes, SHA-256 `b160db803175b71f6edb2b6a5beb922cc6a17bfb070f7cf800bb729119a5965a`

## Purpose and boundary

Complete the remaining TurboVec research through controlled, repeatable, fair experiments. This campaign may change only experiment harnesses, experiment adapters, deterministic corpus and query tools, PDF experiments, tests, benchmark scripts, evidence validators, research reports, reproduction documents, evidence, and experiment decision records.

It must not modify or integrate with the production WinUI frontend, XAML, application backend, model import or inspection, hardware inspection, optimisation, GGUF/OpenVINO inference, TurboQuant, chat, onboarding, settings, production PDF import, or `main`. TurboVec is retrieval-index compression, not model-weight or KV-cache quantisation.

## Preserved starting point

- Base branch: `test/ucl-turbovec-pdf-feasibility-v1`
- Base commit: `07998fb7766887689a222dd0c8726d821fa33169`
- Base tree: `ed68db942aa26c2e66218c10012c914a0e3c361e`
- Execution branch: `test/turbovec-production-scale-final-evaluation-v2`
- Canonical prior run: `EXP-TV-COMP-001-20260902T231605Z-005`
- Prior disposition: `DEMONSTRATOR_ONLY`
- TurboVec: version `1.0.0`, commit `ccab9f325e6ce2a270a87daf01ae4e443bcf2d49`, tree `0a7141836d01da61e6f3cf53b5c60916b741d811`
- Published wheel: 611,788 bytes, SHA-256 `cd855e0b318a57dc57c733f9a62ae98de5192f4f6c2c760e305523e8ceb1b090`
- Granite embedding model: `ibm-granite/granite-embedding-small-english-r2`, revision `2ab6fa8ea2d674564defd37171ae19079b864b33`, 384 dimensions
- Locked OpenVINO model tree SHA-256: `c050a0106ab7c2a77f1767f123438a206d18216d17012ed15e1bff95add636b2`

The unchanged pre-flight reproduced 39/39 Python harness tests and 5/5 direct Microsoft Testing Platform PDF tests. Diagnostic run `EXP-TV-COMP-001-20260904T025427Z-090` reproduced every prior retrieval-quality value and serialized index byte count. Its timing is diagnostic because formal machine preparation was not yet applied.

## Experiment design

At each scale, compare Exact normalized FP32 retrieval with TurboVec TQ2, TQ3, and TQ4 using one frozen embedding matrix, one frozen query matrix, identical identifiers, metadata, relevance judgements, thread settings, device, measurement code, and query ordering.

Required scales are 30, at least 1,000, and at least 10,000 genuine chunks. A 100,000-chunk scale is attempted only when the safety and resource gates show it is practical. Identical text or vectors must never be duplicated merely to increase scale.

The corpus must be deterministic, versioned, licensed or generated, and cover ordinary prose, technical material, education, non-sensitive healthcare material, headings, sections, tables, near-duplicates, semantic distractors, Unicode, long documents, and multiple PDFs where possible. Original documents, extraction output, chunks, ordering, identifiers, embeddings, development/final queries, and independently authored relevance judgements are frozen and hashed. The final evaluation set must not guide tuning.

Queries cover direct facts, paraphrases, multiple terms, near-duplicate disambiguation, multiple relevant chunks, absent answers, citation-sensitive questions, and difficult semantic matches. Ground truth is independent of every candidate result.

## Embedding control

Only the pinned Granite model and OpenVINO CPU route may generate embeddings. Generate embeddings once per frozen corpus and reuse the exact artifacts across all formats. Record model identity and artifact hashes, OpenVINO/Optimum-Intel/Transformers/tokenizer identities, dimensions, dtype, normalization, batch size, device, corpus hash, and embedding hashes. Embedding generation is a separate phase and excluded from index/query measurements unless explicitly labelled.

## PDF evaluation

Exercise valid text, multipage, headed/paragraph, table, Unicode, repeated-passage, malformed, encrypted, empty, image-only, and long PDFs, plus spaces/Unicode filenames and safely reproducible long Windows paths. Every admitted chunk preserves a source-document identifier, page number, chunk identifier, and content hash. This remains experiment-only.

## Formal measurement protocol

Before each formal block, wait at least five minutes after heavy activity, sample system state for at least 60 seconds, and record physical/available/committed/pagefile memory, experiment and system CPU, process working set, GPU/shared memory where available, power mode, AC state, top CPU/RAM processes, uptime, timestamp/timezone, and safe thermal data where available.

A block starts only with average final-minute CPU below 10%, no continuously busy unrelated process, available RAM stable within about 5%, no sustained update/download/sync/disk activity, no surviving prior workers/indexes, and at least 4 GiB available RAM where practical. Two GiB is a hard safety floor. Security controls remain enabled and unrelated processes are never forcibly terminated.

After each candidate, stop only owned processes, unload the index, remove documented experiment temporaries, wait for CPU and RAM recovery, and record the state. An unrecovered repetition is preserved and invalidated; the entire affected comparison block is rerun without cherry-picking.

Use at least five warm-up batches, at least 30 measured full-query batches where practical, and at least five independent formal repetitions. Candidate order is counterbalanced with a recorded rotating Latin square and seed. A fresh matched Exact observation is included in each repetition. Cold and warm measurements remain separate.

## Metrics

Quality: Recall@1/@5/@10, relative nDCG@10, MRR, absent-answer false-positive behaviour, source-document accuracy, and page-number accuracy.

Performance: build, save, load, cold-query, warm-query, p50/p95/p99 latency, and meaningful throughput.

Resources: equivalent serialized bytes, bytes/vector, fixed metadata, variable vector bytes, peak/baseline/incremental process memory, system RAM/commit before and after, shared GPU memory where relevant, and temporary disk use.

Lifecycle: build, query, save, unload, reload, post-reload equality, cancellation, interrupted-write handling, corrupt-index rejection, cleanup, and repeated-run stability.

Report every repetition plus median, mean, minimum, maximum, dispersion, and appropriate 95% confidence intervals. Exclusion criteria are fixed before measurement; no favourable-run selection is allowed.

Storage accounting compares equivalent information and separates vector data, index metadata, document/chunk metadata, serialization overhead, and auxiliary structures. `storage ratio = equivalent Exact bytes / equivalent candidate bytes`; below one means larger than Exact.

## Acceptance gates

At a genuine scale of at least 10,000 chunks, Gate A requires all of:

- lifecycle completion;
- Recall@10 at least 0.90;
- relative nDCG@10 at least 0.95;
- matched p95 slowdown no more than 1.10;
- storage ratio at least 2.00;
- save/reload integrity, corruption rejection, and deterministic cleanup;
- no unresolved Critical or Important security defect;
- no evidence-arithmetic violation.

Gate B additionally requires source/citation accuracy at least 0.95, correct page provenance, no material absent-answer regression, successful mixed-PDF retrieval, cancellation/recovery, rejection of incomplete indexes, reproducibility, and acceptable memory without unsafe paging. Generated-answer quality is explicitly not evaluated unless a later authorized experiment adds it.

## Upstream verification

Reconcile 31/31 versus 39/39 harness history, rerun the 111/111 repository baseline, PdfPig tests, the upstream Python suite, and the Windows long-path failure. Run Rust tests and Clippy with the exact Rust 1.89 toolchain when safely available; user-scoped official Rust installation is authorized. Every experiment correction follows RED, minimum fix, GREEN, and related regression verification.

## Evidence and disposition

Every command records discovered, attempted, executed, passed, failed, skipped, blocked, and unexecuted states with reconciling arithmetic. Formal runs preserve identity, command, order, seed, machine snapshots, dependencies, hashes, stdout/stderr, exit code, raw/processed measurements, exclusions, and cleanup outcomes.

The final disposition is exactly one of `INTEGRATE_CANDIDATE`, `DEMONSTRATOR_ONLY`, `EXCLUDE`, or `BLOCKED`. Integration is never performed by this campaign. All twenty deliverables named in the governing request must be produced, independently reviewed, committed only within scope, pushed without force, and verified against the advertised remote before handoff.
