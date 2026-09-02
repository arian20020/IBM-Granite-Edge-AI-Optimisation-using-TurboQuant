# TurboVec PDF Knowledge Library Design

**Status:** Approved design; implementation and release remain conditional on the feasibility gate

**Date:** 2 September 2026

**Owner:** C0 coordinator

**Related decision:** `docs/architecture/decisions/ADR-TurboVec.md`

**Related evaluation:** `EXP-TV-COMP-001`

**Approved source revision:** `06e3deaa7cd7ef2effa3d5ca572c25656b4c9b69`

## 1. Purpose

Add an optional, entirely local knowledge-library workflow that imports selectable-text PDF, plain-text, and Markdown documents; creates Granite embeddings; retrieves cited passages; and supplies the same bounded context to either the GGUF or OpenVINO chat route.

TurboVec is an evidence-gated compact retrieval index. It is not a model-weight, GGUF, OpenVINO, or KV-cache optimisation. Failure or exclusion of TurboVec must not prevent document retrieval through the exact FP32 index and must not prevent normal model setup or chat.

## 2. Decisions

1. Keep the existing five-stage model setup unchanged.
2. Present knowledge import as an optional Knowledge library reached from Ready to chat.
3. Share one knowledge subsystem across GGUF and OpenVINO generation routes.
4. Support PDF files containing selectable text, UTF-8 TXT, and UTF-8 Markdown in the first release.
5. Detect image-only or scanned PDFs and report that OCR is unavailable; do not silently create an empty library.
6. Use an exact normalized-FP32 cosine index as the correctness oracle and production fallback.
7. Evaluate TurboVec 2-bit, 3-bit, and 4-bit configurations against the exact index before application integration.
8. Expose `Exact retrieval` and `Compact retrieval` to users. Do not expose TurboVec bit widths in the primary UI.
9. Keep parsing, embedding, and TurboVec native execution outside the WinUI process in verified child workers.
10. Use observed backend milestones only. Do not simulate progress or advance a stage before its durable output is verified.

## 3. Controlled dependencies

The feasibility campaign starts from these candidates:

| Purpose | Candidate | Controlled identity |
|---|---|---|
| Compact index | `RyanCodrai/turbovec` | version `1.0.0`; commit `ccab9f325e6ce2a270a87daf01ae4e443bcf2d49`; tree `0a7141836d01da61e6f3cf53b5c60916b741d811`; MIT |
| Windows Python feasibility wheel | `turbovec-1.0.0-cp39-abi3-win_amd64.whl` | SHA-256 `CD855E0B318A57DC57C733F9A62AE98DE5192F4F6C2C760E305523E8CEB1B090` |
| PDF extraction | `UglyToad/PdfPig` | stable release `0.1.15`; annotated tag `fdd933084ccf064cfcb309c91c1237c9a3e9430d`; release commit `f131f642976936e06ee91cb19d3ed728f9dd18b6`; Apache-2.0 |
| Embeddings | `ibm-granite/granite-embedding-small-english-r2` | 47M parameters; 384 dimensions; Apache-2.0; exact model revision and every deployed artefact hash must be frozen before the first formal run |
| Embedding runtime | OpenVINO GenAI `TextEmbeddingPipeline` | reuse the repository's approved OpenVINO version only if its text-embedding API is present; otherwise stop and record a versioned dependency-change decision |

The Python TurboVec wheel is permitted only for the isolated feasibility campaign. A passing production implementation uses a pinned Rust worker unless a later reviewed decision demonstrates that packaging a Python runtime is safer and smaller. No floating package version, mutable model branch, or download-at-runtime dependency is permitted.

## 4. Scope

### 4.1 First-release capabilities

- Create, rename, inspect, rebuild, and delete one or more local knowledge libraries.
- Import `.pdf`, `.txt`, and `.md` files through the Windows file picker or drag and drop.
- Preserve an app-managed source snapshot after successful admission.
- Extract text with PDF page provenance.
- Create deterministic chunks and stable chunk identifiers.
- Generate normalized document and query embeddings locally.
- Build and query the exact FP32 index.
- Enable the TurboVec index only if the feasibility and production gates pass.
- Retrieve top-k cited passages and provide one route-neutral context payload to either chat backend.
- Cancel import, embedding, indexing, rebuild, and query operations.
- Remove a document and all of its derived data.
- Recover or safely discard incomplete staging work after restart.

