# TurboVec PDF Feasibility Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce a reproducible Windows/Intel comparison of Exact FP32 and TurboVec 2/3/4-bit retrieval using selectable-text PDF, TXT, and Markdown inputs embedded by the pinned Granite small English R2 model, then record one formal integration decision without changing the product or frontend.

**Architecture:** A .NET PdfPig console probe extracts page-aware text into a closed JSON contract. A Python evaluation package consumes canonical chunks or the extractor output, invokes a locally locked OpenVINO Granite embedding model, runs one Exact and three TurboVec indices, measures matched results, and emits append-only raw evidence. A separate deterministic evaluator validates the evidence and returns `INTEGRATE`, `DEMONSTRATOR_ONLY`, `EXCLUDE`, or `BLOCKED`; only `INTEGRATE` permits a later production-integration plan.

**Tech Stack:** .NET 8, MSTest 4.3.2, PdfPig 0.1.15, Python 3.12, `unittest`, NumPy, OpenVINO GenAI 2026.3, Granite embedding small English R2, TurboVec 1.0.0, PowerShell 7/Windows PowerShell-compatible orchestration, JSON Schema Draft 2020-12.

**Spec:** `docs/superpowers/specs/2026-09-02-turbovec-pdf-knowledge-library-design.md`

## Global Constraints

- Execute from a new isolated worktree created from commit `e4cb857a8bcce258277cfbc1f0db59f9e541d6ef`; do not develop in `C:\UCL-C0-R4` or the active frontend worktree.
- This plan may change experiment tooling, generated fixtures, evidence controls, and decision documentation only. It must not change product XAML, product C#, existing GGUF/OpenVINO chat workers, `main`, or the frontend worker's branch.
- TurboVec is fixed to version `1.0.0`, commit `ccab9f325e6ce2a270a87daf01ae4e443bcf2d49`, tree `0a7141836d01da61e6f3cf53b5c60916b741d811`, and MIT licence.
- The published Windows TurboVec wheel is `turbovec-1.0.0-cp39-abi3-win_amd64.whl`, 611,788 bytes, SHA-256 `CD855E0B318A57DC57C733F9A62AE98DE5192F4F6C2C760E305523E8CEB1B090`.
- PdfPig is fixed to release `0.1.15`, tag object `fdd933084ccf064cfcb309c91c1237c9a3e9430d`, release commit `f131f642976936e06ee91cb19d3ed728f9dd18b6`, and Apache-2.0 licence.
- The embedding model is `ibm-granite/granite-embedding-small-english-r2`, 384 dimensions. Its exact immutable revision and every local artefact hash must be locked before a measured run.
- The controlled OpenVINO candidate is version `2026.3.0.0`; execution must stop as `BLOCKED` if the local locked distribution cannot create `TextEmbeddingPipeline` for the pinned model without changing system security policy.
- No toolchain, package, model, or dependency may be installed globally. Rust 1.89 installation requires separate explicit user approval.
- No runtime step may download code, models, or wheels. Acquisition is a distinct provenance stage; measured execution consumes only locked local assets.
- Test fixtures must be project-authored or redistributable. Never commit model weights, third-party source trees, private documents, absolute private paths, raw prompts containing user content, or secrets.
- Raw run directories are append-only. A rerun receives a new run ID; scripts must refuse to overwrite an existing run.
- The same admitted documents, extracted text, chunks, embeddings, query vectors, top-k, warm-up policy, and repetitions must be used for Exact and all TurboVec configurations.
- Metrics and claims must distinguish `passed`, `failed`, `skipped`, `blocked`, and `unexecuted`.
- A TurboVec configuration qualifies for a later production plan only when every Gate A threshold in the approved spec passes. Gate B remains mandatory before product exposure or release.

---

## File Structure

### Controlled experiment package

- `scripts/testing/turbovec/__init__.py`: package boundary and experiment version.
- `scripts/testing/turbovec/contracts.py`: strict manifest, corpus, result, and evidence validation.
- `scripts/testing/turbovec/provenance.py`: file/tree hashes and dependency-identity checks.
- `scripts/testing/turbovec/chunking.py`: deterministic `granite-chunker-v1` implementation.
- `scripts/testing/turbovec/embedding.py`: test-double and OpenVINO Granite embedding adapters.
- `scripts/testing/turbovec/indexes.py`: Exact FP32 and TurboVec `IdMapIndex` adapters.
- `scripts/testing/turbovec/metrics.py`: recall, nDCG, latency, size, and aggregate calculations.
- `scripts/testing/turbovec/decision.py`: pure four-outcome qualification policy.
- `scripts/testing/turbovec/runner.py`: matched execution and raw-event capture.
- `scripts/testing/run_turbovec_feasibility.py`: CLI entry point.
- `scripts/testing/Invoke-TurboVecFeasibility.ps1`: locked-asset preflight and append-only orchestration.
- `scripts/testing/Test-TurboVecEvidence.ps1`: final evidence and privacy validator.

### PDF probe

