# Hardware Inspection LLM Fit Gate 1 verification

This record was generated from bounded, strict input artifacts. It records the Gate 1 decision only; it is not production approval.

## Decision

| Field | Result |
|---|---|
| Branch | `feature/hardware-inspection` |
| Evaluated-source commit | `aae1d347c656873a0e764a9a011f442a7b2dd1fc` |
| Final disposition | **Blocked** |
| Decision code | HI-GATE1-OFFLINE-PRECONDITION-FAILED, HI-GATE1-WRONG-TARGET |
| Candidate decision made | No |
| Gate 1 satisfied | No; Gate 2 must not start |

## Source artifact integrity

| Artifact | SHA-256 or availability |
|---|---|
| Trusted candidate evidence | Unavailable |
| Windows reference evidence | Unavailable |
| Offline candidate evidence | `33f0d93e7d198b209b2451ecbf65cb7baabd16751b1e54821424f3f76bc6ec17` |
| Deterministic TRX | `d0c06ab48f59b4bcc66bea064ce87590bb454e9194fa64ade40d2a4c9661aa40` |
| Trusted Windows TRX | `6340dcbc9236fad7503bdd7cd8a29e7e7ab22e9f42db5ef49e998176c31ff6aa` |
| Offline TRX | `e2b830f0f88b257a3730b8e322ab13a1fe058fa3cd4cbf15ed6ac6595132a2e8` |

- TRX freshness and execution against the evaluated-source commit were established operationally; the artifact hashes do not cryptographically prove freshness or source binding.

## Test outcomes

| Route | Result |
|---|---|
| Deterministic | 174 passed, 0 failed, 0 not run |
| Trusted Windows Intel | 0 passed, 0 failed, 3 not run |
| Controlled offline | 0 passed, 1 failed, 0 not run |

## Blocking prerequisites

- The named Windows Intel trusted-target prerequisites were unavailable or the configured target was rejected before candidate evaluation.
- The controlled offline prerequisite was unavailable, so offline behavior was not accepted.
- Trusted candidate evidence | Unavailable
- Windows reference evidence | Unavailable
- Offline prerequisite evidence and TRX have no intrinsic run-binding field; their same-run creation was controlled operationally, and their separate hashes do not independently prove pairing or freshness.

## Privacy result

- The report generator accepted only exact JSON property sets, fixed diagnostic enums, fixed test identities, bounded strict UTF-8/DTD-free XML, counts, comparison booleans/deltas, and SHA-256 values.
- Processor/GPU names, raw JSON, local paths, stdout, stderr, host/user names, serials, device IDs, and network addresses were not copied into this Markdown.

## Exact non-claims

- No production binary has been approved or committed.
- No WinUI or `HardwareSnapshot` implementation exists.
- No Windows Intel dedicated/shared GPU memory conclusion was established.
- No Intel NPU presence or absence was established.
- No model compatibility conclusion was made.
- No offline candidate run occurred; no candidate network behavior was evaluated.
- This Gate 1 record does not verify `F-M07`, `HE-01`, or `HE-02`; complete Block 2 evidence remains for the controlled Gate 9 traceability workflow.