### 4.2 Explicit exclusions

- OCR and image understanding.
- DOCX, web pages, cloud drives, email, audio, and video.
- Cloud embeddings, cloud retrieval, telemetry containing document content, and network listeners.
- Cross-device library synchronisation.
- Automatic modification of the selected model or its optimisation settings.
- A claim that local application data is encrypted at rest. First-release protection is the current Windows-user profile boundary and restrictive file ACLs.
- Secure erasure claims for SSD-backed storage; deletion means normal filesystem deletion followed by absence checks.

The first embedding configuration is English-only. Non-English text may be imported, but the UI must disclose that retrieval quality is not validated for it. Multilingual embedding support requires a new model identity, matched evaluation, and reindexing contract.

## 5. Placement and user journey

The model journey remains:

```text
Choose model -> Inspect model -> Check hardware -> Optimise model -> Ready to chat
```

Ready to chat may show a skippable `Add knowledge` action. It opens the separate Knowledge library; it is not a sixth model-optimisation stage.

The import journey is:

```text
Select files
  -> Snapshot
  -> Extract and validate
  -> Chunk
  -> Embed
  -> Build exact index
  -> Optionally build compact index
  -> Reopen and verify
  -> Publish atomically
  -> Ready
```

The chat journey is:

```text
Question
  -> Granite query embedding
  -> selected retrieval index
  -> validated chunk IDs
  -> bounded cited context
  -> GGUF or OpenVINO chat
  -> answer with source citations
```

Knowledge use is opt-in for each conversation. When it is off, the chat route receives no retrieved document context.

## 6. Component boundaries

### 6.1 Knowledge Library UI

Displays libraries, documents, real operation stages, errors, citations, reindex state, and delete controls. It owns no parsing, embedding, vector-search, worker-launch, or filesystem-custody logic.

### 6.2 `KnowledgeLibraryCoordinator`

Owns the persisted state machine, operation IDs, cancellation, staged publication, restart recovery, and composition of the services below. Only one mutation may publish to a library at a time. Queries may continue against the previous published generation during a rebuild.

### 6.3 `DocumentImportService`

Validates the selected extension and initial metadata, creates a read-only snapshot, calculates SHA-256, assigns non-path-derived identifiers, invokes the extraction worker, and commits the managed source only after extraction succeeds.

### 6.4 Verified document extraction worker

A separate .NET worker hosts the pinned PDF parser and the TXT/Markdown decoders. It receives verified staged paths through the repository's existing protected-worker pattern and returns structured metadata and extracted page records over bounded standard I/O. It cannot open a port and runs under a Windows job object so descendants are terminated with the worker.

### 6.5 `ChunkingService`

Produces deterministic chunks from extracted page records. Version `granite-chunker-v1` uses paragraph-aware boundaries, a target of 512 embedding-model tokens, a hard maximum of 768 tokens, and an overlap of 64 tokens. A chunk may span at most two adjacent PDF pages. Empty and whitespace-only chunks are discarded.

The stable chunk ID is the lowercase hexadecimal SHA-256 of:

```text
document-content-sha256 + "\n" + chunker-version + "\n" + ordinal + "\n" + normalized-chunk-text
```

### 6.6 Verified embedding worker

A dedicated OpenVINO worker owns `TextEmbeddingPipeline`, the frozen Granite embedding artefacts, tokenisation, document/query instructions, batching, and L2 normalisation. The first contract accepts at most 16 chunks or 12,288 aggregate input tokens per embedding request and returns exactly 384 finite FP32 values per input.

### 6.7 `IRetrievalIndex`

The route-neutral application boundary exposes create, upsert, remove, search, save, reopen, verify, and dispose operations. Search returns stable chunk IDs and finite scores, never document text.

`ExactVectorIndex` stores normalized FP32 vectors and ranks by dot product. It is the formal oracle and fallback.

`TurboVecIndexWorker` wraps TurboVec `IdMapIndex` in a pinned Rust executable. Versioned newline-delimited JSON control messages use unique request IDs, a 2 MiB maximum line length, batches of at most 256 vectors, explicit terminal responses, and bounded error records. The worker may not receive document text or original user paths.

### 6.8 `RetrievalContextBuilder`