- `tools/TurboVec.PdfExtractionSpike/TurboVec.PdfExtractionSpike.csproj`: pinned executable project.
- `tools/TurboVec.PdfExtractionSpike/Program.cs`: bounded CLI and JSON-lines terminal output.
- `tools/TurboVec.PdfExtractionSpike/PdfExtractor.cs`: PdfPig page extraction and classification.
- `tools/TurboVec.PdfExtractionSpike/ExtractionContracts.cs`: request/result/failure records.
- `tools/TurboVec.PdfExtractionSpike.Tests/TurboVec.PdfExtractionSpike.Tests.csproj`: MSTest project.
- `tools/TurboVec.PdfExtractionSpike.Tests/PdfExtractorTests.cs`: generated fixture tests.
- `tools/TurboVec.PdfExtractionSpike.Tests/ProgramContractTests.cs`: process/JSON/privacy tests.

### Protocol, tests, and evidence

- `experiments/protocols/turbovec/feasibility-v1.schema.json`: closed evidence schema.
- `experiments/protocols/turbovec/corpus-v1.json`: canonical English passages and source formats.
- `experiments/protocols/turbovec/queries-v1.json`: canonical queries and expected answer facts.
- `experiments/protocols/turbovec/relevance-v1.json`: adjudicated graded relevance.
- `experiments/manifests/turbovec/feasibility-v1.json`: dependency, model, run, and threshold controls.
- `scripts/testing/tests/test_turbovec_contracts.py`: schema and identity tests.
- `scripts/testing/tests/test_turbovec_chunking.py`: deterministic chunk tests.
- `scripts/testing/tests/test_turbovec_embedding.py`: adapter and normalization tests.
- `scripts/testing/tests/test_turbovec_indexes.py`: matched index lifecycle tests.
- `scripts/testing/tests/test_turbovec_metrics.py`: metric oracle tests.
- `scripts/testing/tests/test_turbovec_decision.py`: four-outcome policy tests.
- `scripts/testing/tests/test_turbovec_runner.py`: fake-provider end-to-end tests.
- `experiments/raw-results/turbovec/{run-id}/`: immutable measured-run output, created only by the runner.
- `experiments/processed-results/EXP-TV-COMP-001/{run-id}/`: derived summary and formal decision.

---

### Task 1: Freeze experiment contracts and provenance

**Files:**
- Create: `experiments/protocols/turbovec/feasibility-v1.schema.json`
- Create: `experiments/manifests/turbovec/feasibility-v1.json`
- Create: `scripts/testing/turbovec/__init__.py`
- Create: `scripts/testing/turbovec/contracts.py`
- Create: `scripts/testing/turbovec/provenance.py`
- Create: `scripts/testing/tests/test_turbovec_contracts.py`

**Interfaces:**
- Produces: `load_control_manifest(path: Path) -> ControlManifest`, `sha256_file(path: Path) -> str`, `validate_evidence(document: dict) -> None`.
- Consumes: the immutable identities and thresholds in Global Constraints.

- [ ] **Step 1: Write failing contract tests**

```python
class TurboVecControlContractTests(unittest.TestCase):
    def test_rejects_wrong_turbovec_wheel_hash(self):
        document = valid_manifest()
        document["dependencies"]["turbovec"]["wheel_sha256"] = "0" * 64
        with self.assertRaisesRegex(ValueError, "turbovec wheel"):
            load_control_manifest_dict(document)

    def test_all_four_outcomes_are_closed_enum_values(self):
        self.assertEqual(
            set(Decision),
            {Decision.INTEGRATE, Decision.DEMONSTRATOR_ONLY,
             Decision.EXCLUDE, Decision.BLOCKED},
        )

    def test_schema_rejects_unknown_properties(self):
        evidence = valid_evidence()
        evidence["private_path"] = r"C:\\Users\\Someone\\secret.pdf"
        with self.assertRaises(ValueError):
            validate_evidence(evidence)
```

- [ ] **Step 2: Run the tests and confirm the missing package failure**

Run: `python -m unittest scripts.testing.tests.test_turbovec_contracts -v`

Expected: import failure for `scripts.testing.turbovec.contracts`.

- [ ] **Step 3: Implement closed dataclasses and validators**

Use frozen dataclasses for dependency identity, embedding identity, threshold policy, run identity, command arithmetic, and evidence references. Reject unknown JSON keys, non-absolute external asset paths at execution time, repository-relative evidence paths that escape the repository, non-lowercase SHA-256 values, wrong dimensions, and empty identifiers.

```python
class Decision(str, Enum):
    INTEGRATE = "INTEGRATE"
    DEMONSTRATOR_ONLY = "DEMONSTRATOR_ONLY"
    EXCLUDE = "EXCLUDE"
    BLOCKED = "BLOCKED"

@dataclass(frozen=True)
class Thresholds:
    recall_at_10: float = 0.90
    relative_ndcg_at_10: float = 0.95
    minimum_storage_ratio: float = 2.0
    maximum_p95_slowdown: float = 1.10
```

The schema must set `additionalProperties: false` at every object level and require explicit arithmetic fields `discovered`, `executed`, `passed`, `failed`, and `skipped`.

- [ ] **Step 4: Populate the controlled manifest**

Record all fixed identities from Global Constraints. Represent the Granite revision and local asset hashes as entry-gate fields with status `unlocked`; validation permits that state only before execution and forbids a measured run while any required asset remains unlocked.

- [ ] **Step 5: Run focused and existing Python tests**

Run: `python -m unittest scripts.testing.tests.test_turbovec_contracts -v`

