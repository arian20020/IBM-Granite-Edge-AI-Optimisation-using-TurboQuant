# EP-018 CI Evidence Closure Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Preserve the verified GitHub-hosted TRX, update CI documentation, and record the approved fixture/manifest deferral to EP-023 without editing generated traceability outputs.

**Architecture:** Treat the GitHub artifact as the immutable raw source, preserve its sole TRX entry byte-for-byte, and build a small EP-018 evidence pack around it. Update the two existing CI-facing documents, while keeping formal RTM status changes outside this repository edit until the controlled workbook is revised and regenerated.

**Tech Stack:** Markdown, TRX XML, PowerShell, Git, GitHub Actions evidence

## Global Constraints

- Work on `test/ep-018-testing-foundation`.
- Source archive: `C:\Users\Arian\Downloads\unit-test-results-29463973146-1.zip`.
- Expected archive SHA-256: `8e6e55d72e49be2814ef651bda6bebdced5e1ae5ff7aea8fd63e868cff97274b`.
- Expected TRX SHA-256: `11005889320185526ab60a8c53e09b3a6c8b70973bd7dccdc8be5ea7dd735243`.
- Preserve `GraniteEdgeAI.UnitTests.trx` byte-for-byte; do not reformat its XML.
- Do not edit `docs/traceability/data/task-catalogue.json` or anything under `docs/traceability/generated/`.
- Do not claim model-input behavior, fixture validation, integration testing, contract testing, inference, performance, coverage or release readiness.
- Record EP-023 as the deferred-work owner, with F-M03 and IM-04 primary, IM-06 for OpenVINO classification, and EP-024 only for runtime integration.

---

### Task 1: Preserve the GitHub-hosted TRX

**Files:**
- Create: `docs/evidence/engineering-practices/EP-018/GraniteEdgeAI.UnitTests.trx`

**Interfaces:**
- Consumes: GitHub artifact archive `unit-test-results-29463973146-1.zip`.
- Produces: immutable raw evidence file with SHA-256 `11005889320185526ab60a8c53e09b3a6c8b70973bd7dccdc8be5ea7dd735243`.

- [ ] **Step 1: Verify the archive before extraction**

Run:

```powershell
$zip = 'C:\Users\Arian\Downloads\unit-test-results-29463973146-1.zip'
$expectedArchiveHash = '8e6e55d72e49be2814ef651bda6bebdced5e1ae5ff7aea8fd63e868cff97274b'
$actualArchiveHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $zip).Hash.ToLowerInvariant()
if ($actualArchiveHash -ne $expectedArchiveHash) {
    throw "Artifact archive hash mismatch: $actualArchiveHash"
}
```

Expected: exit 0 and no output.

- [ ] **Step 2: Extract exactly one expected TRX entry**

Run from the repository root:

```powershell
$zip = 'C:\Users\Arian\Downloads\unit-test-results-29463973146-1.zip'
$destinationDirectory = 'docs\evidence\engineering-practices\EP-018'
$destination = Join-Path $destinationDirectory 'GraniteEdgeAI.UnitTests.trx'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($zip)
try {
    $entries = @($archive.Entries | Where-Object { $_.FullName -eq 'GraniteEdgeAI.UnitTests.trx' })
    if ($archive.Entries.Count -ne 1 -or $entries.Count -ne 1) {
        throw 'Expected the artifact to contain exactly one GraniteEdgeAI.UnitTests.trx entry.'
    }
    New-Item -ItemType Directory -Force -Path $destinationDirectory | Out-Null
    $inputStream = $entries[0].Open()
    try {
        $outputStream = [System.IO.File]::Create((Join-Path (Get-Location) $destination))
        try { $inputStream.CopyTo($outputStream) } finally { $outputStream.Dispose() }
    } finally { $inputStream.Dispose() }
} finally { $archive.Dispose() }
```

Expected: `docs/evidence/engineering-practices/EP-018/GraniteEdgeAI.UnitTests.trx` exists.

- [ ] **Step 3: Verify the preserved TRX content and identity**

Run:

```powershell
$trx = 'docs\evidence\engineering-practices\EP-018\GraniteEdgeAI.UnitTests.trx'
$hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $trx).Hash.ToLowerInvariant()
if ($hash -ne '11005889320185526ab60a8c53e09b3a6c8b70973bd7dccdc8be5ea7dd735243') {
    throw "TRX hash mismatch: $hash"
}
[xml]$xml = Get-Content -Raw -LiteralPath $trx
$manager = [System.Xml.XmlNamespaceManager]::new($xml.NameTable)
$manager.AddNamespace('t', 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
$counters = $xml.SelectSingleNode('//t:ResultSummary/t:Counters', $manager)
$testMethod = $xml.SelectSingleNode('//t:TestMethod', $manager)
if ($counters.total -ne '1' -or $counters.executed -ne '1' -or $counters.passed -ne '1' -or $counters.failed -ne '0') {
    throw 'Unexpected TRX test totals.'
}
if ($testMethod.adapterTypeName -ne 'executor://MSTest.Sdk/4.0.2') {
    throw 'Unexpected MSTest adapter identity.'
}
```

