# Workbook 05 controlled testing documents

This directory contains retained phase decisions and operator runbooks for the
Granite–TurboQuant memory-frontier campaign.

## Accepted and blocked Phase 2 boundaries

- [Route A Phase 2 closure](2026-08-13-route-a-phase-2-closure.md)
- [Route B Phase 2 blocked closure](2026-08-06-route-b-phase-2-closure.md)

## Phase 3 C1

- [C1 immutable asset-lock runbook](phase3-asset-lock-runbook.md)

The C1 runbook distinguishes repository/offline verification from a live model
asset operation. No live Granite download or conversion is permitted until the
clean Windows dependency-preflight decision has been independently validated
and accepted.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

Contains controlled helpers for Workbook 05 source admission, build and measurement stages.

### Start here

Begin with [`2026-08-06-route-b-phase-2-closure.md`](2026-08-06-route-b-phase-2-closure.md). The tables below explain the remaining items.

### How this folder fits into testing

This folder supports the controlled path from a test requirement to evidence, validation and a bounded conclusion.

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`2026-08-06-route-b-phase-2-closure.md`](2026-08-06-route-b-phase-2-closure.md) | Records why Route B Phase 2 was closed as blocked and which evidence supports that boundary. | Phase decision record |
| [`2026-08-13-route-a-phase-2-closure.md`](2026-08-13-route-a-phase-2-closure.md) | Records the accepted Route A Phase 2 closure and the evidence admitted into the next stage. | Phase decision record |
| [`phase3-asset-lock-runbook.md`](phase3-asset-lock-runbook.md) | Procedure for verifying and locking the exact Phase 3 C1 model assets before any live operation. | Operator runbook |
| [`phase3-c1-implementation-status.md`](phase3-c1-implementation-status.md) | Summarises which Phase 3 C1 controls are implemented and which operations remain gated. | Status record |
| [`phase3-dependency-preflight-runbook.md`](phase3-dependency-preflight-runbook.md) | Procedure for validating the clean Windows dependencies required before Phase 3 may proceed. | Operator runbook |

### Important boundaries

- Check the file's status and evidence links before treating it as a current result.

### Related guides

- [Parent guide](../README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