Run: `python -m unittest discover -s scripts/testing/tests -p "test_*.py"`

Expected: all tests pass; no existing test count decreases.

- [ ] **Step 6: Commit**

```powershell
git add experiments/protocols/turbovec/feasibility-v1.schema.json `
        experiments/manifests/turbovec/feasibility-v1.json `
        scripts/testing/turbovec scripts/testing/tests/test_turbovec_contracts.py
git commit -m "test(turbovec): freeze feasibility contracts"
```

### Task 2: Create deterministic corpus, chunking, and relevance controls

**Files:**
- Create: `experiments/protocols/turbovec/corpus-v1.json`
- Create: `experiments/protocols/turbovec/queries-v1.json`
- Create: `experiments/protocols/turbovec/relevance-v1.json`
- Create: `scripts/testing/turbovec/chunking.py`
- Create: `scripts/testing/tests/test_turbovec_chunking.py`

**Interfaces:**
- Produces: `chunk_pages(document_sha256: str, pages: Sequence[PageText], tokenizer: TokenCounter) -> tuple[Chunk, ...]`.
- Produces: 30 documents, 120 queries, stable graded relevance, and stable answer facts.

- [ ] **Step 1: Write failing deterministic chunk tests**

```python
def test_chunk_ids_are_stable_and_page_aware(self):
    pages = (PageText(1, "alpha " * 600), PageText(2, "beta " * 300))
    first = chunk_pages("a" * 64, pages, WordFixtureTokenCounter())
    second = chunk_pages("a" * 64, pages, WordFixtureTokenCounter())
    self.assertEqual(first, second)
    self.assertTrue(all(chunk.token_count <= 768 for chunk in first))
    self.assertTrue(all(chunk.end_page - chunk.start_page <= 1 for chunk in first))

def test_empty_pages_preserve_following_page_numbers(self):
    chunks = chunk_pages("b" * 64,
        (PageText(1, ""), PageText(2, "retrievable fact")),
        WordFixtureTokenCounter())
    self.assertEqual(chunks[0].start_page, 2)
```

- [ ] **Step 2: Confirm failure**

Run: `python -m unittest scripts.testing.tests.test_turbovec_chunking -v`

Expected: import failure for `chunk_pages`.

- [ ] **Step 3: Implement `granite-chunker-v1`**

Normalize CRLF to LF, Unicode to NFC, and runs of horizontal whitespace to one space without altering paragraph breaks. Accumulate paragraphs toward 512 tokens, stop at 768, retain a 64-token suffix as overlap, and never span more than two adjacent PDF pages. Calculate the chunk ID exactly as specified in the design.

- [ ] **Step 4: Create the controlled corpus**

Create 10 PDF-designated, 10 TXT-designated, and 10 Markdown-designated documents. Each document contains five independently retrievable factual paragraphs. Cover education, healthcare administration, computing, energy, transport, environment, local government, research methods, accessibility, and information security. Facts must be fictional or project-authored and must not contain personal data or clinical advice.

Create exactly 120 queries:

- 60 direct paraphrases with one grade-3 chunk;
- 30 terminology variants with one grade-3 and one grade-1 chunk;
- 15 multi-detail queries with two grade-3 chunks;
- 15 negative queries with no relevant chunk.

Every positive query carries a literal expected answer fact and every relevance row carries `query_id`, `chunk_id`, and integer grade `1` or `3`. The corpus files include a canonical SHA-256 calculated over UTF-8 JSON serialized with sorted keys and compact separators.

- [ ] **Step 5: Add corpus-integrity assertions**

Tests must assert the exact counts above, unique IDs, no absolute paths, no unbound relevance IDs, no positive query without a grade-3 result, no negative query with a relevance row, and stable canonical hashes.

- [ ] **Step 6: Run and commit**

Run: `python -m unittest scripts.testing.tests.test_turbovec_chunking -v`

```powershell
git add experiments/protocols/turbovec scripts/testing/turbovec/chunking.py `
        scripts/testing/tests/test_turbovec_chunking.py
git commit -m "test(turbovec): add controlled retrieval corpus"
```

### Task 3: Implement the isolated PdfPig extraction spike

**Files:**
- Create: `tools/TurboVec.PdfExtractionSpike/TurboVec.PdfExtractionSpike.csproj`
- Create: `tools/TurboVec.PdfExtractionSpike/ExtractionContracts.cs`
- Create: `tools/TurboVec.PdfExtractionSpike/PdfExtractor.cs`
- Create: `tools/TurboVec.PdfExtractionSpike/Program.cs`
- Create: `tools/TurboVec.PdfExtractionSpike.Tests/TurboVec.PdfExtractionSpike.Tests.csproj`
- Create: `tools/TurboVec.PdfExtractionSpike.Tests/PdfExtractorTests.cs`
- Create: `tools/TurboVec.PdfExtractionSpike.Tests/ProgramContractTests.cs`

**Interfaces:**
- Produces: `PdfExtractionResult PdfExtractor.Extract(PdfExtractionRequest request, CancellationToken cancellationToken)`.
- CLI: `TurboVec.PdfExtractionSpike.exe --input $InputPdf --expected-sha256 $ExpectedSha256 --max-pages 1000 --max-scalars 20000000`.
- Standard output: exactly one closed JSON result; standard error: bounded diagnostics without input paths or extracted text.

- [ ] **Step 1: Create test projects and write failing tests**

The executable targets `net8.0-windows10.0.19041.0`, x64, nullable enabled, analyzers enabled, warnings as errors, and references `PdfPig` version `0.1.15`. The test project uses the repository's MSTest versions.

```csharp
[TestMethod]
public void ExtractPreservesEmptyPageAndFollowingPageNumber()
{
    using var fixture = PdfFixture.Create((1, ""), (2, "second page fact"));
    var result = new PdfExtractor().Extract(fixture.Request, CancellationToken.None);
    Assert.AreEqual(2, result.Pages.Count);
    Assert.AreEqual(string.Empty, result.Pages[0].Text);
    Assert.AreEqual("second page fact", result.Pages[1].Text);
}

