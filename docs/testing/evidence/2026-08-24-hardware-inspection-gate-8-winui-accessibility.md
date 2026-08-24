# Hardware Inspection WinUI and accessibility Gate 8 verification

This record verifies the Hardware Inspection WinUI journey, presentation lifecycle, responsive/accessibility contracts, Windows reduced-motion policy, and fixed packaged UI-thread acceptance. It does not grant release approval, public publisher trust, Smart App Control compatibility, LLM Fit redistribution, or supported Intel-machine end-to-end approval.

## Decision

| Field | Result |
|---|---|
| Branch | `integration/hardware-inspection-intel-completion-v1` |
| Evaluated code head | `1ba28f33` |
| Gate 8 range | `a1ca691d..1ba28f33` |
| Scope | Hardware-owned Windows motion policy, presentation pacing, page/control contracts, one-run lifecycle, onboarding navigation, accessibility, responsive behavior, fixed UI inventory, and UI-thread packaged execution |
| Decision | **Gate 8 development verification passed; independent-review requirement remains an explicit inline-execution exception** |
| Next gate | Gate 9 supported Intel-machine, offline/no-port, public signing/trust, final Block 2 traceability, and release evidence |

## Exact-head verification

| Route | Result |
|---|---|
| Foundation suite | 201/201 passed; 0 failed or skipped |
| Probe unit suite | 22/22 passed; 0 failed or skipped |
| Hardware/runner Python contracts | 63/63 passed; 0 failed or skipped; PowerShell execution-policy bypass was process-scoped |
| Release/x64 application MSIX build | Passed with zero errors; known `NETSDK1198` missing publish-profile warning only |
| Debug/x64 packaged test build | Passed with zero errors; known `NETSDK1198`, two existing Debug-fixture `CS8602` sites, and existing `MSTEST0044` sites only |
| x86 evaluated graph | 142 Compile items evaluated; zero Hardware Inspection infrastructure Compile items and zero Hardware Inspection Foundation project references |
| Gate 8 fixed inventory | 87 unique safe parameterless tests: 32 process-boundary plus 55 Gate 8 presentation/lifecycle/onboarding/accessibility tests; maximum remains 128 |
| v13 disposable-guest acceptance | Three normally installed registered-AUMID repetitions; 87/87 passed in each, package identity present, `Developer` signature, zero failures |

The stage pacer reads `UISettings.AnimationsEnabled` for every newly visible stage. Normal motion retains the exact 500 ms presentation duration. Reduced motion and recoverable setting-read failures skip only presentation delay; they do not delay, cancel, recollect, or change hardware evidence. Caller cancellation is returned as a cancelled task, and critical runtime failures are not collapsed.

The acceptance app creates and activates one WinUI test window, publishes its dispatcher queue, and invokes the fixed inventory sequentially on that UI thread. The inventory covers the exact seven-stage and terminal presentations, details/progress/outcome controls, 44 px actions, focus and automation contracts, collapsed-content accessibility, responsive layout, page actions, ViewModel/journey lifecycle, onboarding handoff/navigation, and reduced motion while retaining the 32 process-boundary tests.

## Disposable-guest artifact and result

| Item | SHA-256 / result |
|---|---|
| v13 transfer ZIP | `4939C7BCEA58CE3AD702A4855D51602B9FAC0BD7B82C5BE2C3F5C4413126A742` |
| Canonical returned summary | `2F256A0003A7DE1A79532FA945B38A1587A0B0BB99F24E7BFCD46A615D2BF7D1` |
| Summary framing | 466 bytes; strict UTF-8; no BOM; exactly one LF; no CR |
| Summary schema | `granite.hardware-inspection.development-acceptance/v1` |
| Classification | `development-only` |
| Repetitions | Ordered runs 1, 2, 3; each package identity present, 87 total, 87 passed, `Developer` signature |
| Failures | Empty |
| Public trust / Smart App Control | Both explicitly `false` |

The generated four-file bundle, certificate, signed development MSIX, raw per-run results, and returned summary are not tracked. The exported certificate contains no private key. The self-signed development identity proves only this hash-bound disposable-guest route and does not establish public publisher trust or Smart App Control compatibility.

## Boundary and review result

The exact range contains no project/package dependency change, provider or orchestration change, download/URL, override root, certificate, key, binary, package, TRX, raw evidence, host label, username, private path, model datum, shell/network surface, arbitrary exception message, or compatibility/fit logic. The evaluated x86 graph retains zero Hardware Inspection infrastructure sources and zero Foundation reference. Whitespace, conflict-marker, artifact, privacy, dependency, network/shell, model-coupling, and raw-output scans passed.

Inline review covered current-setting reads, reduced-motion failure policy, cancellation semantics, 500 ms normal pacing, critical exception preservation, UI-thread ownership, deterministic inventory, test signature/name bounds, package identity, atomic bounded results, action/focus/automation contracts, responsive layout, collapsed traversal, onboarding ownership, and process-exit cleanup. No Critical, Important, or Minor finding remains. Because the user selected inline execution and no independent reviewer performed this range review, the result is deliberately not represented as independent review.

## Exact non-claims

- Gate 8 verifies the development WinUI/accessibility route, not final release approval.
- The self-signed `Developer` package does not establish public publisher trust or Smart App Control compatibility.
- Gate 8 does not approve or redistribute LLM Fit and does not change its `FunctionalPassWithPackagingConcern` status.
- No model compatibility or fit conclusion is produced.
- Gate 9 supported Intel-machine, controlled offline/no-port operation, public signing/trust, final Block 2 traceability, and release evidence remain incomplete.

No TRX, package, certificate, raw result, host label, username, or machine path is committed with this record.
