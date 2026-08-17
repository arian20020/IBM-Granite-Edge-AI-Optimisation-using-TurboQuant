# Workbook 05 Phase 3 C1 Asset-Lock Runbook

## Purpose

This runbook controls **C1 — immutable model and conversion assets** for campaign
`GTQ-WB05-MF-v1`, Route A `route-a-merged-openvino`.

C1 creates a reproducible identity for the IBM Granite 4.1 3B source files,
tokenizer files, conversion environment, conversion command, and converted
OpenVINO files. It does **not** execute the model and does not prove that
TurboQuant, scalar cache quantisation, or any other KV-cache codec ran.

Follow the [clean dependency-preflight runbook](phase3-dependency-preflight-runbook.md)
for dependency-machine preparation, dispatch, evidence review, independent
hashing, and owner acceptance. Do not replace that controlled procedure with an
improvised local package-install sequence.

## Current implementation boundary

The Phase 3 asset workflow has two explicit operations:

```text
offline-fixture
live-asset-lock
```

`offline-fixture` is the only permitted operation until the clean Windows
Python 3.12.10 dependency preflight has produced an independently validated
and project-owner-accepted decision SHA-256. The current workflow deliberately
fails closed if `live-asset-lock` is selected before that gate is completed.

Passing the repository workflow, offline fixture, or dependency installation
must never be reported as model download, conversion, loading, generation,
codec activation, performance, or quality evidence.

PR `#72` implements the separate dependency-preflight repository boundary. Its
implementation does not itself constitute live dependency acceptance and does
not remove the asset workflow's existing block.

## Accepted read-only Phase 2 prerequisites

Do not delete, move, modify, repair, or replace these paths:

```text
Runtime installation:
C:\w5a\phase2-31391119557-4\i-ov

Runtime decision:
C:\w5a\accepted-route-a-runtime-31656417607-1\decision.json

GenAI installation:
C:\w5a\phase2-31661571860-1\i-genai

GenAI decision:
C:\w5a\accepted-route-a-genai-31661571860-1\decision.json
```

Accepted identities:

```text
Runtime source commit:
b9a1f201c109e0bed74763934f79483cf6c4cbf4

Runtime decision SHA-256:
5dae00b38edb9a20f2d82a99dcf4cd1e2d4e7aeaaf6984873ccc55abccf3ae38

GenAI source commit:
bd8d6542e3ca1ac30042d5d8d4202ce00b5f4af0

GenAI decision SHA-256:
0f273e1f345e1512e8e159af9b83f9ad521a26aa5e0ae813f2599161a5cb4a79
```

Every live C1 attempt must revalidate these exact decisions and installed
outputs before it contacts the model repository or invokes conversion tooling.

## Controlled storage boundaries

```text
C:\w5a\  accepted Runtime and GenAI prerequisites; read-only
C:\w5m\  immutable source and converted model assets
C:\w5c\  C1 dependency/conversion/evidence workspaces
C:\w5r\  later measured-run workspaces; unused by C1
```

All roots and child workspaces must be normal local directories. UNC paths,
device paths, junctions, symbolic links, mount points, and Windows reparse
points are rejected. Existing attempt, source, and converted directories are
never silently reused or deleted.

At least exactly `53,687,091,200` free bytes (50 GiB) are required before a
Granite 4.1 3B source download or conversion. Passing the disk check alone does
not authorise a download.

## Reviewed conversion candidate

The controlling direct dependency candidate is:

```text
optimum-intel @ git+https://github.com/huggingface/optimum-intel.git@a3b6012a4c02f4147260da4d4601bb6a3c0d2bb0
optimum @ git+https://github.com/huggingface/optimum.git@982e495540364f95da1e4b6f62d2d4e5907d08fd
transformers==5.5.0
huggingface-hub==1.21.0
nncf==3.2.0
openvino==2026.2.1
openvino-tokenizers==2026.2.1.0
```

The reviewed conversion is:

```text
Model repository: ibm-granite/granite-4.1-3b
Task: text-generation-with-past
Weight format: INT4
Symmetry: asymmetric
Group size: 128
Ratio: 1.0
Remote model code: disabled
```