[TestMethod]
public void ImageOnlyPdfReturnsOcrRequiredWithoutSuccess()
{
    using var fixture = PdfFixture.CreateImageOnly();
    var result = new PdfExtractor().Extract(fixture.Request, CancellationToken.None);
    Assert.AreEqual("ocr_required", result.FailureCode);
    Assert.IsFalse(result.Succeeded);
}
```

- [ ] **Step 2: Run tests and confirm compile failure**

Run: `dotnet test tools/TurboVec.PdfExtractionSpike.Tests/TurboVec.PdfExtractionSpike.Tests.csproj -c Debug`

Expected: compile failure because extractor types do not exist.

- [ ] **Step 3: Implement structural limits and extraction**

Open the already-hashed snapshot read-only with `FileShare.Read`. Reject a hash mismatch before PdfPig opens the file. Enforce 100 MiB, 1,000 pages, 20 million Unicode scalar values, cancellation between pages, and a caller-enforced 120-second process timeout. Return every page ordinal, including empty pages. Classify password protection, malformed PDF, limits, cancellation, and image-only documents with stable failure codes.

- [ ] **Step 4: Implement the closed CLI**

Reject unknown, repeated, missing, relative, link/reparse, and non-file arguments. Serialize with `System.Text.Json`; never echo the input path or extracted text to stderr. Exit `0` only for a structurally valid result envelope; domain failures remain inside the envelope with a nonzero documented domain exit code.

- [ ] **Step 5: Add process and privacy regression tests**

Cover malformed bytes, encrypted fixture, mixed text/image pages, long internal filename, hash mismatch, unknown switch, cancellation, stdout closure, stderr truncation, and a scan proving that neither the absolute fixture root nor extracted sentinel text appears in diagnostics.

- [ ] **Step 6: Run and commit**

Run: `dotnet test tools/TurboVec.PdfExtractionSpike.Tests/TurboVec.PdfExtractionSpike.Tests.csproj -c Debug --logger "trx;LogFileName=turbovec-pdf.trx"`

```powershell
git add tools/TurboVec.PdfExtractionSpike tools/TurboVec.PdfExtractionSpike.Tests
git commit -m "test(turbovec): add isolated PDF extraction spike"
```

### Task 4: Implement and lock the Granite embedding adapter

**Files:**
- Create: `scripts/testing/turbovec/embedding.py`
- Create: `scripts/testing/tests/test_turbovec_embedding.py`
- Modify: `experiments/manifests/turbovec/feasibility-v1.json`

**Interfaces:**
- Produces: `EmbeddingProvider.embed_documents(texts: Sequence[str]) -> numpy.ndarray` and `embed_queries(texts: Sequence[str]) -> numpy.ndarray`.
- Production experiment adapter: `OpenVinoGraniteEmbeddingProvider(model_root: Path, device: str = "CPU")`.

- [ ] **Step 1: Write failing adapter tests**

```python
def test_normalizes_384_finite_values(self):
    provider = FakeRawProvider(np.ones((2, 384), dtype=np.float32))
    vectors = NormalizingEmbeddingProvider(provider).embed_documents(["a", "b"])
    np.testing.assert_allclose(np.linalg.norm(vectors, axis=1), 1.0, atol=1e-6)

def test_rejects_wrong_dimension_and_nan(self):
    for data in (np.ones((1, 383), np.float32),
                 np.full((1, 384), np.nan, np.float32)):
        with self.assertRaises(ValueError):
            validate_embeddings(data, expected_rows=1, expected_dim=384)
```

- [ ] **Step 2: Confirm failure**

Run: `python -m unittest scripts.testing.tests.test_turbovec_embedding -v`

- [ ] **Step 3: Implement the adapters**

Import `openvino_genai` only inside the live adapter. Construct `TextEmbeddingPipeline(str(model_root), "CPU")`; call `embed_documents` for passages and `embed_query` for individual queries; convert to C-contiguous FP32; verify shape and finiteness; and normalize rows with a nonzero norm. Batch documents at 16 inputs and 12,288 aggregate input tokens.

The unit-test fake generates deterministic 384-dimensional vectors from SHA-256-expanded bytes and is forbidden when the run manifest declares `measured: true`.

- [ ] **Step 4: Add the asset-lock preflight**

Require a local model directory, reject links/reparse points, enumerate sorted regular files, and calculate a tree hash over relative UTF-8 path, size, and SHA-256 records. Record the exact Hugging Face revision from the snapshot metadata and reject a mutable branch name. Record OpenVINO package version, Python extension hash, requested/actual device, and model tree hash in a new immutable run manifest rather than editing a previous run.

- [ ] **Step 5: Run tests and perform an unmeasured one-passage smoke test**

Run: `python -m unittest scripts.testing.tests.test_turbovec_embedding -v`

Run: `python scripts/testing/run_turbovec_feasibility.py embedding-smoke --model-root $LockedModelRoot --text "Granite local retrieval smoke test." --output $NewExternalTempDirectory`

Expected: one finite normalized vector with shape `(1, 384)`, actual device `CPU`, and no measured-quality claim.

- [ ] **Step 6: Commit only code and text identity records**

```powershell
git add scripts/testing/turbovec/embedding.py `
        scripts/testing/tests/test_turbovec_embedding.py `
        experiments/manifests/turbovec/feasibility-v1.json
