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

The ordinary Phase 3 asset workflow exposes exactly two operations:

```text
offline-fixture
live-asset-lock
```

Its manual fields remain:

```text
operation
confirm_live_asset_lock
accepted_dependency_preflight_sha256
```

`offline-fixture` is the deterministic, network-free rehearsal. `live-asset-lock`
is admitted only because one exact clean Windows dependency preflight has been
independently validated and accepted. The workflow and live orchestrator both
revalidate that binding before model-root creation, a Hugging Face request,
download, or conversion.

The exact workflow step is:

```text
Verify accepted dependency binding before model access
```

A different digest, run, attempt, artifact, retained workspace, decision file,
Python executable, source identity, package record, manifest, or scientific flag
fails closed. A repository test or offline fixture must never be reported as model
download, conversion, loading, generation, codec activation, performance, or
quality evidence.

## Accepted dependency-preflight binding

The only dependency candidate admitted to C1 is:

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

The live route validates the committed record, retained bundle,
`manifest.sha256`, `decision.json`, live observation, accepted Python binary,
and Optimum CLI location. Every model and scientific-authorisation field remains
`false` at this boundary.

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
C:\w5a\accepted-route-a-genai-31656417607-1\decision.json
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

Every live or resumed C1 attempt revalidates these exact decisions, source
commits, claim boundaries, and installed outputs before conversion tooling runs.

## Controlled storage boundaries

```text
C:\w5a\  accepted Runtime and GenAI prerequisites; read-only
C:\w5m\  immutable source and converted model assets
C:\w5c\  accepted dependency and C1 evidence workspaces
C:\w5r\  later measured-run workspaces; unused by C1
```

All roots and children must be normal local directories. UNC paths, device
paths, junctions, symbolic links, mount points, and reparse points are rejected.
Existing attempts, sources, and converted directories are never silently reused,
overwritten, repaired, or deleted.

A fresh live C1 attempt requires at least `53,687,091,200` free bytes (50 GiB)
before source acquisition. Passing disk preflight alone does not authorise a
download.

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

`--trust-remote-code` is forbidden. A tool that requires repository Python code
blocks the candidate instead of weakening the security boundary.

## Pull-request verification

PR verification uses the exact current head. The following must pass where their
path filters apply:

