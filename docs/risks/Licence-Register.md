# Licence Register

> Latest technical result: **DEMONSTRATOR_ONLY** for TurboVec commit `ccab9f325e6ce2a270a87daf01ae4e443bcf2d49`, run `EXP-TV-COMP-001-20260902T231605Z-005`. MIT identity is established; adoption and distribution remain unapproved.

> TurboVec identity: `https://github.com/RyanCodrai/turbovec`, commit `ccab9f325e6ce2a270a87daf01ae4e443bcf2d49`, MIT. Outcome **BLOCKED** in `EXP-TV-COMP-001-20260902T225731Z-001`; this does not approve distribution or adoption.

**Document ID:** REG-LIC-001  
**Version:** 0.5
**Status:** Selected development dependencies reviewed — exact release-package review pending  
**Owner:** Arian B  
**Effective date:** 2026-07-14  
**Last reviewed:** 2026-08-09
**Next review:** When exact release artefacts are adopted and before release packaging  
**Related requirement:** `G-M05`  
**Related work package:** `PD-05`  
**Related engineering practice:** `EP-007`  
**Related change:** `CR-018` / `CHG-018`  
**Related review:** `RV-007`  
**Supporting evidence:** [Licence Review Notes](Licence-Review-Notes.md), [ADR-001](../architecture/decisions/ADR-001-llamasharp-application-runtime.md)

## Purpose

This register records the licence and permitted-use position for third-party models, runtimes, libraries, forks, datasets, prompt sets, icons, fonts, copied research code, executables and other external material used by or distributed with the project.

The register separates permission to research, build or modify a component from permission to bundle and redistribute a specific release artefact. A repository licence does not automatically control every binary package, converted model, transitive dependency or asset distributed with that repository.

This is an engineering compliance record, not legal advice. Ambiguity remains `Pending` or `Restricted` and must not be interpreted as permission to distribute.

## Review-status vocabulary

| Review status | Meaning |
|---|---|
| Pending | The exact component, revision, provenance or licence decision is unresolved. The item must not be bundled. |
| Approved | Use is permitted for the stated purpose and packaging decision, subject to the recorded duties. |
| Restricted | Use is permitted only under the recorded conditions; further packaging or dependency review remains. |
| Rejected | The component or material must not be used or distributed in the proposed way. |
| Superseded | A later version or licence decision replaces this record. |

## Licence records