git commit -m "test(turbovec): add locked Granite embedding adapter"
```

### Task 5: Implement matched Exact and TurboVec index adapters

**Files:**
- Create: `scripts/testing/turbovec/indexes.py`
- Create: `scripts/testing/tests/test_turbovec_indexes.py`

**Interfaces:**
- Produces: `ExactIndex.add(vectors, ids)`, `TurboVecIndex.add(vectors, ids)`, `search(queries, k) -> SearchBatch`, `remove(id) -> bool`, `save(path)`, and `load(path)`.
- Stable external IDs are unsigned 64-bit values derived from the first eight bytes of the chunk SHA-256; collisions are rejected before insertion.

- [ ] **Step 1: Write parameterized failing lifecycle tests**

```python
def exercise_index(factory):
    vectors = unit_vectors(12, 384, seed=7)
    ids = np.arange(100, 112, dtype=np.uint64)
    index = factory()
    index.add(vectors, ids)
    before = index.search(vectors[:2], 4)
    self.assertEqual(before.ids.shape, (2, 4))
    self.assertTrue(index.remove(105))
    self.assertFalse(index.remove(105))
    self.assertNotIn(105, index.search(vectors[5:6], 12).ids[0])
```

Run this contract against Exact and TurboVec 2/3/4-bit. Add tests for duplicate IDs, ID-hash collision, wrong dimension, non-contiguous input, NaN/infinity, zero vectors, empty query batches, `k <= 0`, corrupt save data, save/load result stability, and the 200-character source-name regression using an internal hash filename.

- [ ] **Step 2: Confirm failure**

Run: `python -m unittest scripts.testing.tests.test_turbovec_indexes -v`

- [ ] **Step 3: Implement Exact**

Store C-contiguous normalized FP32 rows and uint64 IDs. Search uses matrix multiplication, stable descending score order, then ascending ID as the tie-break. Save a closed metadata JSON plus `.npy` arrays under short constant filenames and verify hashes during load.

- [ ] **Step 4: Implement TurboVec**

Wrap only `turbovec.IdMapIndex(dim=384, bit_width=bits)`, `add_with_ids`, `search`, `remove`, `write`, and `load`. Use `index-{bits}.tvim`, never a user-derived stem. Validate result shapes, IDs, scores, bit width, length, and round-trip identity outside the extension boundary.

- [ ] **Step 5: Run tests against the pinned installed wheel**

Run: `python -m unittest scripts.testing.tests.test_turbovec_indexes -v`

Expected: every adapter contract passes on Exact and all three bit widths.

- [ ] **Step 6: Commit**

```powershell
git add scripts/testing/turbovec/indexes.py scripts/testing/tests/test_turbovec_indexes.py
git commit -m "test(turbovec): add matched retrieval adapters"
```

### Task 6: Implement metrics and the four-outcome decision policy

**Files:**
- Create: `scripts/testing/turbovec/metrics.py`
- Create: `scripts/testing/turbovec/decision.py`
- Create: `scripts/testing/tests/test_turbovec_metrics.py`
- Create: `scripts/testing/tests/test_turbovec_decision.py`

**Interfaces:**
- Produces: `recall_at_k`, `ndcg_at_k`, `aggregate_latency`, `compare_configuration`, and `decide`.

- [ ] **Step 1: Write metric oracle tests with hand-calculated values**

```python
def test_recall_and_ndcg_known_ranking(self):
    expected = {10, 20}
    ranked = [10, 30, 20, 40]
    self.assertEqual(recall_at_k(ranked, expected, 4), 1.0)
    self.assertAlmostEqual(ndcg_at_k(ranked, {10: 3, 20: 1}, 4), 0.96394, places=5)

def test_latency_excludes_warmups_and_uses_nearest_rank_p95(self):
    measured = list(range(1, 21))
    self.assertEqual(nearest_rank_percentile(measured, 0.95), 19)
