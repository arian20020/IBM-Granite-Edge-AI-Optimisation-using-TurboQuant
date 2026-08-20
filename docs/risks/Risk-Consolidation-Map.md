# Risk Consolidation Map

**Document ID:** MAP-RISK-CONS-001  
**Version:** 1.1
**Status:** Consolidation decision recorded — treatment evidence review pending  
**Owner:** Arian B  
**Review date:** 2026-08-21
**Source inventory:** `R-001`–`R-254`  
**Operational result:** 37 retained risks

## Purpose

The original risk-storming exercise deliberately favoured coverage and produced 254 records or candidates. This map preserves that history while showing how duplicate, narrow and lower-value candidates were consolidated into the smaller operational set in [Risk-Register.md](Risk-Register.md).

The original candidate files remain read-only identification evidence. They are not the live operational register.

## Consolidation rules applied

A candidate was retained only when it represented a materially different uncertainty that needs its own owner, trigger, validation, mitigation or contingency.

A candidate was merged when it was:

- another cause, symptom or example of an existing risk;
- too narrow to justify separate management;
- duplicated across technology, testing or reporting categories;
- better handled as part of the same treatment plan.

Fixed boundaries remain in the Constraint Register. Beliefs requiring proof remain in the Assumption Register. Exact permission and packaging facts remain in the Licence Register. Existing failures belong in the Failure Register, with a linked risk retained only where recurrence remains possible.

## Retained risks and merged candidate IDs

| Retained ID | Operational subject | Candidate IDs merged into this risk |
|---|---|---|
| R-001 | Schedule overrun | `R-006`, `R-007`, `R-008`, `R-009`, `R-010`, `R-012`, `R-013` |
| R-003 | Scope overload and displacement | `R-004`, `R-005`, `R-011`, `R-118` |
| R-017 | Inconsistent controlled scope and traceability | `R-014`, `R-015`, `R-016`, `R-018`, `R-019`, `R-020`, `R-021`, `R-024`, `R-025`, `R-223` |
| R-023 | Premature implementation or verification claims | `R-022`, `R-198`, `R-222`, `R-249` |
| R-026 | Clean Windows build, packaging, installation and launch failure | `R-027`, `R-028`, `R-029`, `R-030`, `R-031`, `R-032`, `R-039`, `R-078`, `R-079`, `R-239`, `R-240`, `R-241`, `R-253`, `R-254` |
| R-033 | UI responsiveness, process control, cancellation and cleanup failure | `R-034`, `R-035`, `R-036`, `R-037`, `R-038`, `R-063`, `R-064`, `R-174`, `R-175`, `R-177`, `R-182` |
| R-040 | Invalid or unsupported model classification | `R-041`, `R-042`, `R-043`, `R-044`, `R-045`, `R-049`, `R-050`, `R-054` |
| R-046 | Untrusted or incomplete model source and integrity failure | `R-047`, `R-048` |
| R-051 | Original or generated model artefact damage | `R-052` |
| R-053 | Runtime KV-cache and persistent artefact confusion | `R-076`, `R-171` |
| R-055 | Granite and upstream llama.cpp incompatibility | `R-056`, `R-057`, `R-065` |
| R-058 | Runtime CLI/API/output contract drift | `R-059`, `R-060`, `R-061`, `R-062`, `R-066`, `R-074`, `R-077` |
| R-067 | OpenVINO conversion, load or generation failure | `R-068`, `R-069`, `R-070`, `R-075`, `R-190` |
| R-071 | Requested-versus-actual device misreporting | `R-072`, `R-073`, `R-085`, `R-181`, `R-204` |
| R-080 | OpenVINO and custom TurboQuant integration failure | None; retained as a distinct integration risk. |
| R-002 | TurboQuant failure, non-activation or silent fallback | `R-082`, `R-083`, `R-084`, `R-090`, `R-094`, `R-095`, `R-096`, `R-097`, `R-205` |
| R-086 | TurboQuant quality degradation | `R-188`, `R-189` |
| R-087 | TurboQuant performance, memory-benefit or stability shortfall | `R-088`, `R-089`, `R-139`, `R-140`, `R-141`, `R-142` |
| R-099 | Selected TurboVec demonstrator unsuitable for intended Intel or application promotion | `R-100`, `R-101`, `R-102`, `R-103`, `R-104`, `R-117`, `R-119` |
| R-105 | TurboVec pipeline integration and retrieval-quality failure | `R-106`, `R-107`, `R-108`, `R-109`, `R-110`, `R-111`, `R-112`, `R-113`, `R-114`, `R-115`, `R-116`, `R-191`, `R-192` |
| R-121 | Hardware and backend capability detection error | `R-122`, `R-131`, `R-132`, `R-133`, `R-138` |
| R-123 | Unsafe or misleading memory-fit estimate | `R-124`, `R-125`, `R-126`, `R-127`, `R-128`, `R-129`, `R-130` |
| R-151 | Unsafe command and path handling | `R-152`, `R-153`, `R-154` |
| R-155 | Malformed input or native-component security failure | None; retained because it needs separate isolation and incident controls. |
| R-157 | Unexpected network or local-port dependency | `R-158`, `R-164` |
| R-159 | Sensitive-data, secret or local-path leakage | `R-120`, `R-160`, `R-161`, `R-162`, `R-163`, `R-169`, `R-242`, `R-243` |
| R-165 | Privacy, sector-approval or deployment-readiness overclaim | `R-166`, `R-252` |
| R-170 | Non-specialist usability and accessibility failure | `R-172`, `R-173`, `R-176`, `R-178`, `R-179`, `R-180`, `R-183`, `R-184` |
| R-185 | Incorrect or misleading AI output and user over-reliance | `R-167`, `R-168`, `R-186`, `R-187`, `R-197` |
| R-200 | Evidence loss, corruption or missing backup | `R-201`, `R-207`, `R-244` |
| R-202 | Missing experiment identity, actual state or result provenance | `R-203`, `R-206`, `R-208`, `R-212`, `R-213`, `R-214`, `R-215`, `R-224` |
| R-211 | Unfair comparison, hidden failures or invalid test sequencing | `R-081`, `R-134`, `R-135`, `R-136`, `R-143`, `R-144`, `R-145`, `R-146`, `R-147`, `R-148`, `R-149`, `R-209`, `R-210` |
| R-216 | Weak or changing quality-evaluation method | `R-193`, `R-194`, `R-195`, `R-196`, `R-217` |
| R-218 | Non-reproducible build, test or experiment environment | `R-219`, `R-220`, `R-221` |
| R-225 | Unresolved licence, redistribution or attribution position | `R-226`, `R-227`, `R-228`, `R-229`, `R-230`, `R-231`, `R-232`, `R-233`, `R-234`, `R-235`, `R-236`, `R-237` |
| R-238 | Compromised or replaced third-party supply-chain item | `R-156` |
| R-245 | Documentation, compatibility or result overclaim | `R-091`, `R-092`, `R-093`, `R-098`, `R-137`, `R-150`, `R-199`, `R-246`, `R-247`, `R-248`, `R-250`, `R-251` |

## Decision boundary

The merged IDs are not deleted or reused. Their original wording remains in the candidate backlog for audit history, but their operational status is **Superseded by the retained risk shown above**.

Future reviews should update the retained operational risk rather than create another row for a new example of the same uncertainty. A genuinely new risk may receive a new stable ID only when its cause, treatment or contingency is materially different.
