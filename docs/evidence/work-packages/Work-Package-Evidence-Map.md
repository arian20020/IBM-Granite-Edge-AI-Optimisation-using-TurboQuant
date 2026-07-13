<!-- IXN-EVIDENCE-STRUCTURE: GENERATED -->
# Work-Package Evidence Map

> Work packages should point to the same controlled requirement/test/experiment artefacts rather than copying evidence into multiple folders.

| WP | Phase | Work package | Definition of Done | Expected evidence output | Status |
|---|---|---|---|---|---|
| PD-01 | P0 | Freeze first-release definition/RQs | One aim, measurable RQs, contribution and scope tiers are versioned. | Definition v1 | In Progress |
| PD-02 | P0 | Correct/version controlling workflows | Workflow distinguishes runtime KV-cache modes from exported weight files and separates runtime routes. | Workflow change log | Not Started |
| PD-03 | P0 | Recover/archive raw evidence and current UI code | MoSCoW, ModelImportPage and raw campaign evidence are backed up and referenced. | Evidence manifest/checksums | In Progress |
| PD-04 | P0 | Must-Have RTM and acceptance criteria | All Must Haves map to workflows, components, tests and evidence. | RTM | In Progress |
| PD-05 | P0 | Risk/assumption/constraint/licence register | High risks have validation, mitigation, contingency, owner and status. | Register | In Progress |
| PD-06 | P0 | Architecture views and ADRs | Context/component/process/deployment views and key decisions agree. | Diagrams + ADRs | Not Started |
| PD-07 | P0 | Application contracts/states/diagnostics | Stable data/state/error contracts support independent UI and service work. | Schemas/contracts | Not Started |
| PD-08 | P0 | Repository/test/evidence preparation | Clean build/test and evidence structure exist. | Build log/repo tree | In Progress |
| PD-09 | P0 | App-specific evaluation addendum | Remaining experiments map to RQs and frozen evidence schemas. | Evaluation matrix | In Progress |
| PD-10 | P0 | TurboVec and LLM Fit decision | Both items have explicit Implement/Defer decisions. | Scope ADR | In Progress |
| IM-01 | P1 | Finish existing ModelImportPage layout/navigation | Page launches and matches the core import workflow. | Screenshots/commit | In Progress |
| IM-02 | P1 | ViewModel + file/folder picker | Real file/folder selection produces a testable state object. | Tests/screenshots | Not Started |
| IM-03 | P1 | Drag/drop, validation, progress/cancel | Input edge cases and cancellation recover safely. | Failure tests | Not Started |
| IM-04 | P1 | Format detector and GGUF header validation | Valid/corrupt GGUF recognition is deterministic. | Fixtures/tests | Not Started |
| IM-05 | P1 | Real GGUF metadata inspector | Real Granite metadata populates ModelDescriptor. | Metadata JSON/model hash | Not Started |
| IM-06 | P1 | Other-format recognition and classification | OpenVINO/HF and canonical result states are safe and explicit. | Classification matrix | Not Started |
| IM-07 | P1 | Result screens, diagnostics and inspection demo | Import/inspection slice passes tests and demo. | Demo/test report | Not Started |
| HE-01 | P2 | Hardware snapshot service | RAM/CPU/OS/disk snapshot is serialisable and testable. | Hardware manifest | Not Started |
| HE-02 | P2 | Backend/device capability registry | Installed/supported/experimental routes are distinct. | Capability matrix | Not Started |
| HE-03 | P2 | Memory estimator core | Transparent component estimate with uncertainty and safety reserve. | Formula/tests | Not Started |
| HE-04 | P2 | Estimator calibration from existing evidence | Prediction error/margins are quantified for matched tested cases. | Dataset/plot/ADR | Not Started |
| HE-05 | P2 | Candidate generator | Only complete supported candidates survive. | Matrix/property tests | Not Started |
| HE-06 | P2 | Mode selector algorithms | Four deterministic objectives pass boundary tests. | Algorithm/test matrix | Not Started |
| HE-07 | P2 | Fit/not-fit compatibility UI | UI shows only verified configurations and transparent memory reasoning. | Screenshots/E2E | Not Started |
| HE-08 | P2 | Import-to-mode E2E stabilisation | Vertical slice passes twice and gate is approved. | E2E/demo | Not Started |
| RT-01 | P3 | Upstream llama.cpp adapter contract | Pinned executable/model command contract works independently. | Manifest/smoke log | Not Started |
| RT-02 | P3 | Secure single-turn inference | App returns one valid local response without a network port. | Logs/tests | Not Started |
| RT-03 | P3 | Streaming chat and cancellation | Streaming/stop/retry work and clean up resources. | Recording/tests | Not Started |
| RT-04 | P3 | Session/context/metrics | Second turn, bounded history and metrics are correct. | Metrics/context tests | Not Started |
| RT-05 | P3 | Primary runtime hardening | Primary GGUF route passes failure/offline E2E gate. | E2E report | Not Started |
| QX-01 | P4 | Standard GGUF weight quantisation | One suitable source produces a smaller validated GGUF safely. | Hashes/commands/results | Not Started |
| QX-02 | P4 | Reinspection, manifest and export | Output is registered/exported and original preserved. | Manifest/export test | Not Started |
| QX-03 | P4 | AtomicBot Experimental adapter | App proves real turbo3 activation and actual backend for a bounded configuration. | Activation/metrics evidence | Not Started |
| QX-04 | P4 | TurboQuant product gate/fallback | Experimental option is included only if evidence passes; upstream fallback works. | Gate ADR | Not Started |
| OV-01 | P5 | Official OpenVINO baseline | One official Granite IR CPU run passes or blocker is reproducible. | OpenVINO workbook/logs | Partially Verified |
| OV-02 | P5 | Official OpenVINO app adapter | One IR follows common app states with actual device evidence. | Contract/E2E evidence | Not Started |
| OV-03 | P5 | OpenVINO/custom-TQ decision gate | Final UI scope matches verified OpenVINO capability; custom TQ is honestly kept/deferred. | Gate ADR/workbook | Not Started |
| FR-01 | P6 | Full regression and requirements acceptance | Must Haves have pass evidence or explicit incomplete status. | Test reports/RTM | Not Started |
| FR-02 | P6 | Security/offline/UX/accessibility | No critical unresolved issue; residual issues are recorded. | Checklists/issues | Not Started |
| FR-03 | P6 | Final app-integrated evaluation | Frozen raw dataset and cross-route comparison are complete. | Raw data/figures | Not Started |
| FR-04 | P6 | Clean build/package/manuals/demo | Clean build/install and demo pass twice. | Package/manuals/video | Not Started |
| FR-05 | P6 | Release freeze/evidence audit | Tag, backups, final status and evidence pack complete. | Release pack | Not Started |
| DL-01 | P1 | Freeze approved model catalogue/source/licence/hash manifest | At least one model has approved source, licence, revision, expected size, SHA-256 and destination policy. | Approved model manifest | Not Started |
| DL-02 | P1 | Implement model download/progress/cancel/disk/hash service | Approved model downloads safely; partials cannot pass; progress, cancellation, disk and SHA-256 checks work. | Download tests/logs | Not Started |
| DL-03 | P1 | Download-to-inspection end-to-end and failure tests | Verified download enters inspection; interrupted/corrupt/low-disk cases are handled and evidenced. | E2E evidence | Not Started |
| TV-01 | P0 | Identify and pin exact TurboVec implementation and contract | Repository/version/licence/platform/input/output/vector/retrieval contract and go/no-go are recorded. | TurboVec ADR/contract | In Progress |
| TV-02 | P4 | Knowledge-file import, text extraction, chunking and embedding baseline | One supported file is preserved, extracted, chunked and embedded into a versioned uncompressed baseline. | Baseline artefacts/tests | Not Started |
| TV-03 | P4 | TurboVec vector compression/index/retrieval integration | Compressed/optimised vectors are produced, indexed and queried with exact versions and evidence. | TurboVec adapter/results | Not Started |
| TV-04 | P4 | Granite retrieval-chat integration and matched TurboVec evaluation | Retrieved sections feed Granite chat and compressed/uncompressed routes are compared on fixed queries. | EXP-TV-COMP-001 | Not Started |