```

- [ ] **Step 2: Write decision-table tests**

Cover these exact precedence rules:

1. Missing prerequisites or external policy before a fair measured run -> `BLOCKED`.
2. Reproducible candidate correctness, corruption, unsafe execution, or cleanup failure -> `EXCLUDE`.
3. Valid execution but no configuration passes every numeric threshold -> `DEMONSTRATOR_ONLY`.
4. At least one configuration passes every numeric and lifecycle threshold -> `INTEGRATE`, selecting highest recall, then lowest p95, then smallest size, then highest bit width as deterministic tie-breaks.

- [ ] **Step 3: Confirm failure**

Run: `python -m unittest scripts.testing.tests.test_turbovec_metrics scripts.testing.tests.test_turbovec_decision -v`

- [ ] **Step 4: Implement pure metric functions**

Use no runtime/index imports. Negative queries are excluded from recall/nDCG and reported separately for false-positive behavior. Calculate per-query metrics, macro averages, median, nearest-rank p95, byte-accurate serving-index size, relative nDCG, storage ratio, and p95 slowdown. Reject empty measured samples, non-finite values, unmatched query sets, duplicate run IDs, and arithmetic inconsistencies.

- [ ] **Step 5: Implement the policy and machine-readable reasons**

Return a `DecisionResult` containing outcome, selected configuration or null, every threshold's observed value/pass flag, blocking/failing reasons, and exact evidence hashes. Never infer `INTEGRATE` from an incomplete metric set.

- [ ] **Step 6: Run and commit**

Run: `python -m unittest scripts.testing.tests.test_turbovec_metrics scripts.testing.tests.test_turbovec_decision -v`

```powershell
git add scripts/testing/turbovec/metrics.py scripts/testing/turbovec/decision.py `
        scripts/testing/tests/test_turbovec_metrics.py `
        scripts/testing/tests/test_turbovec_decision.py
git commit -m "test(turbovec): add retrieval qualification policy"
```

### Task 7: Build the append-only matched runner

**Files:**
- Create: `scripts/testing/turbovec/runner.py`
- Create: `scripts/testing/run_turbovec_feasibility.py`
- Create: `scripts/testing/tests/test_turbovec_runner.py`

**Interfaces:**
- CLI commands: `preflight`, `embedding-smoke`, `run`, `evaluate`, and `validate`.
- `run` creates exactly one new raw run directory and never edits it after terminal publication.

- [ ] **Step 1: Write fake-provider end-to-end tests**

```python
def test_run_uses_identical_embedding_hash_for_all_indexes(self):
    result = run_fixture_campaign(provider=DeterministicEmbeddingProvider())
    hashes = {row.embedding_matrix_sha256 for row in result.configurations}
    self.assertEqual(len(hashes), 1)

def test_existing_run_directory_is_never_overwritten(self):
    with tempfile.TemporaryDirectory() as root:
        run_id = "EXP-TV-COMP-001-20260902T120000Z-001"
        Path(root, run_id).mkdir()
        with self.assertRaises(FileExistsError):
            create_run_directory(Path(root), run_id)
```

- [ ] **Step 2: Confirm failure**

Run: `python -m unittest scripts.testing.tests.test_turbovec_runner -v`

- [ ] **Step 3: Implement strict preflight and immutable snapshots**

Preflight validates repository commit/tree/cleanliness, manifest, Python version 3.12, dependency versions and hashes, model tree, PDF probe, free disk, physical/available memory, power mode capture, absence of link/reparse chains, output-root containment, and no existing run ID. It writes observations to a new staging directory but does not classify missing prerequisites as a test failure.

- [ ] **Step 4: Implement the matched run order**

For one frozen embedding matrix and query matrix:

1. build Exact;
2. build TurboVec 2-bit;
3. build TurboVec 3-bit;
4. build TurboVec 4-bit;
5. reopen every index;
6. perform five untimed warm-up query batches per index;
7. perform 30 measured full-query batches per index in rotating Latin-square order;
8. remove and re-add a fixed 10-ID set and verify ID stability;
9. corrupt a copied index and verify rejection;
10. record process memory samples, durations in nanoseconds, serving bytes, results, actual CPU feature path when available, and cleanup.

The runner stores result IDs and scores, not source text. It writes `events.jsonl`, `environment.json`, `identities.json`, `embedding-summary.json`, one result file per index, `command-arithmetic.json`, `failures.json`, and `terminal.json`. It publishes terminal evidence only after every referenced file is closed and hashed.

- [ ] **Step 5: Implement interruption and privacy tests**

Simulate provider failure, disk-full write, Ctrl+C, malformed TurboVec output, and stale staging. Assert no terminal success, no overwrite, and no source text/private path in events or summaries.

- [ ] **Step 6: Run and commit**

Run: `python -m unittest scripts.testing.tests.test_turbovec_runner -v`

```powershell
git add scripts/testing/turbovec/runner.py scripts/testing/run_turbovec_feasibility.py `
        scripts/testing/tests/test_turbovec_runner.py
git commit -m "test(turbovec): add matched feasibility runner"
```

### Task 8: Add PowerShell orchestration and independent evidence validation

**Files:**
- Create: `scripts/testing/Invoke-TurboVecFeasibility.ps1`
- Create: `scripts/testing/Test-TurboVecEvidence.ps1`
- Create: `scripts/testing/tests/test_turbovec_evidence_scripts.py`

**Interfaces:**
- Orchestrator parameters: `-PythonExe`, `-ModelRoot`, `-TurboVecWheel`, `-OutputRoot`, `-RunId`, and `-Mode Preflight|Measured`.
- Validator parameter: `-RunDirectory`; exit `0` only for a closed, internally consistent evidence set.

- [ ] **Step 1: Write failing script-contract tests**

Use subprocess tests to require rejection of a relative model path, wrong wheel hash, existing output directory, a reparse-point asset, unknown mode, unclean source worktree, and a measured run with an unlocked model revision.

