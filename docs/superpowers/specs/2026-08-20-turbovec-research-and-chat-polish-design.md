# TurboVec Research Gate, Knowledge Attachments, and Chat Polish Design

**Date:** 2026-08-20

**Status:** Approved design

**Target branch:** `feature/gguf-cli-chat-production`

**Decision basis:** User-approved visual direction B (soft modern), repository ADR-TurboVec, local Windows x64 feasibility tests, and upstream TurboVec 1.0.0 documentation and source

## 1. Purpose

Granite Edge AI needs a more refined light chat screen, a useful file-attachment interaction, and an evidence-based decision about using TurboVec for local retrieval-augmented generation. These concerns are related but must not be conflated: selecting a file is a user-interface capability, while extracting, embedding, indexing, retrieving, and supplying its contents to Granite is a separate research and runtime subsystem.

This design delivers the approved soft-modern visual refinement and an honest attachment-selection experience while first validating TurboVec through a pinned command-line demonstrator. The application must not imply that an attached file influenced a response until indexing and retrieval have completed successfully.

## 2. Existing Decision Boundary

`docs/architecture/decisions/ADR-TurboVec.md` currently defers full application integration until the project records the exact implementation, version, licence, Windows result, input/output contract, matched uncompressed baseline, and release-role decision. This design does not silently overturn that ADR. It implements the required technical gate and leaves application retrieval integration behind a later explicit decision.

The first release role remains one of:

1. Implement in the application.
2. Retain as a command-line demonstrator only.
3. Defer.
4. Exclude.

## 3. Approved Decisions

- Use visual direction B: soft blue-violet accents, rounded geometry, gentle depth, and restrained gradients on an otherwise light interface.
- Display the complete repository lockup from `docs/Logo/granite-edge-ai-lockup.svg` in the sidebar header, not the monogram alone.
- Correct the composer layout structurally so placeholder and typed text remain vertically centred.
- Make the plus control open an attachment flyout containing **Add knowledge files...**.
- Initially accept `.txt` and `.md` knowledge files only.
- Show selected files as removable attachment chips above the prompt row.
- Label selected files as **Not indexed** while TurboVec retrieval is unavailable.
- Do not inject file contents into an ordinary prompt or claim retrieval occurred.
- Pin the research implementation to `RyanCodrai/turbovec` version `1.0.0`, source commit `ccab9f325e6ce2a270a87daf01ae4e443bcf2d49`, subject to captured artefact hashes.
- Use a Python command-line demonstrator first because a published Windows x64 wheel is available and works without installing a Rust toolchain.
- Use an uncompressed float32 index as the matched quality baseline.
- Treat 4-bit TurboVec as the default candidate and 2-bit as experimental.
- Defer PDF support until a bounded local extractor, licence, failure contract, and extraction-quality tests are approved.
- Do not describe TurboVec as compressing documents, GGUF files, model weights, or the Granite KV cache. In this feature it compresses embedding vectors used for retrieval.

## 4. Research Baseline and Findings

### 4.1 Upstream identity

- Repository: `https://github.com/RyanCodrai/turbovec`
- Version: `1.0.0`
- Source commit: `ccab9f325e6ce2a270a87daf01ae4e443bcf2d49`
- Licence: MIT
- Native core: Rust, minimum Rust 1.89 for x86-64 source builds
- Python surface: PyO3 ABI3 wheel supporting Python 3.9 and later
- Windows release route: upstream CI builds and tests Windows x64 wheels
- Primary index types: `TurboQuantIndex` and stable-ID `IdMapIndex`
- Supported widths: 2, 3, and 4 bits per coordinate
- Persistence: `.tv` and `.tvim`, with whole-file and incremental save APIs

### 4.2 Local environment result

The pinned Python package installed and loaded successfully on Windows x64 with Python 3.12. The tested machine is an AMD Ryzen 7 8845HS, so this proves Windows x64 compatibility but is not Intel-specific performance evidence. The target Intel test remains mandatory.

### 4.3 Controlled synthetic comparison

For 20,000 normalised float32 vectors of 384 dimensions and 300 independent queries:

| Route | Stored size | Compression versus FP32 | Recall@10 | Exact top result present in approximate top 10 |
|---|---:|---:|---:|---:|
| Float32 | 30.72 MB | 1.0x | 100% | 100% |
| TurboVec 2-bit | 2.23 MB | 13.8x | 52.3% | 90.7% |
| TurboVec 4-bit | 4.35 MB | 7.1x | 85.8% | 100% |

The write/load round trip returned identical result IDs for the checked queries. These synthetic figures are feasibility evidence only and are not release-quality semantic retrieval claims.

### 4.4 Real embedding compatibility check

FastEmbed 0.8.0 with `BAAI/bge-small-en-v1.5` generated 384-dimensional float32 embeddings through ONNX Runtime CPU. TurboVec accepted those embeddings directly. In a small controlled semantic fixture, the 4-bit route matched the float32 top result for every checked query. The float32 baseline itself selected the intended passage for only four of six queries, demonstrating that embedding, prompting, content, and chunking quality must be evaluated independently from vector compression.

