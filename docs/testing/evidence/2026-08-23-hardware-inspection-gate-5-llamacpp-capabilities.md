# Hardware Inspection llama.cpp capabilities Gate 5 development acceptance

This record closes the development-acceptance scope of Gate 5. It does not grant production or release approval.

## Decision

| Field | Result |
|---|---|
| Branch | `integration/hardware-inspection-intel-completion-v1` |
| Evaluated-source commit | `a9c27a59e487336a4a6b79099d35948015c34a4c` |
| Gate 5 implementation range | `a351a7d9..a9c27a59` |
| Decision | **Gate 5 development acceptance complete** |
| Next gate | Gate 6 evidence authority, normalization, consistency, freshness, resolution, provenance, and canonical snapshot |

## Disposable-guest acceptance

The final reviewed v10 runner was executed against a normally installed signed x64 test AppX through its registered AUMID. Three consecutive repetitions completed with the following invariant result:

| Check | Result |
|---|---|
| Repetitions | 3 |
| Per-repetition tests | 32 total, 32 passed, 0 failed |
| Package identity | Present in every repetition |
| Signature observation | `Developer` in every repetition |
| Published schema | `granite.hardware-inspection.development-acceptance/v1` |
| Classification | Development-only |
| Public trust verified | `false` |
| Smart App Control verified | `false` |
| Result framing | Strict UTF-8 without BOM; one terminal LF; no CR |

The canonical v10 result was 466 bytes with SHA-256 `B089ACA9FF3C3EB9810DB4B3C75461425B10A67B5368A42862B8273A64C4B37D`. The v10 transport ZIP had SHA-256 `7140B037F6014FDF9CE1EE608B01138CB550A7C23200E61E9BFE49863127F820`. Its manifest bound the 139,533,971-byte test MSIX to SHA-256 `0AF434B35943B9B1E3B4BC43BADC88AE30F11FC101BBF7E8CAD7DB45B5557E45` and the 30,375-byte guest runner to SHA-256 `C0E5DBA8AD2557E501FB6CA7137750402C7EB407A7CC6B99758E084BE9BF1151`. The bundle contained exactly the four expected files. The result, transport ZIP, certificate, and MSIX are evidence inputs and were not committed.

The v7 result has identical deterministic summary content but predates the final reviewed runner and fixture changes, so it is superseded rather than treated as final evidence. The v8 and v9 runs were diagnostic failures that exposed, respectively, numeric process-ID reuse risk after cleanup and a child-observation race on normal parent exit. The final source retains stable process identity and uses a bounded, test-only child-observation handshake; independent re-review found no remaining issue in those corrections.

## Final-head verification

| Route | Result |
|---|---|
| Foundation suite | 201/201 passed; 0 failed or skipped |
| Probe unit suite | 22/22 passed; 0 failed or skipped |
| Hardware/runner Python contracts | 63/63 passed in 107.199 seconds |
| Packaged Debug/x64 regression | 118/118 passed; 99 Hardware Inspection, 16 model-handoff, and 3 onboarding-navigation tests; 0 non-passing results |
| Disposable-guest signed AppX acceptance | 32/32 passed in each of 3 repetitions |
| Packaged Debug/x64 build | Passed with zero errors |
| App Release/x64 MSIX build | Passed with zero errors |

The authoritative packaged TRX is `TestResults/HardwareInspection/Gate5FinalPackaged114/gate5-packaged-114.trx`. Its historical directory name reflects the minimum expected count; discovery produced and passed 118 tests. Build warnings were limited to the already known `NETSDK1198` warning in the Release build and the known `NETSDK1198`, `CS8602`, and `MSTEST0044` warning families in the packaged Debug build.

## Boundary, packaging, and privacy audit

- Foundation has no LLamaSharp or native runtime package reference. Its llama.cpp hits are limited to pinned identity/parser constants and documentation.
- The provider and worker introduce no shell, arbitrary `Process.Start`, listener, network, model-inspection, GGUF, OpenVINO, or canonical `HardwareSnapshot` dependency.
- The Debug test MSIX contained one exact real probe executable under `HardwareInspection/LlamaCppProbe` and kept adverse fake tools inside the test package. The Release app MSIX contained the same single inactive real probe and no fake tool.
- The native LLamaSharp runtime remains under the isolated probe subtree. The app and test host do not load llama/ggml modules.
- The Gate 5 range contains no tracked certificate, private key, MSIX/AppX, TRX, or raw acceptance output. No host device label, username, private path, native log, model data, or arbitrary process output is recorded here.
- Production onboarding still composes `UnavailableHardwareInspectionService`; neither the probe nor a hardware coordinator is registered or launched in production.
- The disposable acceptance package and related process residue were absent after the campaign.
- Independent review of the final runner cleanup, stable process-identity changes, and bounded child-observation handshake reported no Critical, Important, or Minor findings.

## Exact non-claims

- Development acceptance passed in a disposable guest. Smart App Control and public-trust signing remain unverified.
- A developer signature is not a public-trust production signature.
- No production Hardware Inspection service, probe launch, or canonical snapshot is activated.
- Gate 5 does not resolve provider authority, unit normalization, tolerance, freshness, cross-provider consistency, provenance, primary-adapter selection, Intel identification, usable graphics memory, or model compatibility.
- Full Hardware Inspection completion remains gated by Gates 6 through 9.
