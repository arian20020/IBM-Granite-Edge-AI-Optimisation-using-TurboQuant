# Upstream llama.cpp Formal Execution Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete the controlled upstream llama.cpp build and UL-01 through UL-13 test campaign with traceable raw evidence, harsh rubric-based quality scores, validated registers, and a fully populated WB-01 workbook.

**Architecture:** Treat each build or formal configuration as an immutable run with command, stdout, stderr, summary, manifest, and hash evidence. Update the operational CSV registers and canonical WB-01 Markdown immediately after validating each run, then regenerate the controlled DOCX and run structural, semantic, hash, and visual checks.

**Tech Stack:** PowerShell 5.1, CMake/CTest, MSVC Visual Studio 18 2026, llama.cpp b9870, Vulkan SDK 1.4.350.0, optional Intel oneAPI/SYCL, JSON/CSV evidence, controlled Markdown-to-DOCX generator.

## Global Constraints

- Use pinned upstream llama.cpp tag `b9870`, commit `2d973636e292ee6f75fadcf08d29cb33511f509f`.
- Preserve failed and blocked attempts; never replace them with successful retests.
- Use the campaign 0-10 quality rubric: correctness 30%, instruction/format 25%, completeness 20%, clarity 15%, stability 10%.
- Run one excluded warm-up and at least three measured repetitions unless a documented safety gate prevents it.
- Use only exact missing-data codes from `docs/testing/Workbook-Data-Requirements.md`; leave no workbook field blank.
- Update registers and WB-01 after each validated test, not at campaign end.
- Do not claim TurboQuant activation in the upstream baseline.

---

### Task 1: Establish the controlled baseline

**Files:**
- Verify: `docs/testing/Master-Test-Plan.md`
- Verify: `docs/testing/workbooks/text-templates/01_Upstream_llama.cpp_Controlled_Retest_Workbook_v1.md`
- Verify: `docs/testing/*.csv`

- [ ] Run the controlled workspace and workbook revision validators.
- [ ] Rebuild the application solution and execute every discoverable repository test project.
- [ ] Re-run the pinned llama.cpp CPU build and all 52 CTest tests, capturing fresh evidence.

### Task 2: Complete backend builds and capability gates

**Files:**
- Update: `docs/testing/Build-Register.csv`
- Update: `docs/testing/Test-Run-Register.csv`
- Update: `docs/testing/Failure-Register.csv`
- Create/update: `experiments/granite_turboquant_intel/{logs,notes,manifests}/upstream-llama-cpp/UL-B05-*`

- [ ] Diagnose the UL-B05 cache-validation discrepancy from CMake evidence.
- [ ] Build and test the corrected Vulkan configuration.
- [ ] Probe oneAPI/SYCL availability, build and test when practical, otherwise record a bounded missing-data classification.
- [ ] Record warnings, binary capabilities, hashes, and backend support as UL-B07.

### Task 3: Validate models, prompts, and measurement harness

**Files:**
- Verify: `experiments/granite_turboquant_intel/models/**`
- Verify: `experiments/granite_turboquant_intel/prompts/fixed-feasibility-prompt-set-v1.json`
- Verify: `experiments/granite_turboquant_intel/rubrics/quality-rubric-v1.json`
- Update: `docs/testing/Model-Register.csv`
- Update: `docs/testing/Configuration-Register.csv`

- [ ] Verify each GGUF size, SHA-256, architecture, chat template, and llama.cpp load compatibility.
- [ ] Validate P1-P6 fixtures and deterministic checks.
- [ ] Pilot the measurement harness and reconcile its metrics against llama.cpp timing output and process telemetry.

### Task 4: Execute UL-01 through UL-13

**Files:**
- Create: `experiments/granite_turboquant_intel/{logs,notes,manifests}/upstream-llama-cpp/UL-*/`
- Update: `docs/testing/Test-Run-Register.csv`
- Update: `docs/testing/Performance-Measurement-Register.csv`
- Update: `docs/testing/Device-Verification-Register.csv`
- Update: `docs/testing/Quality-Evaluation-Register.csv`
- Update: `docs/testing/Failure-Register.csv`

- [ ] For each configuration, run the pilot and safety gate.
- [ ] Run one excluded warm-up and at least three measured repetitions.
- [ ] Capture exact command, stdout, stderr, response, timing, memory, backend placement, and exit state.
- [ ] Score P1-P6 against deterministic checks and the rubric, with criterion-level reasons and no benefit of the doubt.
- [ ] Validate raw-to-processed reconciliation and update all registers immediately.

### Task 5: Complete and verify WB-01

**Files:**
- Update: `docs/testing/workbooks/text-templates/01_Upstream_llama.cpp_Controlled_Retest_Workbook_v1.md`
- Update: `docs/testing/Workbook-Completion-Register.csv`
- Update: `docs/testing/workbooks/generated/01_Upstream_llama.cpp_Controlled_Retest_Workbook_v1.docx`
- Update: `docs/testing/workbooks/Controlled-Workbook-Manifest.csv`

- [ ] Replace every blank WB-01 field with validated evidence or an exact missing-data classification plus reason.
- [ ] Cross-check every workbook result against raw evidence and operational registers.
- [ ] Regenerate all controlled outputs required by the document-control workflow.
- [ ] Run workspace, revision-control, hash, formula/CSV consistency, and visual page-by-page verification.
- [ ] Run final full build and test verification and review the campaign exit gates line by line.