Observed first-run model setup included a network download. A production or reproducible research route must therefore use a separately approved, pre-fetched model artefact with an exact version, licence, file manifest, and SHA-256 rather than downloading implicitly at runtime.

## 5. Visual Design

### 5.1 Overall screen

The screen remains light and follows the approved reference structure: fixed sidebar, elevated conversation surface, header, transcript, and bottom composer. Visual direction B adds polish without changing information architecture.

- Canvas: very light cool blue-grey.
- Sidebar: white-to-cool-white subtle vertical treatment with a low-contrast divider.
- Conversation surface: white, rounded, fine cool-blue border, soft shadow.
- Primary controls: blue-to-indigo restrained gradient with white content.
- Secondary controls: white or lightly tinted surface, cool border, dark text.
- Typography: clear hierarchy with stronger headings and quiet metadata.
- Motion: short hover, press, and focus transitions only; no decorative continuous animation.

### 5.2 Buttons

New Chat, Import Model, Settings, attachment, send, and stop controls share a coherent state system:

- Default, pointer-over, pressed, disabled, and keyboard-focus states.
- Centred icon and label layout.
- Consistent corner radius and minimum hit target.
- Primary actions use the restrained gradient; secondary actions use outlined or tinted surfaces.
- Focus indicators remain visible and do not rely on colour alone.
- The stop control retains the correctly centred square icon.

### 5.3 Branding

The sidebar header uses `docs/Logo/granite-edge-ai-lockup.svg` through the application asset link. It is aligned to the sidebar content margin, uses uniform scaling, and includes the complete monogram and **Granite Edge AI** wordmark. The central empty state may retain the monogram as a distinct decorative mark.

### 5.4 Composer

The composer is a two-row control when files are selected and a single-row control otherwise.

```text
optional attachment chips: [notes.md · Not indexed ×]
+-------------------------------------------------------+
| + | Type a message...                           | ↑  |
+-------------------------------------------------------+
```

The prompt TextBox fills a fixed-height row and centres its content vertically. When multiline input grows, the control expands upward to its existing maximum and changes to top-aligned content only after a second visual line exists. This avoids an awkward baseline while preserving multiline editing.

### 5.5 Attachment interaction

Selecting the plus button opens a small flyout. Its initial command is **Add knowledge files...**. Activating it opens the Windows file picker with `.txt` and `.md` filters and multi-selection enabled within bounded limits.

Each accepted file creates a removable chip containing the filename and **Not indexed** state. The application stores only the picker identity needed for the current attachment selection; it does not copy, parse, log, embed, or transmit content in this milestone. Cancellation causes no state change. Unsupported, inaccessible, duplicate, or oversized items produce a concise local error.

## 6. Command-Line Demonstrator

### 6.1 Purpose and boundary

The demonstrator proves or rejects the TurboVec retrieval route independently of WinUI and Granite generation. It is not silently launched by the application in this milestone.

The entry point is a PowerShell wrapper under `scripts/turbovec/` that invokes a versioned Python CLI. The wrapper uses explicit argument arrays and never interpolates file contents into shell commands.

### 6.2 Commands

The proposed interface is:

```text
Invoke-TurboVecResearch.ps1 doctor
Invoke-TurboVecResearch.ps1 index --input <directory-or-file> --output <index-root> --bits 4
Invoke-TurboVecResearch.ps1 query --index <index-root> --text <query> --top-k 5
Invoke-TurboVecResearch.ps1 benchmark --fixture <fixture-manifest> --output <evidence-root>
```

- `doctor` reports exact Python, package, model, platform, processor, and provider identities.
- `index` extracts supported text, chunks it deterministically, embeds it, creates stable chunk IDs, and writes both float32 baseline and TurboVec indexes.
- `query` embeds a query and returns ranked chunk IDs, scores, source paths, bounded excerpts, and timings.
- `benchmark` evaluates identical queries against float32, 2-bit, and 4-bit routes and emits machine-readable and human-readable results.

### 6.3 Artefacts and manifest

Each index root contains a versioned manifest with:

- TurboVec package version and source commit;
- wheel SHA-256 and licence reference;
- Python and dependency lock identity;
- embedding model identity, licence, manifest, and hashes;
- chunking algorithm version and parameters;
- source file relative identities and content hashes;
- vector dimension, bit width, index format version, and chunk-ID mapping;
- creation time, requested backend, and actual backend;
- no raw document content unless explicitly required by the controlled fixture.

An index is rejected if its manifest does not match the embedding model, vector dimension, chunking version, or source hashes expected by the query route.

## 7. Data Flow

```text
approved text file
    -> bounded UTF-8 extraction
    -> deterministic chunking with source offsets
    -> local embedding model
    -> float32 vectors + stable chunk metadata
    -> matched float32 baseline index
    -> TurboVec 2-bit and 4-bit indexes
    -> fixed queries
    -> ranked chunk IDs and excerpts
    -> recall, latency, storage, and correctness evidence
```