Expected: exit 0 and no output.

- [ ] **Step 4: Commit the immutable raw evidence**

```powershell
git add -- 'docs/evidence/engineering-practices/EP-018/GraniteEdgeAI.UnitTests.trx'
git diff --cached --check
git commit -m 'test: preserve EP-018 hosted unit-test result'
```

Expected: one new TRX file committed.

### Task 2: Create the EP-018 evidence record and CI summary

**Files:**
- Create: `docs/evidence/engineering-practices/EP-018/README.md`
- Create: `docs/evidence/engineering-practices/EP-018/CI-Validation-Summary.md`

**Interfaces:**
- Consumes: preserved TRX from Task 1 and approved scope decision.
- Produces: permanent human-readable evidence and explicit deferral mapping.

- [ ] **Step 1: Create the EP-018 evidence README**

Create `docs/evidence/engineering-practices/EP-018/README.md` with:

```markdown
# EP-018 — Executable Testing and CI Foundation Evidence

## Evidence status

The narrowed EP-018 implementation boundary is complete and supported by a successful GitHub-hosted Windows run. Formal RTM validation remains pending until the controlled workbook adopts the narrowed scope and its generated repository snapshots are regenerated.

## Narrowed implementation boundary

> Establish executable test-project structure, testing-platform configuration, Windows CI, test-result generation and preserved CI evidence.

## Completed criteria

| Criterion | Result | Evidence |
|---|---|---|
| Executable MSTest project exists | Pass | `tests/UnitTests/GraniteEdgeAI.UnitTests/` |
| Microsoft Testing Platform is configured | Pass | `global.json`; `GraniteEdgeAI.UnitTests.csproj` |
| Test project is registered in the solution | Pass | `IBM Granite with TurboQuant (Intel).slnx` |
| Release application and test-project builds run in Windows CI | Pass | `CI-Validation-Summary.md` |
| Unit test executes successfully on a GitHub-hosted Windows runner | Pass | `GraniteEdgeAI.UnitTests.trx` |
| TRX evidence is generated, uploaded and permanently preserved | Pass | `CI-Validation-Summary.md`; `GraniteEdgeAI.UnitTests.trx` |
| CI process and local execution are documented | Pass | `docs/architecture/diagrams/CI-Build-and-Test-Workflow.md`; `tests/README.md` |

## Approved fixture and manifest deferral

Fixture files, the fixture manifest and fixture-manifest integrity validation are deferred from EP-018 to EP-023. The first implementation will support REQ:F-M03 and WP:IM-04 during development of deterministic model-input validation. OpenVINO-specific fixture scenarios will additionally trace to WP:IM-06 and, where runtime integration is involved, EP-024.

| Relationship | Stable owner |
|---|---|
| Primary engineering practice | `EP:EP-023` — Implement unit tests for pure logic |
| Primary requirement | `REQ:F-M03` — Validate a selected input before using it |
| Primary work package | `WP:IM-04` — Format detector and GGUF header validation |
| OpenVINO classification | `WP:IM-06` — Other-format recognition and classification |
| Runtime integration | `EP:EP-024` |

## Claim boundary

This evidence proves that the executable MSTest and Windows CI foundation operated successfully on the recorded GitHub-hosted Windows run and that its raw TRX evidence is preserved in version control.

It does not prove model-input validation behavior, fixture correctness, integration or contract behavior, inference, performance, code coverage or release readiness.

## Controlled RTM follow-up

Do not manually edit generated traceability files. The authoritative RTM workbook must adopt the narrowed EP-018 wording, transfer the deferred fixture and manifest scope to EP-023, update statuses through the controlled review process, and regenerate the repository snapshots before formal RTM closure is claimed.
```

- [ ] **Step 2: Create the hosted CI summary**

Create `docs/evidence/engineering-practices/EP-018/CI-Validation-Summary.md` with:

```markdown
# EP-018 GitHub-Hosted CI Validation Summary

## Run identity

| Field | Value |
|---|---|
| Workflow | Build and test |
| Workflow run ID | `29463973146` |
| Workflow run number | `6` |
| Job | Build WinUI and run unit tests |
| Job ID | `87513108129` |
| Head commit | `30b311f5576bfdfacf2928891bc7c7adeadb1b90` |
| Runner | GitHub-hosted Windows runner |
| Result | Success |

## Results

| Check | Result |
|---|---|
| Sparse checkout | Passed |
| .NET SDK setup | Passed |
| x64 MSBuild setup | Passed |
| WinUI application restore and Release x64 build | Passed |
| Unit-test project restore and Release build | Passed |
| Microsoft Testing Platform execution | Passed |
| Tests | 1 passed; 0 failed; 0 skipped |
| TRX generation | Passed |
| Artifact upload | Passed |

## Preserved artifact

| Field | Value |
|---|---|
| GitHub artifact | `unit-test-results-29463973146-1` |
| GitHub retention expiry | 15 August 2026 |
| Artifact archive SHA-256 | `8e6e55d72e49be2814ef651bda6bebdced5e1ae5ff7aea8fd63e868cff97274b` |
| Preserved file | `GraniteEdgeAI.UnitTests.trx` |
| Preserved TRX SHA-256 | `11005889320185526ab60a8c53e09b3a6c8b70973bd7dccdc8be5ea7dd735243` |

The committed TRX was extracted byte-for-byte from the verified GitHub artifact. It remains available after GitHub deletes the temporary artifact.

## Validation conclusion

The clean GitHub-hosted Windows run independently confirms that the repository can check out the required build inputs, compile the WinUI application, compile and execute the MSTest project through Microsoft Testing Platform, generate a TRX report and upload that report as a workflow artifact.

This is infrastructure evidence only. The single smoke test does not claim application-feature coverage.
```