| ID | Component / material | Exact identity | Licence basis | Use | Modify | Redistribute | Main duties / restrictions | Packaging decision | Status | Next review |
|---|---|---|---|---|---|---|---|---|---|---|
| L-001 | Official IBM Granite 4.1 language models | Official `ibm-granite/granite-4.1-3b` and `ibm-granite/granite-4.1-8b`; exact revision and final artefact pending | Apache-2.0 on both official IBM model cards | Permitted | Permitted under Apache-2.0 | Permitted under Apache-2.0 conditions | Pin repository, revision, filename and hash; retain licence/notices; review any community GGUF source separately | Development and controlled conversion permitted. Do not bundle weights or converted artefacts until exact provenance and release inventory are approved | Restricted | When the exact model and artefact are selected |
| L-002 | ggml-org/llama.cpp research runtime | Tag `b9870`; commit `2d973636e292ee6f75fadcf08d29cb33511f509f` | MIT | Permitted | Permitted | Permitted | Preserve copyright and MIT permission notice; review optional compiled dependencies | Source/build use and redistribution of the pinned component are approved. This identity is retained as research evidence and is not the selected embedded LLamaSharp application runtime | Approved | If commit, backend dependencies, evidence role or packaging changes |
| L-003 | OpenVINO Toolkit | Exact release pending | Apache-2.0 | Permitted | Permitted | Permitted under conditions | Include Apache licence; preserve notices; mark modified files; include relevant NOTICE/third-party content | Development, conversion and runtime use permitted. Final bundling waits for exact DLL and notice inventory | Restricted | When the exact release is pinned and before packaging |
| L-004 | OpenVINO GenAI | Exact GenAI, Runtime and tokenizer releases pending | Apache-2.0 | Permitted | Permitted | Permitted under conditions | Same Apache duties; separately record GenAI, Runtime, tokenizer and native dependencies | Development and testing permitted. Final bundling waits for exact dependency and notice inventory | Restricted | When releases are pinned and before packaging |
| L-005 | Microsoft.WindowsAppSDK NuGet package | Version `2.2.0` from the project file | Package-specific terms; public source repository is MIT | Permitted for application development | Do not assume unrestricted modification of packaged Microsoft binaries; source modifications follow applicable source terms | Only files permitted by exact package/deployment terms | Preserve applicable licences/notices; record component-package dependencies; do not imply Microsoft endorsement | Use through NuGet. Final application redistribution requires package/dependency inspection and notice inventory | Restricted | When package version or deployment model changes and before packaging |
| L-006 | Microsoft.Windows.SDK.BuildTools NuGet package | Version `10.0.28000.2270` | Exact package terms apply | Permitted as a build dependency | Not required for the project | Do not redistribute SDK tooling unless exact terms explicitly permit it | Keep as build-time dependency; inspect final output to ensure SDK tools are not shipped accidentally | Build-time use only | Restricted | Before the first release build |
| L-007 | .NET 8 SDK/runtime and base libraries | `net8.0-windows`; exact runtime patch pending | MIT for main repositories plus third-party notices and component-specific terms | Permitted | Permitted where applicable | Permitted subject to included-file terms and notices | Record exact runtime patch and third-party notices, especially for self-contained publication | Development use permitted. Framework-dependent release is preferred until self-contained inventory is reviewed | Restricted | Before self-contained or packaged release |
| L-008 | AtomicBot-ai TurboQuant llama.cpp fork | Branch `feature/turboquant-kv-cache`; commit `b0e900a28ee4172bbb97df0d1ea1c78e86bc0ac6` | MIT in pinned repository, plus inherited dependencies | Permitted | Permitted | Source permitted; binary redistribution depends on compiled dependencies | Preserve MIT and inherited notices; record backend/native libraries; technical validity remains separate | Research, build, modification and testing permitted. Final binary bundling waits for dependency review | Restricted | Before adoption as the application route or packaging |
| L-009 | animehacker TurboQuant llama.cpp fork | Branch `main`; commit `5bc5ed3bdc25003aa9f07422753a7b8d4f9190fc` | MIT in pinned repository, plus inherited dependencies | Permitted | Permitted | Source permitted; binary redistribution depends on compiled dependencies | Preserve MIT and inherited notices; review SYCL/oneAPI/native components; do not misdescribe Stage-1 implementation as full QJL-enabled TurboQuant | Research, build, modification and testing permitted. Final binary bundling waits for dependency review | Restricted | Before adoption as the application route or packaging |
| L-010 | Selected TurboVec implementation | No repository or commit selected | Unknown | No external implementation permission assumed | No permission assumed | No permission assumed | A paper, idea or public snippet is not a software licence; identify repository, commit, licence and dependencies first | Do not copy, integrate or distribute external TurboVec code yet | Pending | When the feasibility investigation selects a candidate |
| L-011 | Evaluation prompts, synthetic documents and retrieval test data | Project-authored assets plus any later external sources | Project-authored material may be controlled by the project; external terms vary | Project-authored and clearly licensed material permitted | Depends on ownership/source | Depends on ownership/source | Maintain prompt/data manifest and provenance; exclude sensitive, unclear or non-redistributable material | Use project-authored synthetic material by default; review every external benchmark or document separately | Restricted | Before freezing evaluation assets |
| L-012 | Application icons, images, fonts and design assets | Current WinUI assets; complete provenance manifest pending | Project-created, Microsoft-template or third-party terms depending on asset | Depends on source | Depends on source | Depends on source | Record each asset’s source and licence; replace unclear assets | Do not approve final packaging until the asset-provenance manifest is complete | Pending | Before UI freeze and release packaging |
| L-013 | Research papers, textbooks and source extracts | Source-specific publications | Copyright, publisher terms or explicit open licence | Reading, analysis and citation permitted | Limited quotation/adaptation only where lawful | Full-text redistribution is not assumed | Cite accurately; use limited quotation; do not commit or ship full copyrighted books/papers without permission | Reference-only; exclude full copyrighted publications from repository and release | Restricted | When adding a new source or extract |
| L-014 | This project’s own source code and documentation | Repository currently has no root `LICENSE` file | Owner/UCL/partner decision pending | Owner may use the work | Owner decision required for third-party permissions | No broad public redistribution permission is currently granted by a project licence | Consider UCL, IBM/Intel partner, third-party code and submission obligations before selecting a licence | Do not claim the repository is MIT, Apache or another open-source licence until a root licence is approved and added | Pending | Before public release or inviting external reuse |
| L-015 | Selected GGUF, quantised or OpenVINO-converted model artefacts | Exact conversion repositories, revisions, commands and output hashes pending | Derived from the source-model licence plus any converter/repository terms | Controlled project use depends on verified provenance | Conversion may be permitted under the source-model licence | Redistribution depends on source-model duties and exact conversion provenance | Link each output to the official source model, conversion tool, command, revision, licence and hash; review community-hosted GGUF repositories separately | Do not bundle a converted model merely because the upstream IBM model is Apache-2.0 | Pending | When each final model artefact is chosen |
| L-016 | LLamaSharp managed wrapper and published CPU backend | `LLamaSharp` `0.27.0`; `LLamaSharp.Backend.Cpu` `0.27.0`; LLamaSharp tag `v0.27.0`, release commit `7cbbc45e421d55794d5050d126e0b96511007007`; mapped llama.cpp commit `3f7c29d318e317b63f54c558bc69803963d7d88c` | MIT package licence expression and MIT source repository; native package includes the matched llama.cpp-derived CPU libraries and their dependency notices must still be inspected | Permitted for feasibility work and application development | Permitted under MIT for source; modifying published binaries is not required | Source/package redistribution is permitted under applicable MIT and bundled third-party terms, but exact produced application inventory must be reviewed | Preserve LLamaSharp and llama.cpp copyright/MIT notices; inspect NuGet package contents, transitive packages and native DLL notice files; keep exact managed/backend versions aligned; do not describe the separate `b9870` research build as the embedded runtime | Native smoke, controlled VocabOnly completion, and the exact 44-file x64 development/test package inventory now pass. Local development packaging is permitted; distribution and final release remain blocked pending human release-notice/third-party-notice review and the repository-wide release gate | Restricted | Whenever either package changes, before distribution, and before final release packaging |
| L-017 | Inter static font faces for Model Inspection | Official Inter `4.1` release archive `Inter-4.1.zip`, archive SHA-256 `9883fdd4a49d4fb66bd8177ba6625ef9a64aa45899767dde3d36aa425756b11e`; only `Inter-Regular.ttf`, `Inter-Bold.ttf`, and the upstream licence text are adopted | SIL Open Font License 1.1, retained verbatim as `Assets/Fonts/OFL.txt` | Permitted | Permitted under the OFL | Permitted under the OFL when the licence and reserved-name conditions are retained | Keep the OFL text with the packaged faces; do not substitute a system font or unrecorded release; preserve exact byte hashes in `inter-manifest.json` | The two required static faces and full OFL text are approved for application/test packaging; no variable, italic, or additional weight payload is included | Approved | If the Inter release, face set, hashes, font naming, or packaging changes |