Later application integration, if approved, adds a protected local retrieval worker between WinUI and this pipeline. Retrieved chunks then enter Granite through a bounded, source-labelled context template. That later worker should reuse the existing supervised process and structured transport principles used by the GGUF runtime rather than loading Python or Rust directly into the WinUI process.

## 8. Safety, Privacy, and Failure Handling

- No listener, cloud retrieval service, or telemetry is introduced.
- Network access is prohibited during ordinary indexing and query runs.
- Model and package acquisition is a separate controlled setup step.
- Paths and document contents are not written to ordinary logs.
- Diagnostic output uses fixed categories and safe relative identities.
- Input byte count, file count, decoded character count, chunk count, chunk length, query length, and result count are bounded.
- Binary, malformed, inaccessible, and non-UTF-8 files fail clearly.
- Partial indexes are written to an operation-specific staging directory and promoted only after validation.
- Cancellation and failure remove or quarantine incomplete artefacts without damaging a previously valid index.
- Index deserialisation treats all files as untrusted and validates magic, version, dimensions, counts, and manifest consistency before use.

## 9. Evaluation and Tests

### 9.1 UI tests

- Full lockup asset is packaged and referenced in the sidebar.
- Direction-B theme tokens and button states are present.
- Import Model content is centred.
- Prompt placeholder and single-line text are vertically centred.
- Multiline growth remains usable.
- Plus opens the attachment flyout.
- Picker cancellation preserves state.
- Supported files create removable **Not indexed** chips.
- Duplicate, unsupported, oversized, and inaccessible selections fail safely.
- Keyboard navigation, automation names, focus visibility, and contrast remain acceptable.

### 9.2 CLI contract and unit tests

- Argument parsing and stable exit codes.
- UTF-8 extraction boundaries and invalid-input handling.
- Deterministic chunk IDs, offsets, overlap, and limits.
- Manifest creation and mismatch rejection.
- Stable metadata-to-vector mapping.
- No content or absolute-path leakage in diagnostics.
- Persistence round trip and corrupt-index rejection.

### 9.3 Matched retrieval tests

Every evaluation uses the same source chunks, embedding model, queries, `top-k`, and relevance judgements for float32, TurboVec 2-bit, and TurboVec 4-bit routes. Record:

- Recall@1, Recall@5, and Recall@10 against the exact float32 ranking;
- judged source correctness and mean reciprocal rank;
- embedding, indexing, save, load, and query timings;
- peak working set and on-disk size;
- warm and cold runs with repetitions;
- requested and actual CPU/provider state;
- complete environment identity.

The test corpus includes exact lookups, paraphrases, cross-chunk questions, distractors, empty/short files, repeated passages, and queries with no supported answer.

### 9.4 Hardware matrix

The Windows/AMD result is retained as development evidence. Release-role selection requires a controlled run on the intended Intel machine. Intel results must not be inferred from AMD results or upstream Xeon measurements.

## 10. Promotion Gate

TurboVec may proceed to application integration only when:

1. Exact repository, commit, package, wheel hash, licence, and dependency lock are recorded.
2. A pre-fetched embedding model with licence and hashes is controlled.
3. Offline Windows x64 indexing, persistence, reload, and query tests pass.
4. The intended Intel hardware run is complete.
5. On the controlled semantic fixture, the 4-bit route achieves Recall@10 of at least 0.85, retains at least 90% of the float32 mean reciprocal rank, and keeps judged relevant-source Hit@5 within five percentage points of float32.
6. The persisted 4-bit index is no larger than 25% of the matched float32 vector bytes.
7. Median vector-search latency is no slower than the exact float32 search on the same corpus and machine; end-to-end latency is also reported with embedding time included.
8. Failure, privacy, and index-mismatch tests pass.
9. Integration work does not displace the stable GGUF chat route.
10. ADR-TurboVec is updated with Implement, CLI-only, Defer, or Exclude.

Until then, the UI attachment state remains **Not indexed**, and Granite responses must not claim to use those files.

## 11. Explicitly Deferred

- PDF, Office, image, audio, archive, and web-page extraction.
- OCR.
- Cloud embeddings or hosted vector databases.
- Background filesystem watching and automatic re-indexing.
- Cross-device index portability claims beyond validated formats.
- GPU embedding acceleration.
- Direct Rust FFI or in-process Python inside WinUI.
- Automatic attachment-to-prompt injection.
- Full application RAG until the promotion gate and ADR update pass.

## 12. Acceptance Criteria for This Delivery

This delivery is complete when:

- the chat screen implements approved visual direction B;
- the complete lockup is visible in the sidebar;
- prompt text is vertically centred in the single-line state;
- refined controls have coherent interaction and accessibility states;
- the plus action selects supported files and shows removable **Not indexed** chips;
- the TurboVec research CLI can run `doctor`, `index`, `query`, and `benchmark` using pinned inputs;
- matched float32, 2-bit, and 4-bit evidence is reproducible;
- verification passes without weakening existing GGUF chat tests;
- documentation distinguishes feasibility, command-line demonstration, and application integration truthfully.
