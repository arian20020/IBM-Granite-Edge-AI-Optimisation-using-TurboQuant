# T1 Cross-Feature Coverage Map

| ID | Contract boundary | Planned executable evidence |
|---|---|---|
| T1-ING-01 | Picker/drop/download classification | GGUF and OpenVINO selection returns a route and leaf display name without a rooted path. |
| T1-MI-01 | Model Inspection v2 handoff | Independently assert the exact six-field canonical encoding, mutation rejection, claim, rollback and reissue lifecycle. |
| T1-HW-01 | Hardware-to-compatibility projection | Assert model identity and usable hardware facts bind while fresh available memory remains a separate execution-time input. |
| T1-COMP-01 | Compatibility outcomes | Cover current-fit, optimisation-required, no-fit and unknown evidence for both routes. |
| T1-PLAN-01 | Preference-to-plan resolution | Cover Automatic and every visible manual band, monotonic selection, capability admission and the exact v3 configuration digest. |
| T1-EXEC-01 | Execution binding | Reject changes to model, handoff, hardware snapshot, fresh memory/capability, executable identity and plan digest. |
| T1-OUT-01 | Persistent output identity | Register and reinspect successful output; require identical digest and length for Chat and export. |
| T1-OV-01 | Runtime-only OpenVINO output | Preserve exact plan configuration and reject downloadable-model representation. |
| T1-FAIL-01 | Failure publication | Cancel, timeout, crash, malformed/oversized output and cleanup failure publish no success or stale result. |
| T1-REC-01 | Restart/recovery | Retain only identity-bound allowed state and reject stale operations after restart. |
| T1-PARITY-01 | Route parity | Share product-level outcome semantics while retaining route-specific formats and configuration. |
| T1-PRIV-01 | Privacy canaries | Exclude paths, usernames, raw worker output, credentials and provider/model payloads from every cross-feature handoff. |
| T1-BOUND-01 | Canonical/property boundaries | Independently cover encodings, digests, integer/unit limits, preference edges, monotonicity and invalid combinations. |

Characterization tests may begin green only when the production behavior is already present; each such test is labeled in its source comment and audit report. Missing behavior receives a failing test against the frozen baseline and no production fix from T1.
