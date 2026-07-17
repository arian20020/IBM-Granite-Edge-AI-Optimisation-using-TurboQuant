# Candidate Risk Backlog

**Status:** Historical identification inventory — consolidation completed  
**Owner:** Arian B  
**Parent register:** [`../Risk-Register.md`](../Risk-Register.md)  
**Consolidation decision:** [`../Risk-Consolidation-Map.md`](../Risk-Consolidation-Map.md)  
**Assessment method:** [`../Control-and-Validation-Plan.md`](../Control-and-Validation-Plan.md)

This directory preserves the broad risk-storming inventory created before formal consolidation. The files cover `R-003` to `R-254`; `R-001` and `R-002` originated as earlier full records.

The inventory has now been consolidated into **37 operational risks** in the parent Risk Register. Candidate entries that were not retained separately are treated as causes, examples or narrower forms of the operational risk identified in the Risk Consolidation Map.

| File | IDs | Main areas |
|---|---|---|
| [01-project-scope-windows.md](01-project-scope-windows.md) | `R-003`–`R-066` | Schedule, scope, requirements, Windows, WinUI 3, model import and llama.cpp |
| [02-openvino-turboquant.md](02-openvino-turboquant.md) | `R-067`–`R-098` | OpenVINO and TurboQuant |
| [03-turbovec-hardware-performance.md](03-turbovec-hardware-performance.md) | `R-099`–`R-150` | TurboVec, Intel hardware, memory and performance |
| [04-security-ux-ai-quality.md](04-security-ux-ai-quality.md) | `R-151`–`R-199` | Security, privacy, UX, accessibility and AI output quality |
| [05-testing-evidence.md](05-testing-evidence.md) | `R-200`–`R-224` | Testing, evidence and reproducibility |
| [06-licence-release.md](06-licence-release.md) | `R-225`–`R-254` | Licensing, third-party supply chain, release and reporting |

## Control boundary

These files are historical discovery evidence, not the live operational register.

- Do not assess or update a candidate row independently after consolidation.
- Update the retained operational risk when new evidence concerns the same uncertainty.
- Add a new stable risk ID only when the cause, treatment or contingency is materially different.
- Do not delete or reuse candidate IDs; the consolidation map preserves their decision history.
- Existing failures belong in the Failure Register or issue tracker, even where a linked recurrence risk remains operational.

The operational Risk Register controls current probability, impact, exposure, trigger, treatment, owner, status, evidence and review dates.
