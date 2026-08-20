# TurboVec controlled Windows evidence

## Outcome

Run `amd-20260821` establishes a credible offline command-line demonstrator on
Windows x64. It does **not** pass the release retrieval gate and it is not Intel
hardware evidence. The recorded product decision is therefore **Command-line
demonstrator only**. The WinUI attachment workflow remains selection-only and
displays `Not indexed`; retrieved text is not injected into Granite prompts.

Raw evidence, the wheel, model cache, approval manifest, indexes, and virtual
environment are machine-local and ignored. The committed
`controlled-evidence-index.json` records non-sensitive identities, results, and
SHA-256 digests for independently checking those local files.

## Approved identities

- TurboVec: `1.0.0`, upstream commit
  `ccab9f325e6ce2a270a87daf01ae4e443bcf2d49`, MIT.
- Windows wheel: `turbovec-1.0.0-cp39-abi3-win_amd64.whl`, SHA-256
  `cd855e0b318a57dc57c733f9a62ae98de5192f4f6c2c760e305523e8ceb1b090`.
- Python: `3.12.10`.
- Embedding model: `BAAI/bge-small-en-v1.5`, MIT, 384 dimensions.
- Materialized model-cache manifest SHA-256:
  `d4225dc08ed94bfaf627930d30e6f446a10f80b3192ddc16a95f92078627b10a`.
- Dependency lock SHA-256:
  `438b20b685916055f02e9a207d6bb1466ed30669cb0404bbfa1661e409bd7b35`.
- Critical locked packages observed by `doctor`: FastEmbed `0.8.0`, NumPy
  `2.5.2`, ONNX Runtime `1.29.0`, and TurboVec `1.0.0`.

The wheel was acquired separately with `pip download`, then `doctor` proved
that its executable distribution files matched the installed distribution.
No global package was installed or updated.

## Environment

- Microsoft Windows 11 Pro, 64-bit, version/build `10.0.26200`.
- AMD Ryzen 7 8845HS with Radeon 780M Graphics, 8 cores / 16 logical processors.
- Requested and actual embedding provider: `CPUExecutionProvider`.
- Peak benchmark working set: 249,491,456 bytes.

This is AMD feasibility evidence. Intel behavior, acceleration, and performance
remain unknown.

## Retained ignored evidence

The following locations are relative to `$ControlledRoot`; none is committed:

- `approved-input.json` — actual approved input and local cache binding.
- `artifacts/turbovec-1.0.0-cp39-abi3-win_amd64.whl` — pinned wheel.
- `model-cache/` — materialized approved BGE cache.
- `runs/fixture-index-final/` — matched float32, 2-bit, and 4-bit index package.
- `runs/query-2bit.json` and `runs/query-4bit.json` — successful persisted-route queries.
- `runs/benchmark-final2/results.json` — canonical benchmark evidence.
- `runs/benchmark-final2/summary.md` — evidence summary.
- `runs/benchmark-final2/manifest.json` — atomic evidence artifact manifest.
- `runs/sha256-manifest.json` — hashes and controlled-root-relative paths for
  the retained doctor, index, both queries, index package, and benchmark files.

## Reproduction with new destinations

Run setup from a repository PowerShell. `$ApprovedFastEmbedCache` must point to
the already reviewed BGE cache and `$Python` to the locked Python 3.12 virtual
environment. Network is permitted only for the explicit wheel acquisition.

```powershell
$RepoRoot = (Resolve-Path '.').Path
$ControlledRoot = Join-Path $RepoRoot '.controlled\turbovec\amd-20260821'
$Python = '<locked-python-3.12.10-venv>\Scripts\python.exe'
$ApprovedFastEmbedCache = '<approved-fastembed-cache>'
$ModelCache = Join-Path $ControlledRoot 'model-cache'
$Artifacts = Join-Path $ControlledRoot 'artifacts'
$Runs = Join-Path $ControlledRoot 'runs'

New-Item -ItemType Directory -Force -Path $Artifacts, $Runs, $ModelCache | Out-Null
& $Python -m pip download --only-binary=:all: --no-deps --dest $Artifacts 'turbovec==1.0.0'

# Materialize the reviewed cache because the production identity algorithm
# deliberately rejects Hugging Face cache symlinks/reparse points.
Get-ChildItem -LiteralPath $ApprovedFastEmbedCache -Force |
    Copy-Item -Destination $ModelCache -Recurse -Force

$env:PYTHONPATH = Join-Path $RepoRoot 'scripts\turbovec'
$env:PYTHONSAFEPATH = '1'
$ModelManifestSha = & $Python -P -c "from pathlib import Path; from granite_turbovec.cli import _model_manifest_sha256; import sys; print(_model_manifest_sha256(Path(sys.argv[1])))" $ModelCache
$Wheel = (Get-ChildItem -LiteralPath $Artifacts -Filter '*.whl').FullName
$WheelSha = (Get-FileHash -Algorithm SHA256 -LiteralPath $Wheel).Hash.ToLowerInvariant()
```

Create `$ControlledRoot\approved-input.json` with approval status `approved`,
the identities above, `$WheelSha`, `$ModelManifestSha`, and the resolved
`$ModelCache`. Do not use `approved-input.example.json` unchanged: its hashes
are intentionally invalid placeholders.

