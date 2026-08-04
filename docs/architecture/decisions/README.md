<!-- IXN-EVIDENCE-STRUCTURE: GENERATED -->
# Architecture Decision Records

## Purpose

Short, dated records of important technical choices, alternatives, trade-offs and review triggers.

## What belongs here

- One ADR per significant decision.
- Context, options, decision, reasons, consequences and evidence.
- Status: Proposed, Accepted, Superseded or Rejected.

## Current records

| ADR | Status | Decision |
|---|---|---|
| [ADR-001](ADR-001-llamasharp-application-runtime.md) | Accepted | Use `LLamaSharp` 0.27.0 and `LLamaSharp.Backend.Cpu` 0.27.0 as the matched application GGUF runtime pair; retain upstream `b9870` as separate research evidence |

## Related IDs

No stable ADR-specific IDs have been assigned yet. Each ADR records its affected feature, requirements and review triggers inside the document.

## Evidence rules

- Folder creation is only preparation; it is not proof of completion.
- Use stable requirement, work-package, test/evidence and research-question IDs.
- Preserve raw evidence; derive processed results with version-controlled scripts.
- Record dates, versions, hashes, units, actual device/backend state and failures where relevant.
- Do not commit secrets, API keys, private personal data, large model weights or unlicensed material.
- Prefer relative repository links so evidence remains usable after cloning.
- Distinguish an accepted technical decision from verified implementation evidence.
- When a managed wrapper and native backend are coupled, record the exact compatible versions and native revision together.

## Source

Repository evidence structure

