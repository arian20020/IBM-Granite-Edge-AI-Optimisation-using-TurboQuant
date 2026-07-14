# Traceability ID Conventions

Stable IDs allow requirements, implementation work, engineering practices, evidence, tests, commits and report sections to refer to the same project item without relying on row numbers.

## Master keys

The Task Checklist uses a globally unique Master Key:

| Master key | Meaning |
|---|---|
| `REQ:G-M01` | Requirement `G-M01` |
| `WP:PD-01` | Work package `PD-01` |
| `EP:EP-001` | Engineering practice `EP-001` |

The numeric Task No. is display order only. Do not use it in branches, links, issue titles or evidence names because row numbers may change when the RTM is updated.

## Requirement IDs

Requirement IDs combine a requirement family, priority and sequence number.

### Requirement families

| Prefix | Meaning |
|---|---|
| `F-` | Functional or user-facing application behaviour |
| `N-` | Non-functional or quality requirement |
| `R-` | Research and evaluation requirement |
| `G-` | Governance, engineering-control or project-assurance requirement |
| `C-` | Could Have scope item |
| `W-` | Won't Have or explicitly excluded scope item |

### Priority marker

| Marker | Meaning |
|---|---|
| `M` | Must Have |
| `S` | Should Have |

Examples:

- `F-M02` = Functional, Must Have, requirement 02.
- `N-M03` = Non-functional, Must Have, requirement 03.
- `R-M01` = Research, Must Have, requirement 01.
- `G-M07` = Governance, Must Have, requirement 07.

The complete requirement baseline also retains Deferred, Superseded and Excluded records. Their history must remain visible so removed scope does not silently return.

## Work-package IDs

Work packages represent executable project work.

| Prefix | Workstream |
|---|---|
| `PD` | Project definition, planning and development control |
| `IM` | Model import, inspection and classification |
| `DL` | Approved model download |
| `HE` | Hardware inspection, memory estimation and configuration selection |
| `RT` | Runtime integration and chat |
| `QX` | GGUF weight quantisation and TurboQuant integration |
| `OV` | OpenVINO investigation and integration |
| `TV` | TurboVec and knowledge-file retrieval |
| `FR` | Final regression, release and handover |

Examples:

- `PD-01` = first project-definition work package.
- `IM-02` = second model-import work package.
- `RT-03` = third runtime/chat work package.

## Engineering-practice IDs

Engineering practices use sequential IDs such as `EP-001` and `EP-024`.

They describe how the project should be engineered, for example:

- freezing the problem, aim and research questions;
- maintaining workflow and architecture records;
- applying branch and pull-request controls;
- implementing unit, contract, integration and end-to-end tests;
- preserving failed experiments;
- completing final evidence and traceability audits.

Engineering practices are not automatically implementation issues. They are linked to the work packages where the practice must be applied.

## Objective and research-question IDs

- Objectives use `O1` to `O11`.
- Research questions use `RQ1` to `RQ4`.
- The bounded TurboVec research question uses `RQ-TV`.

## Recommended use in Git and GitHub

Branch examples:

```text
feature/IM-02-model-picker
feature/RT-03-streaming-cancellation
experiment/OV-01-official-baseline
docs/PD-06-architecture-views
```

Pull-request title examples:

```text
[IM-02] Implement local model file picker
[RT-03] Add streaming output and cancellation
[PD-06] Add controlled architecture views
```

Commit message examples:

```text
feat(IM-02): add packaged file picker service
test(HE-03): add memory-estimator boundary tests
docs(PD-02): correct runtime workflow terminology
```
