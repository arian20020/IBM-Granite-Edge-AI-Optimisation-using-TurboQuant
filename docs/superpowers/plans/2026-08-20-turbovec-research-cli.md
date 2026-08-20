# TurboVec Research CLI and Matched Retrieval Gate Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a pinned, offline-capable command-line demonstrator that extracts bounded text, chunks and embeds it, creates matched float32 and TurboVec indexes, queries them, and emits reproducible evidence for the TurboVec release-role decision.

**Architecture:** A PowerShell entry point validates an explicitly supplied Python environment and dispatches to a small Python package. Framework-neutral extraction, chunking, manifests, and metrics are independently unit tested with deterministic vectors; FastEmbed and TurboVec are loaded only by concrete adapters and controlled smoke tests. Every index is bound to source hashes, chunking identity, embedding identity, dimension, and backend so incompatible artefacts fail closed.

**Tech Stack:** PowerShell 5.1+, Python 3.12, NumPy 2.5.2, FastEmbed 0.8.0, ONNX Runtime 1.29.0 CPU, TurboVec 1.0.0, Python `unittest`

---

## File Structure

| Path | Responsibility |
|---|---|
| `scripts/turbovec/Invoke-TurboVecResearch.ps1` | Safe wrapper, explicit interpreter selection, offline environment, exit propagation |
| `scripts/turbovec/requirements.lock.txt` | Exact direct and transitive Python version lock captured from the approved environment |
| `scripts/turbovec/approved-input.example.json` | Non-secret example manifest for interpreter, wheel, model, licence, and hashes |
| `scripts/turbovec/granite_turbovec/contracts.py` | Dataclasses and fixed diagnostic/exit-code contracts |
| `scripts/turbovec/granite_turbovec/text_pipeline.py` | Bounded discovery, strict UTF-8 extraction, deterministic chunking, stable IDs |
| `scripts/turbovec/granite_turbovec/embedding.py` | Embedder protocol and offline FastEmbed adapter |
| `scripts/turbovec/granite_turbovec/indexes.py` | Float32 baseline and TurboVec stable-ID adapters |
| `scripts/turbovec/granite_turbovec/manifest.py` | Canonical manifest hashing, validation, atomic JSON writes |
| `scripts/turbovec/granite_turbovec/benchmark.py` | Matched ranking metrics and timing aggregation |
| `scripts/turbovec/granite_turbovec/cli.py` | `doctor`, `index`, `query`, and `benchmark` orchestration |
| `scripts/turbovec/tests/` | Standard-library unit and controlled integration tests |
| `scripts/turbovec/tests/fixtures/knowledge/` | Small deterministic `.txt`/`.md` semantic corpus |
| `scripts/turbovec/tests/fixtures/evaluation.json` | Fixed queries, relevant chunk/source judgements, and top-k |
| `docs/evidence/turbovec/README.md` | Evidence boundary and run instructions |
| `docs/architecture/decisions/ADR-TurboVec.md` | Final gate outcome only after evidence exists |

### Task 1: Create the safe CLI wrapper and pinned environment contract

**Files:**
- Create: `scripts/turbovec/Invoke-TurboVecResearch.ps1`
- Create: `scripts/turbovec/requirements.lock.txt`
- Create: `scripts/turbovec/approved-input.example.json`
- Create: `scripts/turbovec/tests/test_wrapper_contract.py`

- [ ] **Step 1: Write the failing wrapper contract test**

