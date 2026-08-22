# Workbook 05 Post-C Workbook Pack Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Provide complete, source-controlled and deterministically generated D1, D2, E1, E2, E3, E4 and F workbooks before live hardware execution.

**Architecture:** Seven canonical Markdown workbooks feed the existing deterministic DOCX generator. A shared execution index, configuration matrix, evidence register and pack manifest provide machine-readable control. A focused validator and two-job hosted workflow generate the pack and validate the exact artifact independently without model or self-hosted execution.

**Tech Stack:** Python 3.12.10, python-docx 1.2.0, CSV/JSON, PowerShell, GitHub Actions.

**Spec:** `docs/superpowers/plans/2026-08-05-workbook-05-completion-checkpoints.md` plus the project-owner-approved E3 Granite 30B bounded-feasibility extension.

## Global Constraints

- No model acquisition, conversion or inference from the workbook workflow.
- No self-hosted runner label in the workbook workflow.
- Canonical Markdown remains the reviewable source of truth.
- Generated DOCX files must be byte-deterministic and visually inspected.
- Existing WB-01 through WB-06 content and IDs remain unchanged.
- A blocked row stays visible.
- E3 cannot authorise formal performance or quality statistics.
- F cannot close while any required row is unresolved or unvalidated.

---

### Task 1: Freeze workbook-pack acceptance tests
- [x] Add focused tests for seven workbooks, stage/index coverage, safe paths, hashes, E3 non-claims and open closure.
- [x] Verify RED because implementation files are absent.

### Task 2: Author canonical workbooks and shared controls
- [x] Add WB-07 through WB-13 Markdown templates.
- [x] Add post-C execution index, configuration matrix, evidence register, revision register and JSON pack manifest.

### Task 3: Extend deterministic generation and validation
- [x] Extend `Generate-Controlled-Workbooks.py` for thirteen templates and a post-C-only mode.
- [x] Add `post_c_workbook_pack.py` and a PowerShell gate.
- [x] Generate twice and compare every new DOCX byte-for-byte.

### Task 4: Generate and visually verify DOCX working copies
- [x] Generate WB-07 through WB-13.
- [x] Render every page to PNG and inspect for clipping, overlap, broken tables and missing glyphs.

### Task 5: Run the complete repository-safe workbook workflow
- [ ] Push the exact pack head.
- [ ] Require hosted producer and independent-validator jobs to pass on the same head.
- [ ] Inspect the retained artifact and validation report.