- [ ] **Step 3: Verify evidence content and commit**

Run:

```powershell
$files = @(
    'docs/evidence/engineering-practices/EP-018/README.md',
    'docs/evidence/engineering-practices/EP-018/CI-Validation-Summary.md'
)
$text = ($files | ForEach-Object { Get-Content -Raw -LiteralPath $_ }) -join "`n"
foreach ($required in @('EP:EP-023','REQ:F-M03','WP:IM-04','WP:IM-06','EP:EP-024','29463973146','87513108129','30b311f5576bfdfacf2928891bc7c7adeadb1b90','11005889320185526ab60a8c53e09b3a6c8b70973bd7dccdc8be5ea7dd735243')) {
    if (-not $text.Contains($required)) { throw "Missing evidence content: $required" }
}
git diff --check -- $files
git add -- $files
git diff --cached --check
git commit -m 'docs: record EP-018 hosted CI evidence'
```

Expected: two Markdown evidence files committed.

### Task 3: Update CI-facing documentation and perform closure audit

**Files:**
- Modify: `docs/architecture/diagrams/CI-Build-and-Test-Workflow.md`
- Modify: `tests/README.md`
- Verify unchanged: `docs/traceability/data/task-catalogue.json`
- Verify unchanged: `docs/traceability/generated/`

**Interfaces:**
- Consumes: hosted-run evidence from Tasks 1 and 2.
- Produces: truthful current-status documentation and a final audited changeset.

- [ ] **Step 1: Update the architecture document status**

Replace:

```markdown
**Status:** Implemented locally; GitHub-hosted validation pending
```

with:

```markdown
**Status:** Implemented and GitHub-hosted validation confirmed
```

- [ ] **Step 2: Update the tests README CI section**

Replace the two opening CI paragraphs with:

```markdown
The `.github/workflows/build-and-test.yml` workflow restores and builds the WinUI application, restores and builds the MSTest project, runs unit tests through Microsoft Testing Platform, generates a TRX report, and retains that report as a GitHub Actions artifact for 30 days.

The workflow has been validated successfully on a clean GitHub-hosted Windows runner. The application build, test-project build, unit-test execution, TRX generation and artifact upload completed successfully. Permanent evidence is preserved under `docs/evidence/engineering-practices/EP-018/`, and the engineering process is documented in `docs/architecture/diagrams/CI-Build-and-Test-Workflow.md`.
```

Keep the existing paragraph stating that contract and integration projects are not yet executed.

- [ ] **Step 3: Run the full closure audit**

Run:

```powershell
$taskFiles = @(
    'docs/architecture/diagrams/CI-Build-and-Test-Workflow.md',
    'tests/README.md',
    'docs/evidence/engineering-practices/EP-018/README.md',
    'docs/evidence/engineering-practices/EP-018/CI-Validation-Summary.md',
    'docs/evidence/engineering-practices/EP-018/GraniteEdgeAI.UnitTests.trx'
)
git diff --check -- $taskFiles
$pending = Select-String -LiteralPath $taskFiles[0],$taskFiles[1] -Pattern 'GitHub-hosted validation pending|workflow has not yet run on GitHub'
if ($pending) { throw 'Outdated CI-pending language remains.' }
git diff --exit-code HEAD -- 'docs/traceability/data/task-catalogue.json' 'docs/traceability/generated'
[xml]$trx = Get-Content -Raw -LiteralPath $taskFiles[4]
$hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $taskFiles[4]).Hash.ToLowerInvariant()
if ($hash -ne '11005889320185526ab60a8c53e09b3a6c8b70973bd7dccdc8be5ea7dd735243') { throw 'Preserved TRX changed.' }
```

Expected: no output and exit 0.

- [ ] **Step 4: Commit the status documentation**

```powershell
git add -- 'docs/architecture/diagrams/CI-Build-and-Test-Workflow.md' 'tests/README.md'
git diff --cached --check
git commit -m 'docs: confirm EP-018 hosted CI validation'
```

Expected: exactly two documentation files committed.

- [ ] **Step 5: Verify final branch state**

Run:

```powershell
git status --short
git log -4 --oneline --decorate
git diff HEAD~3..HEAD --check
git diff HEAD~3..HEAD --stat
```

Expected: clean status; three implementation commits after the design/plan commits; no whitespace errors; only the evidence and CI documentation paths changed.
