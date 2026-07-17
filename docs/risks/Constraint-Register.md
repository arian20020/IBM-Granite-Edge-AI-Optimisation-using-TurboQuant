# Constraint Register

**Document ID:** REG-CON-001  
**Version:** 0.3  
**Status:** Approved active constraints — compliance evidence reviewed at dependent gates  
**Owner:** Arian B  
**Effective date:** 2026-07-14  
**Last reviewed:** 2026-07-14  
**Next review:** At material scope changes, dependent gates and baseline review  
**Related requirement:** `G-M05`  
**Related work package:** `PD-05`  
**Related engineering practice:** `EP-007`  
**Related change:** `CR-018` / `CHG-018`  
**Related review:** `RV-006`

## Purpose

This register records fixed or externally imposed boundaries that the project must work within. A constraint is not an uncertain event: it is a condition that limits the permitted solution, evidence, schedule, hardware, deployment, licensing or scope.

The entries below consolidate the important schedule, platform, hardware, privacy, licensing, evidence and academic boundaries already present across the Project Definition, requirements, evaluation controls and UCL project guidance.

## Approval decision

The constraints have been reviewed and approved as the active boundaries for the current developer working baseline.

Approval means that the project accepts and will plan, implement, test and report within these boundaries. It does not mean that every later compliance test has already passed. For example, the local/no-port boundary is approved now, while its final network-observation evidence is produced at the runtime and release gates.

## Status vocabulary

| Status | Meaning |
|---|---|
| Active | The constraint currently applies and has been approved. |
| Changed | The constraint remains relevant but its controlled wording or boundary has changed. |
| Removed | The constraint no longer applies, with the reason recorded. |
| Superseded | A later constraint record replaces this one. |

## Constraint records