```python
import pathlib, unittest

ROOT = pathlib.Path(__file__).resolve().parents[3]

class WrapperContractTests(unittest.TestCase):
    def test_wrapper_requires_explicit_python_and_forces_offline_mode(self):
        text = (ROOT / "scripts/turbovec/Invoke-TurboVecResearch.ps1").read_text("utf-8")
        self.assertIn("GRANITE_TURBOVEC_PYTHON", text)
        self.assertIn("HF_HUB_OFFLINE", text)
        self.assertIn("TRANSFORMERS_OFFLINE", text)
        self.assertIn("-m", text)
        self.assertIn("granite_turbovec.cli", text)
        self.assertNotIn("Invoke-Expression", text)
        self.assertNotIn("Start-Process", text)

if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 2: Run the test and verify RED**

```powershell
python -m unittest scripts.turbovec.tests.test_wrapper_contract -v
```

Expected: ERROR because the wrapper does not exist.

- [ ] **Step 3: Implement the minimal wrapper**

The wrapper must resolve the repository root from `$PSScriptRoot`, require an existing interpreter through `GRANITE_TURBOVEC_PYTHON` or `-PythonPath`, set `HF_HUB_OFFLINE=1`, `TRANSFORMERS_OFFLINE=1`, `HF_HUB_DISABLE_TELEMETRY=1`, prepend only the package root to `PYTHONPATH`, and invoke:

```powershell
& $resolvedPython -m granite_turbovec.cli @RemainingArgs
exit $LASTEXITCODE
```

Use `Test-Path -LiteralPath` and `Resolve-Path -LiteralPath`; never construct a command string. The example input manifest contains these fields with obvious non-secret example values:

```json
{
  "schema_version": 1,
  "python_version": "3.12.10",
  "turbovec_version": "1.0.0",
  "turbovec_source_commit": "ccab9f325e6ce2a270a87daf01ae4e443bcf2d49",
  "turbovec_wheel_sha256": "64 lowercase hex characters",
  "embedding_model": "BAAI/bge-small-en-v1.5",
  "embedding_model_manifest_sha256": "64 lowercase hex characters",
  "embedding_model_license": "MIT",
  "model_cache_root": "C:\\approved\\fastembed-cache"
}
```

Generate `requirements.lock.txt` from the controlled spike environment with `python -m pip freeze`, then retain exact `==` pins and remove environment-specific paths.

- [ ] **Step 4: Run the test and PowerShell parser check**

```powershell
python -m unittest scripts.turbovec.tests.test_wrapper_contract -v
$errors = $null
[System.Management.Automation.Language.Parser]::ParseFile(
  (Resolve-Path '.\scripts\turbovec\Invoke-TurboVecResearch.ps1'),
  [ref]$null, [ref]$errors) | Out-Null
if ($errors.Count -ne 0) { throw ($errors | Out-String) }
```

Expected: PASS and zero parser errors.

- [ ] **Step 5: Commit**

```powershell
git add -- scripts/turbovec
git commit -m "build(turbovec): add pinned research CLI entrypoint"
```

### Task 2: Define bounded text extraction and deterministic chunking

**Files:**
- Create: `scripts/turbovec/granite_turbovec/__init__.py`
- Create: `scripts/turbovec/granite_turbovec/contracts.py`
- Create: `scripts/turbovec/granite_turbovec/text_pipeline.py`
- Create: `scripts/turbovec/tests/test_text_pipeline.py`
- Create: `scripts/turbovec/tests/fixtures/knowledge/granite.txt`
- Create: `scripts/turbovec/tests/fixtures/knowledge/retrieval.md`

- [ ] **Step 1: Write failing extraction and chunking tests**

```python
class TextPipelineTests(unittest.TestCase):
    def test_extracts_only_txt_and_md_in_stable_relative_order(self):
        docs = discover_documents(self.fixture_root, max_files=8, max_bytes=1_000_000)
        self.assertEqual([d.relative_path for d in docs], ["granite.txt", "retrieval.md"])

    def test_chunks_are_deterministic_bounded_and_overlap(self):
        chunks = chunk_document(
            Document("notes.md", "alpha beta gamma delta epsilon"),
            max_chars=18, overlap_chars=6)
        self.assertGreater(len(chunks), 1)
        self.assertTrue(all(len(chunk.text) <= 18 for chunk in chunks))
        self.assertEqual(chunks, chunk_document(
            Document("notes.md", "alpha beta gamma delta epsilon"), 18, 6))
        self.assertTrue(all(0 <= chunk.chunk_id <= 2**64 - 1 for chunk in chunks))

    def test_invalid_utf8_and_unsupported_files_fail_with_fixed_codes(self):
        with self.assertRaisesRegex(ResearchError, "input-invalid-utf8"):
            decode_document(b"\xff\xfe", "bad.txt")
        with self.assertRaisesRegex(ResearchError, "input-unsupported-type"):
            validate_extension("report.pdf")
