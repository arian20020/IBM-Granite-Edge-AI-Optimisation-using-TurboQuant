<!-- IXN-EVIDENCE-STRUCTURE: GENERATED -->
# Architecture Decision Records

## Purpose

Short, dated records of important technical choices, alternatives, trade-offs, consequences, and review triggers.

## What belongs here

- one ADR per significant decision;
- context, options, decision, reasons, consequences, and evidence boundaries;
- status: Proposed, Accepted, Superseded, or Rejected;
- explicit non-claims so an accepted design is not mistaken for implementation proof.

## Current records

| ADR | Status | Decision |
|---|---|---|
| [ADR-001](ADR-001-llamasharp-application-runtime.md) | Accepted | Use `LLamaSharp` 0.27.0 and `LLamaSharp.Backend.Cpu` 0.27.0 as the matched application GGUF runtime pair; retain upstream `b9870` as separate research evidence |
| [ADR-002](ADR-002-core-inspection-versus-backend-verification.md) | Accepted | Keep Model Inspection focused on lightweight core-runtime compatibility; verify ordinary Vulkan and TurboQuant through later, separate backend gates |
| [ADR-003](ADR-003-protected-model-inspection-worker.md) | Accepted | Run LLamaSharp/native Model Inspection in a dedicated x64 worker using bounded port-free standard-stream JSON; keep model classification in application code and chat on a separate pinned `llama-cli` route |

## Evidence rules

- An accepted ADR records a decision; it does not prove the implementation exists.
- Folder or source-file creation is preparation, not completion evidence.
- Use stable requirement, work-package, test/evidence, and research-question IDs where available.
- Preserve raw evidence and derive processed results with version-controlled scripts.
- Record dates, versions, hashes, units, actual device/backend state, and failures where relevant.
- Do not commit secrets, API keys, private personal data, model weights, or unlicensed material.
- Prefer relative repository links.
- When a managed wrapper and native backend are coupled, record the exact compatible versions and mapped native revision together.
- Keep model inspection, hardware fit, backend verification, optimisation, and inference as separate claims unless evidence proves them together.
- Operational failures must not be relabelled as model outcomes.

## Source

Repository evidence structure and accepted project architecture decisions.
