# Candidate Risk Backlog

**Status:** Draft candidate inventory — not yet consolidated, assessed or baselined  
**Owner:** Arian B  
**Parent register:** [`../Risk-Register.md`](../Risk-Register.md)  
**Assessment method:** [`../Control-and-Validation-Plan.md`](../Control-and-Validation-Plan.md)

This directory contains the project-wide candidate risks identified before the formal risk review. The files together cover `R-003` to `R-254`. Existing full records `R-001` and `R-002` remain in the parent Risk Register.

| File | IDs | Main areas |
|---|---|---|
| [01-project-scope-windows.md](01-project-scope-windows.md) | `R-003`–`R-066` | Schedule, scope, requirements, Windows, WinUI 3, model import and llama.cpp |
| [02-openvino-turboquant.md](02-openvino-turboquant.md) | `R-067`–`R-098` | OpenVINO and TurboQuant |
| [03-turbovec-hardware-performance.md](03-turbovec-hardware-performance.md) | `R-099`–`R-150` | TurboVec, Intel hardware, memory and performance |
| [04-security-ux-ai-quality.md](04-security-ux-ai-quality.md) | `R-151`–`R-199` | Security, privacy, UX, accessibility and AI output quality |
| [05-testing-evidence.md](05-testing-evidence.md) | `R-200`–`R-224` | Testing, evidence and reproducibility |
| [06-licence-release.md](06-licence-release.md) | `R-225`–`R-254` | Licensing, third-party supply chain, release and reporting |

## Important boundary

A candidate row records a possible uncertain event. It is not yet a fully controlled risk. Before promotion into the full register, the review must:

1. merge duplicates and closely related rows;
2. confirm that the row is a risk rather than an issue, assumption or constraint;
3. define cause, trigger, validation, mitigation and contingency;
4. assess probability, impact, exposure and residual risk using the controlled method;
5. add evidence, traceability, owner and review dates;
6. record the promotion, merge, rejection or supersession decision in the Review Log.

No candidate in this directory is automatically Critical, High, validated, accepted or baselined merely because it has been listed. The candidate tables preserve discovery coverage; the operational Risk Register will contain the smaller set of accepted and fully controlled risks.