```

- [ ] **Step 2: Run tests and verify RED**

```powershell
$env:PYTHONPATH = (Resolve-Path '.\scripts\turbovec')
python -m unittest scripts.turbovec.tests.test_text_pipeline -v
```

Expected: import failure because the package is absent.

- [ ] **Step 3: Implement contracts and text pipeline**

Use frozen dataclasses:

```python
@dataclass(frozen=True)
class Document:
    relative_path: str
    text: str
    sha256: str = ""

@dataclass(frozen=True)
class Chunk:
    chunk_id: int
    relative_path: str
    start: int
    end: int
    text: str

class ResearchError(RuntimeError):
    def __init__(self, code: str):
        super().__init__(code)
        self.code = code
```

Discovery accepts one file or one directory, does not follow reparse-point/symlink directories, sorts case-insensitively by relative path, accepts `.txt`/`.md`, caps at 64 files, caps each file at 8 MiB and the total at 32 MiB, and decodes UTF-8 strictly after optionally removing a UTF-8 BOM. Chunking uses 1,200 characters with 200-character overlap, prefers the last paragraph/newline/space boundary after 60% of the window, guarantees forward progress, and derives `chunk_id` from the first eight bytes of SHA-256 over UTF-8 `relative_path + NUL + start + NUL + end + NUL + text` interpreted as unsigned big-endian.

- [ ] **Step 4: Run tests and verify GREEN**

Expected: all text-pipeline tests pass.

- [ ] **Step 5: Commit**

```powershell
git add -- scripts/turbovec/granite_turbovec scripts/turbovec/tests
git commit -m "feat(turbovec): add bounded deterministic text pipeline"
```

### Task 3: Add offline embedding and matched index adapters

**Files:**
- Create: `scripts/turbovec/granite_turbovec/embedding.py`
- Create: `scripts/turbovec/granite_turbovec/indexes.py`
- Create: `scripts/turbovec/tests/test_indexes.py`

- [ ] **Step 1: Write failing adapter tests with deterministic vectors**

```python
class IndexAdapterTests(unittest.TestCase):
    def test_float32_and_fake_quantized_indexes_preserve_stable_ids(self):
        vectors = np.eye(8, dtype=np.float32)
        ids = np.arange(101, 109, dtype=np.uint64)
        baseline = Float32Index(vectors, ids)
        scores, found = baseline.search(vectors[:1], k=3)
        self.assertEqual(101, int(found[0, 0]))
        self.assertEqual((1, 3), scores.shape)

    def test_rejects_non_finite_wrong_dimension_and_duplicate_ids(self):
        with self.assertRaisesRegex(ResearchError, "vector-non-finite"):
            validate_vectors(np.array([[np.nan] * 8], dtype=np.float32), 8)
        with self.assertRaisesRegex(ResearchError, "vector-id-duplicate"):
            validate_ids(np.array([1, 1], dtype=np.uint64))
```

- [ ] **Step 2: Run tests and verify RED**

Expected: import failure for `Float32Index`.

- [ ] **Step 3: Implement adapters**

Define an `Embedder` protocol with `dimension`, `model_identity`, `embed_documents`, and `embed_queries`. `FastEmbedder` constructs:

```python
TextEmbedding(
    model_name="BAAI/bge-small-en-v1.5",
    cache_dir=approved_cache_root,
    providers=["CPUExecutionProvider"],
    local_files_only=True)