All ordinary research operations are offline and go through the wrapper:

```powershell
$env:GRANITE_TURBOVEC_PYTHON = $Python
$env:GRANITE_TURBOVEC_WHEEL = $Wheel
$Wrapper = Join-Path $RepoRoot 'scripts\turbovec\Invoke-TurboVecResearch.ps1'
$Approval = Join-Path $ControlledRoot 'approved-input.json'
$Index = Join-Path $Runs 'fixture-index-reproduction'
$Evidence = Join-Path $Runs 'benchmark-reproduction'

powershell -NoProfile -ExecutionPolicy Bypass -File $Wrapper doctor `
    --approved-input $Approval
powershell -NoProfile -ExecutionPolicy Bypass -File $Wrapper index `
    --approved-input $Approval `
    --input (Join-Path $RepoRoot 'scripts\turbovec\tests\fixtures\knowledge') `
    --output $Index --bits 2 4
powershell -NoProfile -ExecutionPolicy Bypass -File $Wrapper query `
    --approved-input $Approval --index $Index `
    --text 'Which document explains how retrieval finds relevant chunks for a query?' `
    --top-k 5 --route 4bit
powershell -NoProfile -ExecutionPolicy Bypass -File $Wrapper benchmark `
    --approved-input $Approval `
    --fixture (Join-Path $RepoRoot 'scripts\turbovec\tests\fixtures\evaluation.json') `
    --index $Index --output $Evidence
```

The wrapper fixes `HF_HUB_OFFLINE=1`, `TRANSFORMERS_OFFLINE=1`, and telemetry
off. There is no automatic download path. Every index/evidence destination is
no-overwrite, so use a new destination for another run.

## Results

`doctor`, `index`, and both explicit 2-bit and 4-bit persisted-index queries
exited 0. The corpus produced 22 chunks. Both top-5 queries ranked the direct
`retrieval.md` definition first; the 4-bit top result scored 0.8583944439888.

The benchmark used the existing matched index, seven ordered queries, top-k 10,
one cold vector-search sample and five warm samples per route. Query embeddings
were created once and reused across float32, 2-bit, and 4-bit searches. It
atomically retained complete evidence and exited 40 because the unchanged
simultaneous gate failed.

| Gate | Actual | Threshold | Outcome |
|---|---:|---:|:---:|
| Recall@10, 4-bit | 0.9571428571428573 | >= 0.85 | pass |
| MRR ratio, 4-bit / float32 | 1.0 | >= 0.90 | pass |
| Hit@5 delta, 4-bit - float32 | 0.0 | >= -0.05 | pass |
| Size ratio, 4-bit / raw float32 | 13.352716619318182 | <= 0.25 | fail |
| Warm median latency ratio, 4-bit / float32 | 2.383012969628705 | <= 1.0 | fail |

The raw float32 vectors were 33,792 bytes (33,920-byte NPY). Persisted TurboVec
containers were 248,559 bytes at 2-bit and 451,215 bytes at 4-bit. Warm median
vector-search latency was 0.00006239999493118376 seconds for float32,
0.0001514999894425273 seconds for 2-bit, and 0.00014869999722577631 seconds for
4-bit. Document embedding took 9.788840799999889 seconds; the index operation
took 12.502798000001349 seconds. Benchmark query embedding took
0.2757184999936726 seconds and the benchmark end-to-end phase took
2.986802700004773 seconds.

Deterministic tests: 166 passed, one controlled test skipped. With the controlled
approval and offline environment configured: 167 passed, zero skipped. The
separate Release GGUF verification passed 60 tests and retained one allowed
model-dependent skip (the controlled GGUF runtime/model was not configured);
its self-contained application build succeeded with zero warnings and errors.

## Promotion gates and limitations

| # | Approved promotion gate | State |
|---:|---|:---:|
| 1 | Exact upstream, runtime, dependency, and wheel identity lock | Pass |
| 2 | Controlled embedding model identity, cache manifest, licence, and provider | Pass |
| 3 | Offline Windows x64 persistence, reload, and query for both TurboVec routes | Pass |
| 4 | Intended Intel hardware evidence | Unknown |
| 5 | Recall, MRR ratio, and Hit@5 quality thresholds | Pass |
| 6 | Four-bit persisted-size threshold | Fail |
| 7 | Four-bit warm median latency threshold | Fail |
| 8 | Failure, privacy, corruption, and index-identity mismatch tests | Pass |
| 9 | Stable GGUF route remains verified and undisplaced | Pass |
| 10 | ADR updated to an allowed release-role outcome | Pass |

Because gates 6 and 7 fail and gate 4 is unknown, the outcome remains exactly
**Command-line demonstrator only**.

The fixture is deliberately small (22 chunks and seven queries), and sub-
millisecond vector-search ratios are sensitive to host scheduling. One cold and
five warm samples follow the declared method but do not establish production
capacity. The experiment does not test Intel hardware, integrate TurboVec into
the app, index user attachments, or inject retrieval context into a prompt.
Network was used only during the separate pinned-wheel setup.
