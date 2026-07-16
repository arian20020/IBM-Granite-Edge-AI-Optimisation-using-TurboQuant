# Upstream llama.cpp Evidence Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Archive superseded upstream llama.cpp calibration evidence, document the active evidence route, and preserve complete workbook traceability.

**Architecture:** Use Git-aware directory moves to separate current formal runs from calibration and superseded runs without changing raw bytes. Rewrite only repository-path fields in the central evidence index, then validate every indexed size/hash and every workbook evidence reference.

**Tech Stack:** Git, PowerShell 5.1, Python 3.13, CSV, Markdown, python-docx.

## Global Constraints

- Do not delete or rewrite raw evidence.
- Do not change controlled test IDs, scores, medians or formal run IDs.
- Preserve every archived file's byte size and SHA-256.
- Keep UL-B01 through UL-B06 build and failure evidence in their current test folders.
- Push the completed cleanup to draft PR #29 only after all validators pass.

---

### Task 1: Capture the migration contract

**Files:**
- Create: `experiments/granite_turboquant_intel/logs/upstream-llama-cpp/archive/calibration-and-superseded/move-manifest.csv`

**Interfaces:**
- Consumes: the classification rules in `docs/superpowers/specs/2026-07-16-upstream-llama-evidence-cleanup-design.md`.
- Produces: rows with `Source_Path`, `Archive_Path`, `File_Count`, `Total_Bytes`, and `Tree_SHA256` used to verify the move.

- [ ] Enumerate the exact source run directories and reject a missing source or existing destination.
- [ ] Calculate file count, total bytes and a deterministic tree digest from each relative file path plus SHA-256.
- [ ] Write the CSV and verify it lists 23 superseded runs: 13 `METRICS-R001` runs, six UL-01 metrics calibrations, one UL-01 TTFT calibration, two UL-01 pilot generations, and UL-13 quality R001.

### Task 2: Move superseded evidence

**Files:**
- Move: classified run directories beneath `experiments/granite_turboquant_intel/logs/upstream-llama-cpp/archive/calibration-and-superseded/<Test-ID>/`

**Interfaces:**
- Consumes: `move-manifest.csv`.
- Produces: the archived directory tree with unchanged file content.

- [ ] Move every source directory with `git mv`.
- [ ] Recompute file counts, total bytes and tree digests at each destination.
- [ ] Fail if any source remains, destination differs from the manifest, or an active `SERVER-METRICS-R001`/formal benchmark/quality directory was moved.

### Task 3: Document the route

**Files:**
- Create: `experiments/granite_turboquant_intel/logs/upstream-llama-cpp/README.md`
- Create: `experiments/granite_turboquant_intel/logs/upstream-llama-cpp/archive/calibration-and-superseded/README.md`
- Modify: `experiments/granite_turboquant_intel/README.md`
- Modify: `docs/testing/workbooks/README.md`

**Interfaces:**
- Consumes: WB-01 v1.4, processed results and archived layout.
- Produces: current-route and archive guidance for future operators.

- [ ] Add the route map, current formal run naming, processed-result locations and archive warning.
- [ ] Explain Vulkan CRLF/Jinja2 recovery, corrected loaded-server TTFT, UL-13 project pass, and the retained 49/52 SYCL edge-suite limitation.
- [ ] Document future-run procedure: create a new run ID, capture immutable raw evidence, avoid sensitive environment dumps, update the evidence index/workbook, regenerate DOCX only if canonical workbook text changes, and run validators.
- [ ] Update the campaign README with the archive convention and the workbook README's stale WB-01 version/status.

### Task 4: Repair traceability paths

**Files:**
- Modify: `docs/testing/Evidence-Index.csv`
- Modify only if references exist: workbook, processed result, note and plan files.

**Interfaces:**
- Consumes: `move-manifest.csv` source/destination prefixes.
- Produces: valid current repository paths while retaining `Evidence_ID`, size and SHA-256.

- [ ] Replace old prefixes only in the `Repository_Path` column and assert each source prefix matched at least one row.
- [ ] Search tracked text for stale source paths and update genuine links; do not rewrite historical command strings that merely record where a tool ran.
- [ ] Verify all 722 evidence-index rows resolve to files and match recorded size/SHA-256.
- [ ] Verify every evidence path in canonical WB-01 exists.

### Task 5: Validate and publish

**Files:**
- Modify only when required by canonical workbook changes: generated WB-01 DOCX and `Controlled-Workbook-Manifest.csv`.

**Interfaces:**
- Consumes: cleaned tree and repaired traceability.
- Produces: a verified commit pushed to PR #29.

- [ ] Run `py -3.13 -m unittest discover -s scripts/testing/tests -v` and require 11/11 pass.
- [ ] Run workbook revision-control and controlled-workspace validators.
- [ ] Run WB-01 blank/semantic checks, 39-sample validation, archive digest validation and `git diff --check`.
- [ ] Confirm no secrets, caches, model files or binaries are staged.
- [ ] Commit with `Organize upstream llama.cpp evidence` and push the branch.
- [ ] Verify local HEAD equals the remote branch and the worktree is clean.
