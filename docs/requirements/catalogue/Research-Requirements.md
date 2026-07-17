# Research Requirements

**Baseline:** MoSCoW v1.2 / RTM v1.3  
**Owner:** Arian B  
**Presentation change:** CHG-014 / CR-014 — no scope change  

> The controlled workbook and full RTM contain rationale, mappings, acceptance criteria, verification methods and evidence paths.

| ID | Priority | Release Role | Lifecycle | Requirement |
|---|---|---|---|---|
| R-M01 | Must | Research | Active | The project must compare at least one verified TurboQuant run with a matched standard KV-cache baseline. |
| R-M02 | Must | Research | Active | The project must identify, pin and document the exact TurboVec implementation and decide its release role. |
| R-M03 | Must | Research | Active | The project must complete and preserve an official OpenVINO GenAI Granite baseline or a reproducible blocker. |
| R-M04 | Must | Research | Active | The project must measure memory use for every final test configuration. |
| R-M05 | Must | Research | Active | The project must evaluate runtime performance for every final test configuration. |
| R-M06 | Must | Research | Active | The project must compare output quality with a fixed prompt set and scoring guide. |
| R-M07 | Must | Research | Active | The project must identify the largest stable tested context for selected final configurations. |
| R-M08 | Must | Core | Active | The project must preserve a complete record of every final experiment. |
| R-M09 | Must | Core | Active | The project must record failed attempts and known limitations. |
| R-M10 | Must | Core | Active | Another developer must be able to reproduce the clean build and core result from written instructions. |
| R-M11 | Must | Research | Active | The project must determine which selected Granite/runtime/backend combinations run reliably on the tested Intel CPU and integrated GPU. |
| R-M12 | Must | Research | Active | The project must assess selected complete configurations against 4 GB, 8 GB and 16 GB total system-memory budgets. |
| R-M14 | Must | Research | Active | The project must quantify memory-estimator error and false-safe/false-unsafe recommendations. |
| R-M13 | Should | Deferred | Deferred | If TurboVec integration is reactivated, the project should compare compressed or optimised vectors with an uncompressed-vector baseline. |
| R-S01 | Should | Superseded | Superseded | The project should run a small TurboVec feasibility test if the scope decision supports it. |

Do not edit this category file independently of the controlled RTM workbook.
