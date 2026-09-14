# Licence Register

> Final review note: TurboVec commit `ccab9f325e6ce2a270a87daf01ae4e443bcf2d49` is MIT-licensed, but its project decision is **DEMONSTRATOR_ONLY**. This does not approve application adoption or final package distribution.

**Document ID:** REG-LIC-001  
**Version:** 0.5<br>
**Status:** Initial source-licence review complete — exact release-package review pending  
**Owner:** Arian B  
**Effective date:** 2026-07-14  
**Last reviewed:** 2026-09-13<br>
**Next review:** Before release packaging or when a listed component changes<br>
**Related requirement:** `G-M05`  
**Related work package:** `PD-05`  
**Related engineering practice:** `EP-007`  
**Related change:** `CR-018` / `CHG-018`  
**Related reviews:** `RV-007`; `RV-009`; `RV-010`<br>
**Supporting evidence:** [Licence Review Notes](Licence-Review-Notes.md)

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
| L-002 | ggml-org/llama.cpp | Tag `b9870`; commit `2d973636e292ee6f75fadcf08d29cb33511f509f` | MIT | Permitted | Permitted | Permitted | Preserve copyright and MIT permission notice; review optional compiled dependencies | Source/build use and redistribution of the pinned component are approved. Final binary must include applicable notices | Approved | If commit, backend dependencies or packaging changes |
| L-003 | OpenVINO Toolkit | Campaign version `2026.2.1-21919-ede283a88e3` | Apache-2.0 | Permitted | Permitted | Permitted under conditions | Include the Apache licence, applicable NOTICE material and third-party notices | Development, conversion and runtime use are permitted. Final bundling waits for the produced DLL inventory and notices | Restricted | Before release packaging or a version change |
| L-004 | OpenVINO GenAI | Version `2026.2.1.0-2-00edae3bfd4`; upstream `7dea0459b2ac7d8dfd877fd9df6737674`; patch `00edae3bfd40a968c964ea4878128dceeeb22d1a`; derived tree `2e872dd4817c42d91cb7c3094954d7b56fa12b0a`; verified application worker closure `0c642015d9b6912533d8c6d5e8f136e97f1071aa71ff62b6dbf46f818771a6f9` | Apache-2.0 | Permitted | Permitted | Permitted under conditions | Include the Apache licence and record GenAI, Runtime, tokenizer and native dependency notices | Development and tested application use are permitted. Final bundling waits for the produced worker inventory and notices | Restricted | Before release packaging or a version change |
| L-005 | Microsoft.WindowsAppSDK NuGet package | Version `2.2.0` from the project file | Package-specific terms; public source repository is MIT | Permitted for application development | Do not assume unrestricted modification of packaged Microsoft binaries; source modifications follow applicable source terms | Only files permitted by exact package/deployment terms | Preserve applicable licences/notices; record component-package dependencies; do not imply Microsoft endorsement | Use through NuGet. Final application redistribution requires package/dependency inspection and notice inventory | Restricted | When package version or deployment model changes and before packaging |
| L-006 | Microsoft.Windows.SDK.BuildTools NuGet package | Version `10.0.28000.2270` | Exact package terms apply | Permitted as a build dependency | Not required for the project | Do not redistribute SDK tooling unless exact terms explicitly permit it | Keep as build-time dependency; inspect final output to ensure SDK tools are not shipped accidentally | Build-time use only | Restricted | Before the first release build |
| L-007 | .NET SDK/runtime and base libraries | Application target `net8.0-windows10.0.19041.0`; build SDK `10.0.301`; exact shipped runtime patch and package mode pending | MIT for main repositories plus third-party notices and component-specific terms | Permitted | Permitted where applicable | Permitted subject to included-file terms and notices | Keep the build SDK separate from the runtime shipped with the application; record the exact runtime files and notices | Development use is permitted. Self-contained packaging remains restricted until its file inventory is reviewed | Restricted | Before self-contained or packaged release |
| L-008 | AtomicBot-ai TurboQuant llama.cpp fork | Branch `feature/turboquant-kv-cache`; commit `b0e900a28ee4172bbb97df0d1ea1c78e86bc0ac6`; application evidence manifest `08EF00CF8CD425BC5409071A77C12BE5A90292C121A5109EDDE63BB109B3B7C4`; supported candidate `GGUF-CURRENT-08EF-CPU-TURBO3-COMPAT-01` | MIT in pinned repository, plus inherited dependencies | Permitted | Permitted | Source permitted; binary redistribution depends on compiled dependencies | Preserve MIT and inherited notices; record the packaged backend and native libraries | Research, build and tested application use are permitted. Final binary bundling waits for the produced dependency and notice inventory | Restricted | Before release packaging or an identity change |
| L-009 | animehacker TurboQuant llama.cpp fork | Branch `main`; commit `5bc5ed3bdc25003aa9f07422753a7b8d4f9190fc` | MIT in pinned repository, plus inherited dependencies | Permitted | Permitted | Source permitted; binary redistribution depends on compiled dependencies | Preserve MIT and inherited notices; review SYCL/oneAPI/native components; do not misdescribe Stage-1 implementation as full QJL-enabled TurboQuant | Research, build, modification and testing permitted. Final binary bundling waits for dependency review | Restricted | Before adoption as the application route or packaging |
| L-010 | Selected TurboVec implementation | `https://github.com/RyanCodrai/turbovec`; version `1.0.0`; commit `ccab9f325e6ce2a270a87daf01ae4e443bcf2d49`; wheel SHA-256 `CD855E0B318A57DC57C733F9A62AE98DE5192F4F6C2C760E305523E8CEB1B090` | MIT in the pinned repository | Permitted | Permitted | Permitted with the MIT notice | Preserve the MIT notice and the notices for the demonstrator's other dependencies | The component was used only in the separate feasibility demonstrator. It was not adopted by the application | Restricted | If the demonstrator is distributed or integration is reconsidered |
| L-011 | Evaluation prompts, synthetic documents and retrieval test data | Project-authored assets plus any later external sources | Project-authored material may be controlled by the project; external terms vary | Project-authored and clearly licensed material permitted | Depends on ownership/source | Depends on ownership/source | Maintain prompt/data manifest and provenance; exclude sensitive, unclear or non-redistributable material | Use project-authored synthetic material by default; review every external benchmark or document separately | Restricted | Before freezing evaluation assets |
| L-012 | Application icons, images, fonts and design assets | Current WinUI assets; complete provenance manifest pending | Project-created, Microsoft-template or third-party terms depending on asset | Depends on source | Depends on source | Depends on source | Record each asset’s source and licence; replace unclear assets | Do not approve final packaging until the asset-provenance manifest is complete | Pending | Before UI freeze and release packaging |
| L-013 | Research papers, textbooks and source extracts | Source-specific publications | Copyright, publisher terms or explicit open licence | Reading, analysis and citation permitted | Limited quotation/adaptation only where lawful | Full-text redistribution is not assumed | Cite accurately; use limited quotation; do not commit or ship full copyrighted books/papers without permission | Reference-only; exclude full copyrighted publications from repository and release | Restricted | When adding a new source or extract |
| L-014 | This project’s own source code and documentation | Repository currently has no root `LICENSE` file | Owner/UCL/partner decision pending | Owner may use the work | Owner decision required for third-party permissions | No broad public redistribution permission is currently granted by a project licence | Consider UCL, IBM/Intel partner, third-party code and submission obligations before selecting a licence | Do not claim the repository is MIT, Apache or another open-source licence until a root licence is approved and added | Pending | Before public release or inviting external reuse |
| L-015 | Experiment GGUF, quantised and OpenVINO-converted model artefacts | Exact identities and available hashes are recorded in the [final evidence manifest](../testing/final-results/catalog/evidence-manifest.csv); some planned artefacts were unavailable | Derived from each source-model licence plus converter or repository terms | Controlled project use depends on verified provenance | Depends on the source-model and converter terms | Redistribution depends on exact provenance and notices | Keep unavailable artefacts unavailable; link each retained output to its source, tool, revision, command and hash | Evidence use is allowed only for recorded artefacts. Do not bundle converted experiment models without a separate package decision | Pending | Before any experiment model is distributed |
| L-016 | Official IBM Granite 4.0 H Micro GGUF application catalogue | `ibm-granite/granite-4.0-h-micro-GGUF`; revision `51ce07a9c9cfa971ca359d9625836bf8a4a1b61f`; five filenames, byte lengths and SHA-256 hashes in `PinnedGraniteModelCatalog.cs` | Apache-2.0 on the official IBM model card | Permitted | Permitted under Apache-2.0 | Permitted under Apache-2.0 conditions | Keep the pinned source, revision, filename, byte length and SHA-256 checks; preserve the licence and notices | The application may download and verify these files. The files are not approved for bundling inside the application package | Restricted | If the catalogue changes or model files are bundled |
| L-017 | LLamaSharp and LLamaSharp CPU backend | NuGet versions and source tag `v0.27.0`; LLamaSharp commit `7cbbc45e421d55794d5050d126e0b96511007007`; mapped llama.cpp commit `3f7c29d318e317b63f54c558bc69803963d7d88c` | MIT | Permitted | Permitted | Permitted with the MIT notice and native dependency terms | Preserve both MIT notices and review the native llama.cpp backend files included by the CPU package | Application use is permitted. Final bundling waits for the native file and notice inventory | Restricted | Before release packaging or a package change |
| L-018 | PdfPig | NuGet version `0.1.15`; repository commit `f131f642976936e06ee91cb19d3ed728f9dd18b6` recorded by the package | Apache-2.0 | Permitted | Permitted | Permitted under Apache-2.0 conditions | Preserve the Apache licence and applicable notices | Used only by the separate TurboVec PDF extraction demonstrator, not by the application | Restricted | If the demonstrator is distributed or the package changes |

## Current decision summary

| Decision area | Current conclusion |
|---|---|
| Research, build and modification of llama.cpp | Approved for the pinned MIT source |
| Research, build and modification of reviewed TurboQuant forks | Permitted under MIT, with inherited dependency duties |
| Verified TurboQuant application candidate | Technically admitted for the exact evidence identity; final binary packaging remains Restricted |
| OpenVINO and OpenVINO GenAI development and tested application use | Permitted under Apache-2.0; final worker packaging remains Restricted |
| Official Granite 4.1 3B/8B use and conversion | Permitted under Apache-2.0, subject to exact artefact provenance |
| Official Granite 4.0 H Micro GGUF download catalogue | Permitted under Apache-2.0 with exact source and integrity checks; model files are not approved for bundling |
| One final bundle containing all runtimes, models and assets | Not yet approved |
| TurboVec implementation code | MIT identity recorded; demonstrator-only and not adopted into the application |
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