```

It normalises output to contiguous finite float32 and records actual ONNX providers. `Float32Index.search` computes inner products and returns stable IDs using deterministic score-descending/ID-ascending tie-breaking. `TurboVecIndex` wraps `turbovec.IdMapIndex(dim=dimension, bit_width=bits)`, uses `add_with_ids`, `search`, `write`, and `load`, and permits only bits 2 or 4 in this research CLI.

- [ ] **Step 4: Run unit tests and controlled package smoke**

```powershell
python -m unittest scripts.turbovec.tests.test_indexes -v
& $env:GRANITE_TURBOVEC_PYTHON -c "import fastembed,numpy,turbovec; print(turbovec.__version__)"
```

Expected: unit tests pass; controlled interpreter prints `1.0.0`.

- [ ] **Step 5: Commit**

```powershell
git add -- scripts/turbovec/granite_turbovec scripts/turbovec/tests/test_indexes.py
git commit -m "feat(turbovec): add offline embedding and index adapters"
```

### Task 4: Add canonical manifests and atomic index promotion

**Files:**
- Create: `scripts/turbovec/granite_turbovec/manifest.py`
- Create: `scripts/turbovec/tests/test_manifest.py`

- [ ] **Step 1: Write failing manifest tests**

```python
class ManifestTests(unittest.TestCase):
    def test_canonical_manifest_round_trips_and_rejects_mismatch(self):
        manifest = fixture_manifest()
        write_manifest_atomic(self.root / "manifest.json", manifest)
        loaded = load_and_validate_manifest(self.root / "manifest.json", manifest.identity)
        self.assertEqual(manifest, loaded)
        with self.assertRaisesRegex(ResearchError, "index-embedding-mismatch"):
            load_and_validate_manifest(
                self.root / "manifest.json",
                dataclasses.replace(manifest.identity, embedding_model="other"))

    def test_manifest_contains_hashes_not_document_text_or_absolute_paths(self):
        encoded = canonical_json(fixture_manifest())
        self.assertNotIn("secret document sentence", encoded)
        self.assertNotIn("C:\\\\Users", encoded)
        self.assertRegex(encoded, r'"sha256":"[0-9a-f]{64}"')
```

- [ ] **Step 2: Run tests and verify RED**

Expected: manifest module import fails.

- [ ] **Step 3: Implement canonical manifest and staged promotion**

Use sorted-key UTF-8 JSON with `ensure_ascii=False`, compact separators for hashing, schema version 1, and fixed identity fields from the approved design. Write `manifest.json.tmp-<operation-id>` in the target directory, flush and `os.fsync`, then `os.replace` it. Index creation writes all artefacts to a sibling `.staging-<operation-id>` directory, validates them by reloading, then promotes to a new destination only; it never overwrites a valid existing index.

Validate schema version, source hashes, chunking version/parameters, embedding identity/model manifest hash, vector dimension, index format, bit width, package version/commit, and stable-ID metadata count before query.

- [ ] **Step 4: Run tests and verify GREEN**

Expected: all manifest tests pass.

- [ ] **Step 5: Commit**

```powershell
git add -- scripts/turbovec/granite_turbovec/manifest.py scripts/turbovec/tests/test_manifest.py
git commit -m "feat(turbovec): bind indexes to canonical manifests"
```

### Task 5: Implement matched metrics and benchmark evidence

**Files:**
- Create: `scripts/turbovec/granite_turbovec/benchmark.py`
- Create: `scripts/turbovec/tests/test_benchmark.py`
- Create: `scripts/turbovec/tests/fixtures/evaluation.json`

- [ ] **Step 1: Write failing metric tests**

```python
class BenchmarkTests(unittest.TestCase):
    def test_metrics_use_matched_rankings_and_judgements(self):
        exact = [[10, 20, 30], [40, 50, 60]]
        candidate = [[10, 30, 99], [50, 40, 60]]
        relevant = [{10}, {40}]
        metrics = evaluate_rankings(exact, candidate, relevant, k=3)
        self.assertAlmostEqual(4 / 6, metrics.recall_at_k)
        self.assertAlmostEqual(0.75, metrics.mrr)
        self.assertEqual(1.0, metrics.hit_at_k)

    def test_gate_requires_quality_size_and_latency_together(self):
        passing = GateMetrics(recall_at_10=.86, mrr_ratio=.92,
            hit_at_5_delta=-.03, size_ratio=.20, latency_ratio=.80)
        self.assertTrue(evaluate_gate(passing).passed)
        self.assertFalse(evaluate_gate(dataclasses.replace(
            passing, recall_at_10=.84)).passed)