`--trust-remote-code` is forbidden. A tool requiring remote repository Python
code blocks the candidate instead of weakening the security boundary.

## Before merging the C1 or dependency-preflight implementation

PR verification must use its exact current head, not an earlier green commit.
The following must pass where the changed paths trigger them:

```text
Build and test
Workbook 05 documented build
Workbook 05 Route A Runtime controlled resume contract
Workbook 05 Phase 3 assets — repository contract
Workbook 05 Phase 3 dependency preflight — repository contract
```

Run the repository-controlled Phase 3 gate locally or in CI:

```powershell
& '.\scripts\testing\Validate-Workbook05-Phase3.ps1' `
    -RepositoryRoot (Get-Location).Path `
    -PythonPath 'python'
```

The final controlled lines must include:

```text
WORKBOOK05_BUILD_STAGE_GATE_PASS
WORKBOOK05_PHASE3_GATE_PASS
```

Also require:

```powershell
git diff --check
git status --short
git ls-files |
    Select-String -Pattern '\.(safetensors|bin|xml|onnx|exe|dll|pyd|whl|zip)$'
```

No generated model, tokenizer, IR, executable, library, wheel, or archive may be
committed as evidence.

## Safe asset-workflow rehearsal after merge

Pull requests run only the GitHub-hosted repository-contract jobs. They cannot
reach the self-hosted Intel runner.

After the C1 implementation is merged, the first asset-workflow rehearsal must
still use:

```text
Actions
→ Workbook 05 Phase 3 assets
→ Run workflow

Use workflow from: main
operation: offline-fixture
confirm_live_asset_lock: unchecked
accepted_dependency_preflight_sha256: blank
```

Expected job order:

```text
Verify Phase 3 repository contract
        ↓
Collect controlled C1 asset evidence
        ↓
Validate C1 artifact as untrusted data
```

The offline fixture performs no network model download and no model execution.
It creates disposable fixture payloads beneath runner temporary storage and
uploads only text/JSON/CSV/log evidence.

Expected artifact identity:

```text
workbook-05-phase3-assets-<workflow-run>-<attempt>
```

## Clean dependency preflight gate

The controlling instructions are in the
[clean dependency-preflight runbook](phase3-dependency-preflight-runbook.md).
After PR `#72` is approved and merged, and after a fresh `main` application
regression passes, dispatch exactly:

```text
Actions
→ Workbook 05 Phase 3 dependency preflight
→ Run workflow

Use workflow from: main
confirm_live_dependency_preflight: checked
```

The workflow must prove all of the following from one exact run and attempt:

1. Both Optimum repositories have the exact reviewed HTTPS origins and full
   commits.
2. Both source trees are clean and have complete tracked-file SHA-256
   catalogues and aggregate hashes.
3. A separate bootstrap environment uses the committed hash lock for
   `pip-tools==7.6.0` and `pip==26.1.2`.
4. Every ordinary distribution is resolved into one complete target lock and
   installed with `--require-hashes` and a retained pip report.
5. The two VCS packages are installed only from their immutable reviewed local
   source trees with `--no-deps --no-build-isolation`.
6. The final environment contains only pip, the ordinary locked packages, and
   the exact two VCS packages.
7. `pip check` succeeds.
8. New-process imports succeed for `optimum`, `optimum.intel`, `transformers`,
   `nncf`, and `openvino`, and every imported file stays under the final
   environment.
9. `optimum-cli --help` exits with code `0`.
10. The repository-controlled no-model compatibility check passes without
    opening a model, contacting a model repository, creating a conversion
    output, or launching conversion.
11. The constructed conversion arguments contain no `--trust-remote-code`.
12. The text-only artifact passes independent hosted validation strictly as
    untrusted data.

Acceptance then requires the exact `main` commit, workflow run and attempt,
artifact name, GitHub digest, independently recalculated artifact SHA-256,
independently recalculated `decision.json` SHA-256, retained `C:\w5c` workspace,
and explicit project-owner acceptance to be recorded.

A successful dependency preflight still does **not** enable `live-asset-lock`.
A separate reviewed binding change must consume and verify the exact accepted
decision digest and retained workspace. Merely entering a digest into the asset
workflow is not sufficient.