- [ ] **Step 2: Confirm failure**

Run: `python -m unittest scripts.testing.tests.test_turbovec_evidence_scripts -v`

- [ ] **Step 3: Implement orchestration**

Use `Set-StrictMode -Version Latest`, `$ErrorActionPreference = 'Stop'`, `-LiteralPath`, argument arrays, and no command-string evaluation. Invoke the .NET PDF tests, all Python TurboVec tests, preflight, embedding smoke, and measured run in that order. Record each command's discovered/executed/passed/failed/skipped arithmetic and exit code. A prerequisite block stops before measured execution and still produces a truthful blocked observation.

- [ ] **Step 4: Implement independent validation**

Validate schema closure, all hashes/byte counts, run ID, commit/tree, command arithmetic, configuration set `{exact, tq2, tq3, tq4}`, one shared embedding hash, 30 measured repetitions each, warm-up exclusion, query/relevance set identity, no absolute user-profile path, no document text sentinel, no unknown evidence file, terminal state, and decision-policy recomputation.

- [ ] **Step 5: Run the complete deterministic harness**

Run: `python -m unittest discover -s scripts/testing/tests -p "test_turbovec_*.py" -v`

Run: `dotnet test tools/TurboVec.PdfExtractionSpike.Tests/TurboVec.PdfExtractionSpike.Tests.csproj -c Debug`

Expected: all deterministic tests pass with zero skip.

- [ ] **Step 6: Commit**

```powershell
git add scripts/testing/Invoke-TurboVecFeasibility.ps1 `
        scripts/testing/Test-TurboVecEvidence.ps1 `
        scripts/testing/tests/test_turbovec_evidence_scripts.py
git commit -m "test(turbovec): gate append-only experiment evidence"
```

### Task 9: Run upstream and controlled feasibility campaigns

**Files:**
- Create by runner: `experiments/raw-results/turbovec/{run-id}/...`
- Create by evaluator: `experiments/processed-results/EXP-TV-COMP-001/{run-id}/summary.json`
- Create by evaluator: `experiments/processed-results/EXP-TV-COMP-001/{run-id}/decision.json`
- Create: `experiments/processed-results/EXP-TV-COMP-001/{run-id}/report.md`

**Interfaces:**
- Consumes: all tooling from Tasks 1-8 and externally locked source/model/wheel assets.
- Produces: a schema-valid formal decision and reproducible evidence index.

- [ ] **Step 1: Preserve pre-execution identity**

Record the experiment branch commit/tree, clean status, Windows build, CPU/RAM, power mode, Python/.NET versions, OpenVINO identity, TurboVec source and wheel identity, PdfPig package identity, model revision/tree hash, and free storage. Do not begin measurement if any required identity is absent or mismatched.

- [ ] **Step 2: Run pinned upstream TurboVec tests**

In an external short-path source checkout at the exact commit, run the full Python suite against the published wheel and preserve JUnit output. If Rust 1.89 has been separately approved and installed in an isolated toolchain, also run:

```powershell
cargo test -p turbovec --release --locked
cargo clippy --workspace --all-targets --all-features --locked -- -D warnings
```

If Rust is not approved or App Control blocks it, record the commands as unexecuted/blocked; do not convert them into skips or failures.

- [ ] **Step 3: Run PDF and embedding gates**

Run the PDF MSTest project with TRX output. Run one locked Granite passage/query embedding smoke on CPU and verify 384 finite normalized values. A parser or real-model failure stops the matched campaign and feeds the decision precedence rules.

- [ ] **Step 4: Execute a new measured run**

```powershell
$ControlledPythonExe = (Resolve-Path -LiteralPath $ControlledPythonPath).Path
$LockedModelRoot = (Resolve-Path -LiteralPath $LockedModelPath).Path
$LockedTurboVecWheel = (Resolve-Path -LiteralPath $TurboVecWheelPath).Path
$NewEvidenceRoot = (Resolve-Path -LiteralPath $EvidenceParent).Path
$RunId = "EXP-TV-COMP-001-$([DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ'))-001"

& .\scripts\testing\Invoke-TurboVecFeasibility.ps1 `
  -Mode Measured `
  -PythonExe $ControlledPythonExe `
  -ModelRoot $LockedModelRoot `
  -TurboVecWheel $LockedTurboVecWheel `
  -OutputRoot $NewEvidenceRoot `
  -RunId $RunId
```

The four input path variables are operator-supplied external asset locations and are never committed. The orchestrator resolves and validates them against the manifest before executing.

- [ ] **Step 5: Validate independently and derive results**

Run: `& .\scripts\testing\Test-TurboVecEvidence.ps1 -RunDirectory (Join-Path $NewEvidenceRoot $RunId)`

Run: `python scripts/testing/run_turbovec_feasibility.py evaluate --run-directory (Join-Path $NewEvidenceRoot $RunId) --processed-root experiments/processed-results/EXP-TV-COMP-001`

Re-run the validator against the raw and processed files. Preserve the first invalid output rather than overwriting it; corrections use a new processed revision linked to the same immutable raw run.

- [ ] **Step 6: Commit only admissible evidence**

Before staging, scan for model binaries, wheels, private paths, document text, secrets, oversized files, and unlicensed content. Commit the textual evidence set, hashes, summary, decision, and report only after validation.