```

- [ ] **Step 2: Run tests and verify RED**

Expected: benchmark module import fails.

- [ ] **Step 3: Implement exact formulas and output schema**

Compute Recall@k as mean set overlap divided by `k`, MRR from the rank of the first judged relevant result, Hit@k as the fraction with any judged relevant result, and ratios against the same-run float32 route. Aggregate cold and warm timings separately with median and p95. Evaluate the approved thresholds: Recall@10 >= 0.85, MRR ratio >= 0.90, Hit@5 delta >= -0.05, stored 4-bit size ratio <= 0.25, and vector-search median ratio <= 1.0.

`evaluation.json` uses:

```json
{
  "schema_version": 1,
  "top_k": 10,
  "queries": [
    {"id": "q01", "text": "What does TurboVec compress?", "relevant_sources": ["retrieval.md"]}
  ]
}
```

- [ ] **Step 4: Run tests and verify GREEN**

Expected: metric and gate tests pass.

- [ ] **Step 5: Commit**

```powershell
git add -- scripts/turbovec/granite_turbovec/benchmark.py scripts/turbovec/tests/test_benchmark.py scripts/turbovec/tests/fixtures/evaluation.json
git commit -m "test(turbovec): add matched retrieval gate metrics"
```

### Task 6: Implement `doctor`, `index`, `query`, and `benchmark`

**Files:**
- Create: `scripts/turbovec/granite_turbovec/cli.py`
- Create: `scripts/turbovec/tests/test_cli.py`

- [ ] **Step 1: Write failing CLI tests using fake adapters**

```python
class CliTests(unittest.TestCase):
    def test_doctor_emits_machine_readable_identity_without_paths(self):
        result = run_cli(["doctor", "--approved-input", str(self.approved)])
        self.assertEqual(0, result.exit_code)
        payload = json.loads(result.stdout)
        self.assertEqual("1.0.0", payload["turbovec_version"])
        self.assertNotIn(str(pathlib.Path.home()), result.stdout)

    def test_query_rejects_manifest_mismatch_before_loading_index(self):
        result = run_cli(["query", "--index", str(self.bad_index),
                          "--text", "question", "--top-k", "5"])
        self.assertEqual(31, result.exit_code)
        self.assertEqual("index-embedding-mismatch", json.loads(result.stderr)["code"])

    def test_index_does_not_replace_existing_destination(self):
        result = run_cli(["index", "--input", str(self.docs),
                          "--output", str(self.existing), "--bits", "4"])
        self.assertEqual(22, result.exit_code)