| Constraint ID | Constraint | Source | Affected scope | Impact | Project response | Owner | Status | Evidence | Related IDs | Date raised | Last reviewed | Next review |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| C-001 | The project must be completed within the fixed MSc project timetable. | UCL MSc project guidance; Project Definition schedule. | All planning, implementation, testing, evidence and report work. | Development, integration and evaluation time is limited. | Protect the approved first-release scope, review progress weekly and use change control when a deadline or scope boundary changes. | Arian B | Active | UCL schedule and project timetable. | `PD-01`; `PD-05`; `R-001` | 2026-07-14 | 2026-07-14 | Weekly during development |
| C-002 | The first application release is a Windows 11 x64 desktop application built with WinUI 3. | Project Definition and active application requirements. | UI, application architecture, packaging, deployment and compatibility testing. | macOS, Linux, web and mobile releases are not part of the tested Windows release. | Design, build, package and test the release for Windows 11 x64; record other platforms as untested unless scope is formally changed. | Arian B | Active | Project Definition; WinUI project file; release evidence at packaging gate. | Windows application requirements; `PD-04` | 2026-07-14 | 2026-07-14 | At architecture and release reviews |
| C-003 | The project has an Intel hardware focus. | IBM/Intel project brief and Project Definition. | Hardware detection, runtime selection, optimisation and performance evaluation. | Implementation and conclusions must prioritise the Intel routes actually tested. | Prioritise Intel CPU and GPU routes and keep hardware detection separate from proven inference support. | Arian B | Active | Project Definition and target-hardware records. | `RQ3`; hardware-analysis requirements | 2026-07-14 | 2026-07-14 | Before each device gate |
| C-004 | Validation is limited to the Windows Intel hardware that is actually available to the project. | Project Definition constraints. | Compatibility, memory, performance and device claims. | The project cannot establish universal Intel CPU, GPU or NPU compatibility. | Record exact processor, graphics device, drivers, RAM, operating system and runtime for every controlled test; limit conclusions to those configurations. | Arian B | Active | Hardware manifests and experiment evidence. | `A-006`; `A-007`; `R-121`; `R-123` | 2026-07-14 | 2026-07-14 | Before each experiment campaign |
| C-005 | The core AI workflow must run locally and must not depend on a cloud AI service. | Problem statement, privacy requirements and target-user needs. | Model import, inference, chat, document processing and evaluation. | Core prompts, models and user documents must remain on the local computer. | Use local runtimes; treat internet-dependent downloading as a separate optional activity with clear consent and integrity controls. | Arian B | Active | Architecture, offline test and network-observation evidence at dependent gates. | `A-013`; `R-157`; privacy and offline requirements | 2026-07-14 | 2026-07-14 | At architecture, security and release reviews |
| C-006 | The supported core inference route must not require an open local HTTP server port. | Project design decision for restricted healthcare, education and institutional environments. | Runtime architecture and application-to-backend communication. | A server-only design may be unsuitable or blocked on some target organisational devices. | Use direct command-line process control or direct runtime APIs for the supported route; label any separate experimental server route clearly. | Arian B | Active | Runtime architecture and no-port test evidence at integration gate. | `A-013`; `R-157`; process-control requirements | 2026-07-14 | 2026-07-14 | At runtime integration gate |
| C-007 | Implementation and testing must operate within the available RAM, graphics memory, storage and compute capability. | Project Definition constraints and available hardware. | Model selection, context length, OpenVINO, TurboQuant, TurboVec investigation and experiment execution. | Some models, conversions and context lengths cannot run safely on the available machines. | Use transparent memory-fit estimates, safety reserves, bounded model sizes, disk checks and controlled stopping rules. | Arian B | Active | Hardware inventory, estimator evidence and stopping-rule logs. | `A-008`; `R-123`; memory-fit requirements; `RQ3` | 2026-07-14 | 2026-07-14 | Before model/configuration approval |
| C-008 | Models, runtimes, libraries, forks, datasets and assets may only be used or distributed according to their exact licences. | Project Definition, UCL IP guidance and third-party licence terms. | Source use, model use, modification, packaging, redistribution and final release. | A component may be usable for research but unsuitable for bundling or redistribution. | Maintain the Licence Register, pin exact versions, include required notices and exclude or separately acquire restricted components. | Arian B | Active | Licence Register; `Licence-Review-Notes.md`; authoritative licence files. | `A-016`; `R-225`; `PD-05` | 2026-07-14 | 2026-07-14 | When a dependency changes and before release |
| C-009 | OpenVINO, TurboQuant and TurboVec claims must be limited to exact pinned and tested configurations. | Project Definition, experiment plan and reproducibility controls. | Experimental integration, comparison and final reporting. | One successful model, build or device result cannot prove broad compatibility or benefit. | Record exact model revisions, commits, build flags, cache or vector settings, devices and evidence for every claim. | Arian B | Active | Repository register, manifests and controlled experiment evidence. | `A-003`; `A-005`; `A-009`; `A-011`; `A-014` | 2026-07-14 | 2026-07-14 | At each technical gate and report review |
| C-010 | Requested settings and actual runtime behaviour must be recorded separately. | Project Definition technical distinctions and truthful-reporting requirements. | Runtime, backend, device, cache, optimisation and fallback reporting. | A user selection does not prove that the selected route actually executed. | Preserve requested and actual states in the UI, logs, manifests and evidence; label fallback clearly. | Arian B | Active | Runtime contracts and requested-versus-actual test evidence. | `A-007`; `A-010`; `R-071`; `N-M11`; `QX-04` | 2026-07-14 | 2026-07-14 | At runtime contract review |
| C-011 | Published results, calculated estimates and project measurements must remain separate. | Evaluation and evidence controls. | Memory, speed, quality, context, compression and retrieval reporting. | External claims or calculations cannot be presented as measured project results. | Label every result as published, estimated or measured and link measured claims to exact experiment and evidence IDs. | Arian B | Active | Evaluation plan, workbooks and report traceability. | `A-014`; `R-211`; evidence-governance requirements | 2026-07-14 | 2026-07-14 | At every result and report review |
| C-012 | Real patient, pupil or other sensitive personal data must not be used in development, testing or demonstrations. | Project safety, privacy and ethical boundaries. | Prompt sets, imported documents, screenshots, logs, usability testing and evidence. | Intended sector use does not permit uncontrolled sensitive data in the research prototype or repository. | Use synthetic, public or properly authorised and anonymised material; inspect evidence before commit or release. | Arian B | Active | Data inventory, test-data provenance and repository review. | `R-159`; security/privacy requirements | 2026-07-14 | 2026-07-14 | Before every user or release evaluation |
| C-013 | The application is a research prototype and is not an approved clinical, NHS, school or classroom system. | Project Definition claim boundaries. | UI wording, demonstrations, manuals, evaluation and final report. | The project cannot claim professional approval, guaranteed correctness or automatic privacy, security or legal compliance. | Display appropriate prototype and Experimental labels, retain limitations and avoid medical, educational or compliance guarantees. | Arian B | Active | README, manuals, UI wording and report review. | `R-165`; `R-185`; safety and claims requirements | 2026-07-14 | 2026-07-14 | At UX, release and report reviews |
| C-014 | Project evidence and final claims must remain traceable to the controlled RTM and repository records. | Evidence governance, `G-M05`, `PD-05` and `EP-007`. | Requirements, work packages, experiments, evidence packs, release and report. | A deliverable cannot be treated as Verified only because a file or implementation exists. | Require acceptance checks, evidence records, exact paths, validation outcomes and RTM updates before a Verified claim. | Arian B | Active | Evidence template, RTM and validation audit. | `G-M05`; `PD-05`; `EP-007`; `R-023` | 2026-07-14 | 2026-07-14 | Weekly and before baseline/release reviews |
| C-015 | The work and final submission must remain the student's individual MSc contribution, while external guidance and sources are acknowledged. | UCL MSc individual-project and academic-integrity guidance. | Development ownership, documentation, evaluation and report writing. | External help cannot replace the student's own implementation, analysis, understanding or authorship. | Keep journals, commits and citations; record supervisor and partner guidance; verify that submitted work is understood and individually produced. | Arian B | Active | UCL guidance, Git history, journal and references. | academic integrity; report requirements | 2026-07-14 | 2026-07-14 | At report and final submission reviews |

## Entry and maintenance rule

Each constraint must identify:

- the exact boundary;
- its authoritative source;
- the requirements, work packages, experiments or release decisions it affects;
- the practical impact on the project;
- how the project will work within it;
- an owner, status, evidence and review dates.

A limitation should not be recorded as a constraint unless the project must genuinely operate within it. Uncertain future problems belong in the Risk Register instead.

A material change to an active constraint must use project change control and update every affected requirement, risk, assumption, test, evidence record and report section.