```powershell
git add experiments/raw-results/turbovec experiments/processed-results/EXP-TV-COMP-001
git commit -m "evidence(turbovec): record matched feasibility result"
```

### Task 10: Reconcile the formal decision and stop at the gate

**Files:**
- Modify: `docs/architecture/decisions/ADR-TurboVec.md`
- Modify: `docs/requirements/catalogue/Research-Requirements.md`
- Modify: `docs/requirements/catalogue/Functional-Requirements.md`
- Modify: `docs/requirements/MoSCoW-Requirements-v1.2.md`
- Modify: `docs/requirements/Requirements-Traceability-Matrix-v1.3.md`
- Modify: `docs/risks/Licence-Register.md`
- Modify: `docs/risks/Licence-Review-Notes.md`
- Modify: `docs/risks/Risk-Register.md`
- Modify: `experiments/raw-results/turbovec/README.md`
- Modify: `experiments/processed-results/EXP-TV-COMP-001/README.md`
- Create: `docs/testing/TurboVec-Feasibility-Result-v1.md`
- Test: `scripts/testing/tests/test_turbovec_decision_documents.py`

**Interfaces:**
- Consumes: the validator-approved `decision.json` from Task 9.
- Produces: aligned controlled records with no claim beyond the executed evidence.

- [ ] **Step 1: Write failing cross-document tests**

Tests load `decision.json` and assert that every controlled document names the same outcome, candidate repository/commit/licence, evidence run ID, and product status. They reject `Implemented`, `Production ready`, or `Released` unless the result is `INTEGRATE` and a later production campaign exists; this plan never creates that campaign.

- [ ] **Step 2: Confirm failure before document reconciliation**

Run: `python -m unittest scripts.testing.tests.test_turbovec_decision_documents -v`

Expected: failure because existing records still say no candidate is selected and integration is deferred without the new evidence decision.

- [ ] **Step 3: Update records according to the machine decision**

- `INTEGRATE`: mark R-M02 and EXP-TV-COMP-001 evidence complete, select the qualifying configuration as the candidate for a later production plan, and keep F-M25/F-M26/F-M27 unimplemented.
- `DEMONSTRATOR_ONLY`: mark the feasibility question answered, document which production thresholds failed, and keep all product requirements deferred.
- `EXCLUDE`: mark the exact candidate excluded for the tested configuration and preserve Exact retrieval as the only proposed product index.
- `BLOCKED`: retain the requirements as pending/deferred, identify the exact external prerequisite, and make no compatibility or quality conclusion.

In all outcomes, replace `No repository or commit selected` in licence records with the pinned TurboVec identity and MIT disposition, while keeping dependency-distribution approval distinct from technical suitability.

- [ ] **Step 4: Write the bounded result report**

The report includes tested identities, environment, corpus/query hashes, command arithmetic, PDF/embedding results, per-index metrics, threshold table, failures/blocks, selected outcome, claim boundary, and direct evidence links. It states explicitly that no product, frontend, chat route, package, or `main` integration occurred.

- [ ] **Step 5: Run cross-document and full deterministic verification**

Run: `python -m unittest scripts.testing.tests.test_turbovec_decision_documents -v`

Run: `python -m unittest discover -s scripts/testing/tests -p "test_turbovec_*.py" -v`

Run: `dotnet test tools/TurboVec.PdfExtractionSpike.Tests/TurboVec.PdfExtractionSpike.Tests.csproj -c Release`

Run: `git diff --check`

Expected: zero failures, zero unexpected skips, and no controlled-record mismatch.

- [ ] **Step 6: Commit the decision checkpoint**

```powershell
git add docs/architecture/decisions/ADR-TurboVec.md `
        docs/requirements docs/risks docs/testing/TurboVec-Feasibility-Result-v1.md `
        experiments/raw-results/turbovec/README.md `
        experiments/processed-results/EXP-TV-COMP-001/README.md `
        scripts/testing/tests/test_turbovec_decision_documents.py
git commit -m "docs(turbovec): record feasibility decision"
```

- [ ] **Step 7: Stop at the decision gate**

Do not implement product interfaces or UI under this plan. For `INTEGRATE`, request approval for a new production knowledge-library plan based on the selected bit width and evidence hashes. For every other outcome, hand off the report and exact baseline recommendation without creating product work.

---

## Final Review Checklist

- [ ] Every dependency, model, corpus, query, relevance set, embedding matrix, index, command, commit, tree, and output is identified and hashed.
- [ ] Exact and TurboVec consumed identical vectors and queries.
- [ ] Warm-ups are excluded and exactly 30 measured batches per configuration are present.
- [ ] PDF extraction and real Granite embedding gates ran or are explicitly blocked.
- [ ] Recall@10, relative nDCG@10, p50/p95, memory, and storage are present or the Gate A run cannot qualify.
- [ ] Citation correctness, grounded-answer usefulness, packaging, and two-route application checks remain explicitly assigned to Gate B and are not claimed by this plan.
- [ ] The four-outcome decision recomputes from raw evidence.
- [ ] No fake provider contributed to a measured result.
- [ ] No source text, user path, model weight, wheel, third-party checkout, or secret is committed.
- [ ] No product/backend/frontend file changed.
- [ ] The worktree is clean and the branch contains reviewable task-level commits.
