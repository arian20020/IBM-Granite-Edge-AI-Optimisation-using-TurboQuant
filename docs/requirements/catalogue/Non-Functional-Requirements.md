# Non-Functional Requirements

**Baseline:** MoSCoW v1.2 / RTM v1.3  
**Owner:** Arian B  
**Presentation change:** CHG-014 / CR-014 — no scope change  

> The controlled workbook and full RTM contain rationale, mappings, acceptance criteria, verification methods and evidence paths.

| ID | Priority | Release Role | Lifecycle | Requirement |
|---|---|---|---|---|
| N-M01 | Must | Core | Active | The core workflow must work locally after required models and tools are installed. |
| N-M02 | Must | Core | Active | The application must not upload models, prompts, knowledge files or answers to a cloud AI service in the core workflow. |
| N-M03 | Must | Core | Active | The application window must remain responsive during long tasks. |
| N-M04 | Must | Core | Active | The application must show the current state of a long task. |
| N-M05 | Must | Core | Active | The application must handle model/document paths and process arguments safely. |
| N-M06 | Must | Core | Active | The application must clean temporary files and stopped child processes. |
| N-M07 | Must | Core | Active | Each final run must record requested and actual backend, device and optimisation state. |
| N-M08 | Must | Core | Active | A clean copy of the repository must build and run its automated tests. |
| N-M09 | Must | Core | Active | The repository must not contain secrets, personal test data or large proprietary model files. |
| N-M10 | Must | Core | Active | Experimental options must be clearly labelled in the interface. |
| N-M11 | Must | Core | Active | An experimental option must not be reported as active unless activation is proved. |
| N-M12 | Must | Core | Active | The main local inference route must not require a local web server or open network port. |
| N-M13 | Must | Core | Active | The main workflow must support keyboard operation and Windows text scaling. |
| N-M14 | Must | Core | Active | The project must produce a basic distributable Windows x64 release build with documented dependencies. |
| N-S02 | Should | Should | Active | The project should provide a simple packaged installation route such as MSIX. |
| N-S01 | Should | Superseded | Superseded | The main workflow should support keyboard use and Windows text scaling. |

Do not edit this category file independently of the controlled RTM workbook.