```

- [ ] **Step 2: Run tests and verify RED**

Expected: CLI module import fails.

- [ ] **Step 3: Implement argparse orchestration and fixed exits**

Use one parser with required subcommands. Fixed exits are: `0` success, `2` argument error, `20` input error, `21` extraction/limit error, `22` destination exists, `30` environment/dependency mismatch, `31` index/manifest mismatch, `32` embedding failure, `33` TurboVec failure, and `40` benchmark gate failed.

All success output is one JSON document on stdout. All expected failures are one JSON diagnostic on stderr containing only `schema_version`, `code`, and safe relative identity when applicable. Unexpected exceptions map to `research-unexpected-failure` without stack trace unless `GRANITE_TURBOVEC_DEBUG=1`.

`index` writes `chunks.jsonl`, `vectors-float32.npy`, `ids.npy`, `baseline-results` metadata, `index-2bit.tvim` and/or `index-4bit.tvim`, and `manifest.json` through staging. `query` returns source-relative path, chunk offsets, score, and a maximum 240-character whitespace-normalised excerpt. `benchmark` repeats warm search at least five times, writes `results.json` and `summary.md`, and exits 40 when 4-bit fails the gate.

- [ ] **Step 4: Run CLI tests and verify GREEN**

```powershell
$env:PYTHONPATH = (Resolve-Path '.\scripts\turbovec')
python -m unittest scripts.turbovec.tests.test_cli -v
```

Expected: all CLI tests pass.

- [ ] **Step 5: Commit**

```powershell
git add -- scripts/turbovec/granite_turbovec/cli.py scripts/turbovec/tests/test_cli.py
git commit -m "feat(turbovec): add research CLI commands"
```

### Task 7: Run controlled Windows evidence and document the decision boundary

**Files:**
- Create: `docs/evidence/turbovec/README.md`
- Create under an ignored controlled evidence root: raw `doctor.json`, `results.json`, `summary.md`, and SHA-256 manifest
- Modify only after evidence review: `docs/architecture/decisions/ADR-TurboVec.md`

- [ ] **Step 1: Run all deterministic tests**

```powershell
$env:PYTHONPATH = (Resolve-Path '.\scripts\turbovec')
python -m unittest discover -s .\scripts\turbovec\tests -v
```

Expected: zero failures and zero errors.

- [ ] **Step 2: Run doctor with the approved interpreter and input manifest**

```powershell
$env:GRANITE_TURBOVEC_PYTHON = 'C:\approved\turbovec\.venv\Scripts\python.exe'
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\turbovec\Invoke-TurboVecResearch.ps1 doctor --approved-input C:\approved\turbovec\approved-input.json
```

Expected: exit 0; JSON reports Python 3.12.x, TurboVec 1.0.0, FastEmbed 0.8.0, NumPy 2.5.2, ONNX Runtime 1.29.0, CPU provider, exact wheel/model hashes, Windows x64, and actual processor identity.

- [ ] **Step 3: Run fixture indexing, query, and matched benchmark**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\turbovec\Invoke-TurboVecResearch.ps1 index --input .\scripts\turbovec\tests\fixtures\knowledge --output C:\approved\turbovec\runs\fixture-index --bits 2 4 --approved-input C:\approved\turbovec\approved-input.json
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\turbovec\Invoke-TurboVecResearch.ps1 query --approved-input C:\approved\turbovec\approved-input.json --index C:\approved\turbovec\runs\fixture-index --text "What does TurboVec compress?" --top-k 5
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\turbovec\Invoke-TurboVecResearch.ps1 benchmark --approved-input C:\approved\turbovec\approved-input.json --index C:\approved\turbovec\runs\fixture-index --fixture .\scripts\turbovec\tests\fixtures\evaluation.json --output C:\approved\turbovec\runs\fixture-evidence
```

Expected: index and query exit 0; benchmark exit 0 only when the approved 4-bit gate passes, otherwise exit 40 with complete evidence retained.

- [ ] **Step 4: Repeat on intended Intel hardware**

Use the same approved inputs, fixture hashes, power profile, repetition count, and commands. Record actual CPU, ONNX provider, cold/warm state, and background-load notes. Do not combine AMD and Intel measurements into one performance claim.

- [ ] **Step 5: Write the evidence README and ADR outcome**

The README records commands, hashes, raw evidence locations, limitations, and whether each gate passed. Update `ADR-TurboVec.md` to exactly one of **Implement**, **Command-line demonstrator only**, **Defer**, or **Exclude**. If Intel evidence is unavailable, the outcome cannot be **Implement**.

- [ ] **Step 6: Run repository verification**

```powershell
python -m unittest discover -s .\scripts\turbovec\tests -v
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\gguf-runtime\Invoke-GgufChatVerification.ps1
git diff --check
```

Expected: TurboVec tests pass; GGUF verification remains green with only its controlled model-dependent skip; diff check reports no errors.

- [ ] **Step 7: Commit documentation and non-sensitive evidence index**

```powershell
git add -- scripts/turbovec docs/evidence/turbovec docs/architecture/decisions/ADR-TurboVec.md
git commit -m "research(turbovec): record matched Windows retrieval gate"
```

Do not commit model files, wheels, absolute local paths, raw private documents, virtual environments, indexes derived from private content, or uncontrolled benchmark output.