Resolves returned chunk IDs against the committed metadata store, rejects unknown or deleted IDs, removes duplicate passages, preserves rank and page citations, and emits a route-neutral context payload. Both chat backends receive identical serialized context bytes for the same library generation, question embedding, and retrieval result.

## 7. Data and storage

Each library generation is stored below the app's current-user local application-data root using a random 128-bit library ID and short fixed filenames. User filenames never become internal filenames. A generation contains:

- an ACL-restricted managed source directory;
- a manifest;
- extracted page records;
- chunk records and citation metadata;
- normalized FP32 embeddings;
- one selected serving index: either normalized FP32 vectors for Exact or a TurboVec index and sidecar for Compact;
- hashes and byte counts for every committed artefact.

The manifest binds:

- schema and generation versions;
- source hashes and safe display names;
- extractor identity;
- chunker identity and parameters;
- embedding model repository, revision, artefact hashes, dimension, normalisation, and instruction version;
- retrieval implementation, version, commit, bit width, and index hashes;
- chunk-ID set hash;
- creation time and publication state.

An index whose manifest, dimension, model identity, chunk-ID set, byte count, or hash does not match is incompatible and cannot be queried.

Embedding batches exist in staging while an index is built. An Exact generation commits the normalized FP32 vectors as its serving index. A Compact generation commits only the verified TurboVec serving index and removes the transient FP32 embedding artefact before publication. The source snapshot, extracted pages, and deterministic chunks remain available for an explicit rebuild to Exact. The application never counts a hidden retained FP32 index as a Compact storage saving.

Default admission limits are 100 MiB per source file, 1,000 pages per PDF, 20 million extracted Unicode scalar values per document, 50,000 chunks per library, and 120 seconds for extraction of one document. Limit failures are explicit and do not publish partial content. These are application safety limits, not claims about dependency capacity.

## 8. Persisted lifecycle

An import or rebuild uses these states:

```text
Selected -> Snapshotting -> Extracting -> Chunking -> Embedding
         -> Indexing -> Verifying -> Ready
```

Terminal alternatives are `Cancelled`, `Failed`, and `NeedsReindexing`.

- A stage advances only after its backend output passes structural and integrity validation.
- UI progress is derived from observed completed documents, pages, chunks, or batches. No timer synthesizes percentage progress.
- Cancellation stops new work, propagates to the active worker, closes pipes, terminates the job if the grace period expires, and removes staging artefacts.
- The cancellation grace period is five seconds; surviving owned processes are a test failure.
- Restart recovery never promotes an incomplete generation. It offers retry from immutable admitted sources or cleanup.
- Publication uses a staging generation, persisted manifest, reopen-and-verify pass, and one atomic current-generation pointer replacement.
- The prior valid generation remains queryable until the replacement publishes.
- An embedding-model, instruction, dimension, chunker, or source-content change produces `NeedsReindexing`; incompatible vectors are never mixed.

## 9. PDF and text admission

PDF extraction uses page order and layout-aware word extraction sufficient to produce readable text and page citations. The worker distinguishes:

- password-protected PDF;
- malformed or unsupported PDF;
- image-only/scanned PDF;
- page, byte, text, memory, or time limit exceeded;
- extraction cancellation;
- extraction worker unavailable, unverifiable, crashed, or inconsistent.

A PDF is classified as image-only when it has at least one page and no page yields meaningful selectable text after whitespace normalization. Mixed PDFs are accepted when at least one page contains meaningful text; pages with no text remain represented so page numbering stays correct.

TXT and Markdown inputs must decode as strict UTF-8, with an optional UTF-8 BOM. NUL-containing or binary-like inputs fail admission. Markdown is treated as text; the first release does not fetch linked resources or execute embedded HTML, scripts, or directives.

## 10. Retrieval and prompt safety

- Default retrieval uses `top-k = 8`.
- Retrieved context consumes at most 25% of the selected chat route's usable context window and never more than 4,096 tokens.
- A whole chunk is omitted rather than truncated when it would exceed the remaining retrieval budget, except that the highest-ranked chunk may be safely clipped with its citation preserved.
- Document content is delimited as untrusted reference material. It cannot modify system policy, tool permissions, worker configuration, or application settings.
- Citations carry library generation, document ID, safe display name, page range where applicable, and chunk ID.
- Unknown, stale, duplicate, non-finite, out-of-range, or deleted results fail validation before prompt construction.
- If compact retrieval fails before prompt construction, the UI identifies it as unavailable and offers an explicit rebuild using Exact retrieval. It does not silently search a hidden exact index. The audit record must state which implementation actually produced the results.