## Current decision summary

| Decision area | Current conclusion |
|---|---|
| Research, build and modification of standalone llama.cpp `b9870` | Approved for the pinned MIT source; retained as research evidence |
| Initial application GGUF runtime pair | `LLamaSharp` 0.27.0 plus `LLamaSharp.Backend.Cpu` 0.27.0 selected for the protected x64 worker; local development/test packaging is verified, while distribution remains restricted pending release-notice review |
| Research, build and modification of reviewed TurboQuant forks | Permitted under MIT, with inherited dependency duties |
| OpenVINO and OpenVINO GenAI development | Permitted under Apache-2.0 |
| Official Granite 4.1 3B/8B use and conversion | Permitted under Apache-2.0, subject to exact artefact provenance |
| One final bundle containing all runtimes, models and assets | Not yet approved |
| TurboVec implementation code | Not yet selected or licensed |
| Project’s own public reuse licence | Not yet selected |

## Release licence gate

Before a component can be bundled into a release, record:

1. the exact version, revision, package or model artefact;
2. the authoritative licence and any NOTICE or third-party-notice files;
3. every file that will actually be distributed;
4. attribution, modification, patent, source-disclosure or end-user-term duties;
5. whether the component is bundled, downloaded separately, used only during development or excluded;
6. the packaging decision and reviewer;
7. a new review date whenever the component changes.

A `Pending` item must not be bundled. A `Restricted` item may be used only within the stated conditions. The final release review must inspect the actual produced package rather than approving from project references alone.
