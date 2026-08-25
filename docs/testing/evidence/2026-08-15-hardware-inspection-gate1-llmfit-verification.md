# Hardware Inspection LLM Fit Gate 1 verification

This record was generated from bounded, strict input artifacts. It records the Gate 1 decision only; it is not production approval.

## Decision

| Field | Result |
|---|---|
| Branch | `integration/hardware-inspection-intel-completion-v1` |
| Evaluated-source commit | `8883f812c174b7268ce3db15803cd1930b63d16d` |
| Final disposition | **FunctionalPassWithPackagingConcern** |
| Decision code | None |
| Candidate decision made | Yes |
| Gate 1 behavioral gate | Satisfied for the scope stated here |

## Source artifact integrity

| Artifact | SHA-256 or availability |
|---|---|
| Trusted candidate evidence | `f4dab11e8ed4816ccf14f22349b889ee664be4dac685c5dc9ccd72ba1cc39441` |
| Windows reference evidence | `714d1c4dc4ddcc9ff9283769749d9949c12cb7e764432fcda5fd2ae554fbe29e` |
| Offline candidate evidence | `d32a43f7c63b77b50badf93aeb12380200934c9ac38aca875866a547d57c6bf3` |
| Deterministic TRX | `226d3540a6272e72528987c51ee96b31431c39dfbd38640fec495e342be4e083` |
| Trusted Windows TRX | `8010ec487e413340c3d5fc2f27dd58f1d39bdf7475ffdaceb081812422a08daa` |
| Offline TRX | `ec54eb57669de628734557946cd85f20a70e0a49dc9d7bb01b99a50690468117` |

- TRX freshness and execution against the evaluated-source commit were established operationally; the artifact hashes do not cryptographically prove freshness or source binding.

## Test outcomes

| Route | Result |
|---|---|
| Deterministic | 174 passed, 0 failed, 0 not run |
| Trusted Windows Intel | 3 passed, 0 failed, 0 not run |
| Controlled offline | 1 passed, 0 failed, 0 not run |

## Candidate identity and fixed execution boundary

| Field | Result |
|---|---|
| Candidate tag | `v1.1.9` |
| Candidate ID | `llmfit-v1.1.9-win-x64` |
| Upstream release commit | `a02e13f1013ed69889ff44426a651bf7c68c292e` |
| Expected archive SHA-256 | `a030269d7cc8a5bf40383f526a481655d698ec71dd792a25b06510cef9f8b738` |
| Observed archive SHA-256 | `a030269d7cc8a5bf40383f526a481655d698ec71dd792a25b06510cef9f8b738` |
| Expected executable SHA-256 | `db82bcb17f065b7ff7528ffe9904b2b0e1cce0c843fcd659b4ee1432a2e72e19` |
| Observed executable SHA-256 | `db82bcb17f065b7ff7528ffe9904b2b0e1cce0c843fcd659b4ee1432a2e72e19` |
| Reported version | `llmfit 1.1.9` |
| PE architecture | `AMD64` |
| License | MIT; the upstream `LICENSE` must be retained for any permitted redistribution |
| Version command | `--version` |
| System command | `--no-dashboard --json system` |
| Other commands | Prohibited: `serve`, dashboard, REST, model recommendation, and arbitrary arguments |

## Candidate observations

| Check | Result |
|---|---|
| JSON and required CPU/RAM schema | Yes |
| Windows Intel GPU dedicated/shared memory semantics | Not established; candidate VRAM must not be interpreted as either |
| Intel NPU detection | `DetectionUnavailable`; no presence or absence conclusion |
| Candidate-owned socket/listener observed | No |
| Dashboard port 8787 observed | No |
| Residual candidate process observed | No |
| Authenticode observation | `NotSigned`; upstream signing claim mismatch Yes |
| Transitive dependency-license inventory | Pending; production redistribution remains blocked |
| Schema-documentation drift | Yes |

## Windows comparison (derived values only)

| Comparison | Result |
|---|---|
| Named Windows Intel x64 target | Yes |
| Capture within 30 seconds | Yes; 10466 ms |
| CPU identity matched | Yes |
| Logical processors | Windows 12, candidate 12, matched Yes |
| Total RAM | delta 0 GiB, tolerance 1 GiB, passed Yes |
| Available RAM | delta 0.087 GiB, tolerance 2 GiB, passed Yes |
| Intel GPU identity | `Matched`; memory semantics remain unaccepted |

## Privacy result

- The report generator accepted only exact JSON property sets, fixed diagnostic enums, fixed test identities, bounded strict UTF-8/DTD-free XML, counts, comparison booleans/deltas, and SHA-256 values.
- Processor/GPU names, raw JSON, local paths, stdout, stderr, host/user names, serials, device IDs, and network addresses were not copied into this Markdown.

## Exact non-claims

- No production binary has been approved or committed.
- Gate 1 does not approve any WinUI or `HardwareSnapshot` implementation.
- No Windows Intel dedicated/shared GPU memory conclusion was established.
- No Intel NPU presence or absence was established.
- No model compatibility conclusion was made.
- No network-syscall proof is claimed beyond the controlled offline run and listener/process observations.
- This Gate 1 record does not verify `F-M07`, `HE-01`, or `HE-02`; complete Block 2 evidence remains for the controlled Gate 9 traceability workflow.