## 11. User-visible behavior

The primary library view provides:

- import PDF/TXT/Markdown;
- document list with indexing state and page/chunk counts;
- cancel, retry, reindex, and delete;
- retrieval mode: `Exact` or `Compact` when Compact has qualified;
- honest storage and last-indexed information;
- a clear English-only retrieval disclosure;
- explicit scanned-PDF guidance without implying OCR occurred.

The UI must not display TurboVec bit widths in the normal flow. Diagnostics may show dependency versions, index identity, actual retrieval mode, and safe failure codes without source paths or content.

Frontend connection occurs only after the active frontend campaign publishes an approved checkpoint. Backend development must not edit the frontend worker's XAML surfaces; reconciliation uses a separate reviewed change.

## 12. Privacy and security

- Processing is local and no component opens a network listener.
- Runtime downloading of parsers, models, native libraries, or indices is prohibited.
- Executables and model artefacts are resolved from approved installation roots and verified before use.
- Workers inherit the repository's job-object, bounded-pipe, timeout, cancellation, environment, and terminal-consistency protections.
- Document text, embeddings, prompts, retrieved passages, private paths, and raw worker output are excluded from telemetry, normal logs, test summaries, and published evidence.
- Tests and committed evidence use generated or redistributable fixtures only.
- Internal storage uses restrictive current-user ACLs. The release documentation states that library content is not separately encrypted at rest.
- Delete removes the source snapshot, extracted pages, chunks, embeddings, all index forms, manifest, and staging remnants, then verifies absence. No secure-erasure claim is made.

## 13. TurboVec feasibility and decision gates

`EXP-TV-COMP-001` compares exact FP32 with TurboVec 2-bit, 3-bit, and 4-bit using identical admitted documents, extracted text, chunking, normalized embeddings, queries, top-k, hardware, power state, warm-up policy, and repetitions.

### 13.1 Gate A: feasibility and permission to implement

Gate A is executed before a production TurboVec worker or application integration exists. TurboVec qualifies for a production implementation plan only if one configuration satisfies every Gate A criterion:

| Measure | Qualification threshold |
|---|---|
| Recall@10 against exact | at least `0.90` |
| nDCG@10 relative to exact | at least `0.95` |
| Stored serving-index size | TurboVec index plus required sidecars is at least 2x smaller than the matched Exact normalized-FP32 serving index; shared documents, chunks, and transient build files are excluded from both sides |
| Search p95 | no more than 10% slower than exact |
| Candidate correctness and lifecycle | zero crash, hang, silent corruption, ID mismatch, save/load mismatch, or unreported fallback in the controlled Windows experiment |

At Gate A, `INTEGRATE` means only that a production implementation plan is permitted. It is not a product, packaging, or release approval.

### 13.2 Gate B: production and release qualification

After Gate A returns `INTEGRATE`, the separately approved production implementation must satisfy every Gate B criterion before Compact retrieval is exposed:

| Measure | Qualification threshold |
|---|---|
| Citation correctness | no more than 2 percentage points below Exact on the same application corpus and queries |
| Grounded-answer usefulness | no more than 2 percentage points below Exact through both GGUF and OpenVINO chat routes |
| Production lifecycle | zero crash, hang, leaked process, silent corruption, stale publication, or unreported fallback |
| Deployment | passes normal Windows packaging, executable/model verification, and Application Control gates without weakening policy |
| Application quality | passes cancellation, deletion, reindex, restart recovery, privacy, accessibility, and route-neutral context-identity gates |

The formal result is one of:

- `INTEGRATE`: the best Gate A configuration is eligible for a separately approved production plan; it does not become user-visible until Gate B passes.
- `DEMONSTRATOR_ONLY`: TurboVec runs and evidence is valid, but no configuration satisfies every production threshold.
- `EXCLUDE`: an essential correctness, security, compatibility, or deployment gate fails reproducibly.
- `BLOCKED`: prerequisites or external policy prevent a fair run; no pass/fail performance claim is made.

