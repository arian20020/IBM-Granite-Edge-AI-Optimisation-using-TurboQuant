# Workbook 05 Phase 3 C1 Asset-Lock Runbook

## Purpose

This runbook controls **C1 — immutable model and conversion assets** for campaign
`GTQ-WB05-MF-v1`, Route A `route-a-merged-openvino`.

C1 creates a reproducible identity for the IBM Granite 4.1 3B source files,
tokenizer files, conversion environment, conversion command, and converted
OpenVINO files. It does **not** execute the model and does not prove that
TurboQuant, scalar cache quantisation, or any other KV-cache codec ran.

Follow the [clean dependency-preflight runbook](phase3-dependency-preflight-runbook.md)
for dependency-machine preparation, evidence review, independent hashing, and
acceptance. Do not replace that controlled procedure with an improvised local
package-install sequence.

## Current implementation boundary

The Phase 3 asset workflow exposes exactly two operations:

```text
offline-fixture
live-asset-lock
```

`offline-fixture` remains the default, deterministic, network-free rehearsal.
`live-asset-lock` is enabled only because one exact clean Windows dependency
preflight has now been independently validated and accepted into the repository.
The workflow and the live orchestrator both revalidate that binding before any
model-root creation, Hugging Face request, download, or conversion command.

The exact workflow step is:

```text
Verify accepted dependency binding before model access
```

A different digest, run, attempt, artifact, retained workspace, decision file,
Python executable, source identity, package record, manifest, or scientific flag
fails closed. Passing repository tests, dependency installation, or the offline
fixture must never be reported as model download, conversion, loading,
generation, codec activation, performance, or quality evidence.

## Accepted dependency-preflight binding

The only dependency candidate admitted to live C1 is:

```text
Workflow run: 32211117536
Run attempt: 1
Main commit: c417efd936a7fa2e871b689065b2f3b88636c1a1
Artifact ID: 9350956534
Artifact name: workbook-05-phase3-dependency-preflight-32211117536-1
GitHub artifact SHA-256:
b68a4f8af8c57a9f5d71347d2485855796f4d0dce0291c50b596512c309c0c21
Independent artifact SHA-256:
b68a4f8af8c57a9f5d71347d2485855796f4d0dce0291c50b596512c309c0c21
decision.json SHA-256:
429b90548ce2b4c8463941c5b2983c8ff0cf6cc3ea3865375c8cae78d7193b49
Retained workspace:
C:\w5c\dependency-preflight-32211117536-1
```

The committed acceptance record is:

```text
experiments/granite_turboquant_intel/manifests/campaigns/
GTQ-WB05-MF-v1/phase3/accepted-dependency-preflight.json
```

It records explicit owner acceptance while keeping model-download and every
scientific-authorisation field `false`. The live C1 workflow does not trust the
manual digest alone: it validates the committed record, the retained bundle,
`manifest.sha256`, `decision.json`, the live observation, the accepted Python
binary, and the Optimum CLI location before model access.

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

Every live C1 attempt revalidates these exact decisions, source identities,
claim boundaries, and installed outputs before it contacts the model repository
or invokes conversion tooling.

## Controlled storage boundaries

```text
C:\w5a\  accepted Runtime and GenAI prerequisites; read-only
C:\w5m\  immutable source and converted model assets
C:\w5c\  accepted dependency and C1 evidence workspaces
C:\w5r\  later measured-run workspaces; unused by C1
```

All roots and child workspaces must be normal local directories. UNC paths,
device paths, junctions, symbolic links, mount points, and Windows reparse
points are rejected. Existing attempt, source, and converted directories are
never silently reused or deleted.

At least `53,687,091,200` free bytes (50 GiB) are required before a Granite
4.1 3B source download or conversion. Passing the disk check alone does not
authorise a download.

## Reviewed conversion candidate

The controlling dependency candidate is:

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
Requested revision: main, resolved once to a full immutable commit
Task: text-generation-with-past
Weight format: INT4
Symmetry: asymmetric
Group size: 128
Ratio: 1.0
Remote model code: disabled
```

`--trust-remote-code` is forbidden. A tool requiring remote repository Python
code blocks the candidate instead of weakening the security boundary.

## Pull-request verification

PR verification must use its exact current head, not an earlier green commit.
The following must pass where the changed paths trigger them:

```text
Build and test
Workbook 05 documented build
Workbook 05 Route A Runtime controlled resume contract
Workbook 05 Phase 3 assets — repository contract
Workbook 05 Phase 3 dependency preflight — repository contract
```

The repository-controlled gate is:

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

## Safe offline rehearsal

Pull requests run only GitHub-hosted repository-contract jobs and cannot reach
the self-hosted Intel runner. A post-merge model-free rehearsal uses:

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

The fixture creates disposable source-shaped and converted-shaped files beneath
runner temporary storage. It performs no network model download and no model
execution, then uploads only text, JSON, CSV, and log evidence.

## Live C1 dispatch

Use this route only after the live-binding implementation has been merged and a
fresh post-merge `main` regression has passed:

```text
Actions
→ Workbook 05 Phase 3 assets
→ Run workflow

Use workflow from: main
operation: live-asset-lock
confirm_live_asset_lock: checked
accepted_dependency_preflight_sha256:
429b90548ce2b4c8463941c5b2983c8ff0cf6cc3ea3865375c8cae78d7193b49
```

Before pressing the final green button:

- keep the Lenovo connected to mains power;
- keep Windows sleep disabled while plugged in;
- keep the lid open and cooling vents unobstructed;
- confirm the self-hosted runner is online and idle;
- avoid Visual Studio builds, games, cleanup utilities, or large downloads;
- do not change anything under `C:\w5a`, `C:\w5m`, or `C:\w5c` during the run.

The workflow first validates the manual boundary, installs only its isolated
repository validator, runs the full Phase 3 gate, and executes **Verify accepted
dependency binding before model access**. The dedicated live orchestrator then
repeats the dependency proof before creating or touching `C:\w5m`.

## Required live stage order

The orchestrator retains this exact order:

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

The first `prerequisite-verification` stage contains both the accepted dependency
revalidation and the accepted Route A Runtime/GenAI revalidation. Model-root access
occurs only in the following `path-root-verification` stage.

`manifest.sha256` is written last. Records are written to `*.tmp`, flushed, and
atomically moved to their final names. A failure preserves earlier evidence and
writes `failure.json`; it must not create a positive asset lock, repair a partial
model directory, or delete an earlier attempt.

## Required text-only artifact

```text
dependency-acceptance-proof.json
dependency/decision.json
prerequisite-proof.json
disk-preflight.json
resolved-model.json
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
Model source files and converted OpenVINO IR remain under `C:\w5m` and are never
uploaded.

The independent hosted validator must reject:

- missing required evidence;
- hash drift, added files, or duplicate manifest rows;
- unsafe portable paths or parent traversal;
- secret or token patterns;
- model, IR, executable, library, wheel, or archive payloads;
- wrong Granite repository or non-immutable revision;
- remote-code arguments;
- a positive asset paired with a failed conversion;
- any model, activation, storage, performance, or quality authorisation.

## Interruption and recovery

1. Do not rerun immediately and do not delete the failed workspace.
2. Record the workflow run ID, attempt, exact `main` commit, failed job, and step.
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
manifest.sha256 coverage and member hashes
asset-lock.json SHA-256 and status
conversion-record.json SHA-256 and status
immutable model repository and full revision
local source and converted directory identities
complete source, tokenizer, and converted-file catalogues
aggregate model, tokenizer, and conversion-output hashes
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