```text
Build and test
Workbook 05 documented build
Workbook 05 Route A Runtime controlled resume contract
Workbook 05 Phase 3 assets — repository contract
Workbook 05 Phase 3 dependency preflight — repository contract
Workbook 05 Phase 3 C1 controlled source resume — repository contract
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

Also require `git diff --check`, a clean status, and confirmation that no model,
tokenizer, IR, executable, library, wheel, or archive was committed as evidence.

## Safe offline rehearsal

```text
Actions → Workbook 05 Phase 3 assets → Run workflow
Use workflow from: main
operation: offline-fixture
confirm_live_asset_lock: unchecked
accepted_dependency_preflight_sha256: blank
```

The fixture creates disposable source-shaped and converted-shaped files beneath
runner temporary storage. It performs no network model download or model
execution and uploads only permitted text evidence.

## Fresh live C1 dispatch

```text
Actions → Workbook 05 Phase 3 assets → Run workflow
Use workflow from: main
operation: live-asset-lock
confirm_live_asset_lock: checked
accepted_dependency_preflight_sha256:
429b90548ce2b4c8463941c5b2983c8ff0cf6cc3ea3865375c8cae78d7193b49
```

Before dispatch, keep the Lenovo connected to mains, awake, cool, online, and
idle. Avoid builds, games, cleanup utilities, large downloads, and any change
beneath `C:\w5a`, `C:\w5m`, or `C:\w5c`.

## Required live stage order

The fresh orchestrator retains this exact order:

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

The first stage revalidates the accepted dependency plus Route A Runtime and
GenAI. `manifest.sha256` is written last. A failure preserves earlier evidence,
writes `failure.json`, and never creates a positive asset lock.

## Required fresh text-only artifact

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

Model source files and converted OpenVINO IR remain under `C:\w5m` and are never
uploaded. The hosted validator rejects missing evidence, hash drift, unsafe paths,
credentials, forbidden payloads, wrong model identity, remote-code arguments,
failed conversion relationships, and any later scientific authorisation.

## Interruption and recovery

1. Do not immediately rerun and do not delete the failed workspace.
2. Record the run, attempt, exact head, failed job, and first failing step.
3. Preserve and independently hash the text artifact.
4. Inspect `failure.json`, the final completed stage, logs, and resource trace.
5. Classify the cause truthfully; do not call infrastructure pressure an algorithm
   failure.
6. Add a focused failing regression before implementing the smallest causal fix.
7. Use new workspace and output identities. Never repair a failed directory in
   place.

## Controlled source-resume boundary for run 32410714130

The dedicated workflow exists only for the independently verified failed attempt:

```text
Prior workflow run: 32410714130
Prior run attempt: 1
Prior head: 80946fc2e06767a8aeff878deab7d31f10fd6676
Prior artifact: workbook-05-phase3-assets-32410714130-1
Prior artifact SHA-256:
33d3ca32236d6da973c21c5d95ba36d088c5ed7384d93eb3f353dbd081671257
Resolved Granite revision:
c0650403e44e78ec0262dab1c90914c65b196c4e
Retained source:
C:\w5m\sources\granite41-3b-c0650403
Aggregate model SHA-256:
58e7e6635ac57fbf201e4258cc17c01f9ac04a997bbd9cc5315ec52caba79956
Aggregate tokenizer SHA-256:
21b6eb2dd3b049017077d62aaab88d38bbc639c6765e4bcb33c2bfc44e463b4e
```

The prior run completed source acquisition and hashing, then the conversion
watchdog stopped it after available physical memory stayed below 1.5 GiB for ten
seconds. Its maximum Windows commit use remained below the independent 90 percent
hard stop. A later fresh attempt encountered the preserved-data disk boundary.

The resume workflow therefore does **not** lower the fresh 50 GiB gate and does
**not** download the source again. It validates the prior GitHub artifact through
`Assert-Workbook05ArtifactIdentity.ps1`, treats its extracted members as untrusted
data, rehashes every retained source path, size, and SHA-256, and checks both
aggregate tree identities before conversion.

It then requires:

```text
at least 20 GiB free disk for conversion-only work
at least 4 GiB free physical memory before conversion starts
Windows commit use at or below 70 percent before conversion starts
no active cmake, MSBuild, cl, link, ninja, devenv, or optimum-cli process
```

The resumed conversion uses one new directory whose name contains the new workflow
run and attempt. The earlier partial conversion and failed evidence workspace are
preserved unchanged. `HF_HUB_OFFLINE=1` and `TRANSFORMERS_OFFLINE=1` prevent a
hidden network/model fetch.

During conversion, the resume-only watchdog option requires sustained **combined
memory pressure** before the 512 MiB low-memory threshold stops the process: free
physical memory must be below 512 MiB while Windows commit is above 80 percent for
five consecutive two-second samples. Windows commit above 90 percent remains an
independent hard stop. The existing fresh Runtime, GenAI, dependency, and C1 callers
retain their original independent 1.5 GiB safeguard.

### Required resume stage order

```text
prerequisite-verification
prior-artifact-validation
retained-source-requalification
conversion-disk-preflight
resource-preflight
conversion-new-output-directory
converted-file-hash-inventory
schema-validation
manifest-generation
```

### Required resume evidence

The ordinary successful C1 files remain required. The resume artifact additionally
contains:

```text
prior-artifact-identity.json
prior-attempt-validation.json
retained-source-proof.json
resource-preflight.json
prior-attempt/**
```

The prior text artifact is embedded so a clean hosted runner can independently
repeat its failure/source validation. `manifest.sha256` covers the complete new
bundle and is written last.

### Explicit resume non-authorisations

Every resume proof, final record, success result, and failure record keeps these
fields false:

```text
model_execution_authorised = false
activation_claim_authorised = false
packed_storage_claim_authorised = false
performance_claim_authorised = false
quality_claim_authorised = false
```

The resume operation may qualify immutable source and converted assets only. It
cannot become evidence of model loading, generation, TurboQuant or QJL/PolarQuant
activation, packed KV storage, performance, context stability, perplexity, or
quality.

## Project-owner acceptance

C1 is accepted only after the repository contract, Lenovo collection, and hosted
untrusted-data validation all pass. The owner must independently verify the exact
run/head, artifact name and digest, internal manifest, asset and conversion records,
immutable model revision, retained/new local directories, complete inventories,
aggregate hashes, and false scientific flags.

Copy only accepted text evidence and decision records to a stable acceptance
directory. Model and converted payloads remain under their immutable `C:\w5m`
paths and are not uploaded to GitHub.

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