## Live C1 dispatch — only after dependency acceptance and binding

Do not use this sequence until the asset-workflow implementation explicitly
consumes and verifies the accepted dependency-preflight decision and retained
workspace. The repository currently keeps this operation blocked.

The eventual live dispatch will be:

```text
Actions
→ Workbook 05 Phase 3 assets
→ Run workflow

Use workflow from: main
operation: live-asset-lock
confirm_live_asset_lock: checked
accepted_dependency_preflight_sha256: <exact accepted lowercase SHA-256>
```

Before the final green button:

- keep the Lenovo connected to mains power;
- keep Windows sleep disabled while plugged in;
- keep the lid open and cooling vents unobstructed;
- confirm the self-hosted runner is online and idle;
- avoid Visual Studio builds, games, cleanup utilities, or large downloads;
- do not change anything under `C:\w5a`, `C:\w5m`, or `C:\w5c` during the run.

## Required live stage order

The orchestrator must retain this exact order:

```text
prerequisite-verification
path-root-verification
disk-preflight
immutable-revision-resolution
source-snapshot-download
source-file-hash-inventory
conversion-new-output-directory
converted-file-hash-inventory
schema-validation
manifest-generation
```

`manifest.sha256` is written last. Records are written to `*.tmp`, validated,
flushed, and atomically moved to their final names. A failure preserves earlier
evidence and writes `failure.json`; it must not create a positive asset lock,
repair a partial model directory, or delete an earlier attempt.

## Required text-only artifact

```text
prerequisite-proof.json
disk-preflight.json
asset-lock.json
conversion-record.json
source-files.csv
converted-files.csv
commands/conversion.json
logs/conversion.stdout.txt
logs/conversion.stderr.txt
summary.md
stage-order.json
manifest.sha256
```

The source and converted CSV files contain paths, sizes, and SHA-256 values only.
They never contain model bytes.

The independent hosted validator must reject:

- missing required evidence;
- hash drift, added files, or duplicate manifest rows;
- unsafe portable paths or parent traversal;
- secret/token patterns;
- model, IR, executable, library, wheel, or archive payloads;
- wrong Granite repository or non-immutable revision;
- remote-code arguments;
- a positive asset paired with a failed conversion;
- any model, activation, storage, performance, or quality authorisation.

## Interruption and recovery

1. Do not rerun immediately and do not delete the failed workspace.
2. Record the workflow run ID, attempt, exact `main` commit, failed job and step.
3. Preserve the text artifact if one was uploaded.
4. Inspect `failure.json`, the last completed stage, stdout, stderr, and resource
   state before proposing a repair.
5. Classify the first proven cause as `IntegrityFailure`, `Blocked`, `Failed`, or
   `InfrastructureInterrupted`; do not call an infrastructure failure an
   algorithm failure.
6. Correct the smallest causal defect with a focused failing test.
7. Use a new workspace identity for the next attempt. Never overwrite or repair
   an existing source or converted directory in place.

## Project-owner acceptance

C1 is accepted only after all three asset-workflow boundaries pass and the
project owner independently verifies:

```text
workflow run ID and attempt
exact main commit
artifact name and GitHub-recorded SHA-256
independently recalculated artifact SHA-256
asset-lock decision SHA-256
conversion decision SHA-256
immutable model repository and full revision
local source and converted directory identities
complete file catalogues and aggregate hashes
all later scientific authorisations remain false
```

Copy only the accepted text evidence and decision records into a stable
acceptance directory. Model and converted payloads remain under their immutable
`C:\w5m` paths and are not uploaded to GitHub.

## C1 non-claims

C1 does not prove or authorise:

```text
model loading
text generation
CPU, model, or KV-cache placement
stateful SDPA/KV-cache execution
scalar U8 or U4 cache activation
TurboQuant U3 or U4 activation
QJL or PolarQuant activation
requested-versus-selected codec equality
fallback absence
packed K or V storage
full KV-cache allocation
TTFT, TPOT, or throughput
peak inference RAM
maximum stable context
perplexity
P1–P6 quality
Granite 8B feasibility
```

Those require the later C2 process harness, C3 activation/storage conformance,
C4 quality-evidence package, and C5 standard-cache smoke boundary.