If no configuration qualifies at Gate A, no TurboVec production worker is built. If Gate A passes but Gate B fails, the product retains Exact retrieval and does not ship or expose Compact retrieval. The feasibility report and reproducible evidence remain valid project results.

## 14. Verification strategy

### 14.1 Provenance and upstream tests

- Verify repository, tags, commit, tree, licence, dependency locks, source archives, wheel/crate hashes, and advisory results.
- Run locked Rust tests with Rust 1.89 after explicit toolchain approval.
- Exercise release and debug builds plus forced scalar, AVX2, and available AVX-512 paths.
- Build the Python wheel from the pinned source and compare it with the published-wheel campaign.
- Preserve upstream skips and failures honestly; do not treat CI results as local execution.

### 14.2 Document fixtures

Generated fixtures cover valid single/multipage PDFs, mixed text/image pages, Unicode, columns, empty pages, encrypted PDFs, malformed structures, nested objects, image-only PDFs, oversized inputs, long paths, timeout, cancellation, worker crash, strict UTF-8, BOM, NUL, and binary masquerading.

### 14.3 Determinism and citations

Repeated import produces identical normalized page text, chunks, chunk IDs, embeddings within the pinned runtime tolerance, and citations. Page numbers and source identities survive save/load and cannot cross document or generation boundaries.

### 14.4 Worker contracts

Contract tests cover handshake, version mismatch, bounded messages, malformed JSON, wrong dimensions, non-finite values, duplicate IDs, remove, save/load, corruption, cancellation, timeout, parent death, stderr bounds, cleanup, and zero network listeners.

### 14.5 Retrieval evaluation

The controlled corpus contains project-authored PDF, TXT, and Markdown sources with adjudicated relevance and answer/citation keys. Runs record storage bytes, peak resident memory, build time, query p50/p95, recall@10, nDCG@10, citation correctness, answer usefulness, failures, actual SIMD path, and actual retrieval implementation.

### 14.6 Application end to end

The same library generation and retrieved-context hash are exercised through GGUF and OpenVINO chat. Tests cover import, opt-in knowledge use, cited response, cancellation, delete, reindex, restart recovery, exact fallback, Compact disclosure, packaging, App Control, keyboard access, text scaling, screen-reader names, no network, and zero remaining owned processes.

Every defect corrected during implementation receives a regression test that fails on the defective behavior and passes on the correction.

## 15. Sequencing and change control

1. Preserve the approved C0 and frontend checkpoints.
2. Update the deferred TurboVec decision records to reference this reactivation design without claiming integration.
3. Execute dependency provenance, Windows source/wheel, PDF-parser, and embedding-runtime feasibility in isolated worktrees.
4. Implement the exact retrieval baseline and controlled evaluation harness before a production TurboVec adapter.
5. Run `EXP-TV-COMP-001` and publish one of the four formal decisions.
6. Stop product integration for `DEMONSTRATOR_ONLY`, `EXCLUDE`, or `BLOCKED`.
7. For `INTEGRATE`, implement the verified Rust worker and production coordinator behind `IRetrievalIndex`.
8. Connect the completed backend to the separately approved frontend checkpoint.
9. Run unit, contract, native, package, security, accessibility, visual, performance, and two-route acceptance gates.
10. Merge only after independent review and evidence validation.

Each stage uses append-only evidence. Failed, blocked, superseded, and inconclusive runs remain preserved. No worker may modify or merge `main` without a separate C0 decision.

## 16. Claim boundary

Approval of this design authorizes preparation of an implementation plan. It does not prove TurboVec suitability, PDF safety, embedding-runtime compatibility, application integration, packaging, performance, accessibility, or release readiness. Those claims require the matching executed gates and preserved evidence.

## 17. Primary references

- TurboVec repository: <https://github.com/RyanCodrai/turbovec>
- TurboQuant paper: <https://arxiv.org/abs/2504.19874>
- PdfPig repository: <https://github.com/UglyToad/PdfPig>
- IBM Granite embedding models: <https://github.com/ibm-granite/granite-embedding-models>
- OpenVINO GenAI TextEmbeddingPipeline: <https://docs.openvino.ai/2026/api/genai_api/_autosummary/openvino_genai.TextEmbeddingPipeline.html>
