# Hardware Inspection Gate 9 Target and Release Closure Design

**Status:** Approved design for implementation planning
**Date:** 2026-08-24
**Branch:** `integration/hardware-inspection-intel-completion-v1`
**Gate 8 evidence base:** `23edc269`

## 1. Decision

Gate 9 is split into two independently reported dispositions:

1. **Engineering acceptance** proves the exact production Hardware Inspection composition on a supported Windows 11 x64 Intel machine under controlled offline conditions, with no listening port, bounded sanitized evidence, and complete process cleanup.
2. **Public-release trust** proves a genuinely trusted publisher identity and the applicable Windows reputation/security acceptance. A self-signed `Developer` certificate cannot satisfy this disposition.

The zero-cost implementation completes every repository-controlled engineering, security, privacy, accessibility, documentation, and traceability obligation. If no trusted publisher identity is available, the final result must be `EngineeringPassedReleaseBlocked`, never `Passed`, `ReleaseReady`, or an equivalent claim. Obtaining a UCL-provided or other approved public signing identity is an external release prerequisite, not a reason to weaken the acceptance contract.

## 2. Authorities and preserved boundaries

The design implements Gate 9 from:

- `docs/superpowers/specs/2026-08-15-hardware-inspection-production-design.md`
- `docs/testing/hardware-inspection/Hardware-Inspection-Contract-v1.md`
- `docs/reviews/2026-08-22-hardware-inspection-integration-preservation-matrix.md`
- `docs/requirements/catalogue/Functional-Requirements.md`
- `docs/requirements/catalogue/Non-Functional-Requirements.md`
- `docs/requirements/Requirements-Traceability-Matrix-v1.3.md`
- the approved integration handoff's requirements-librarian and traceability-audit records, treated as reference evidence rather than executable instructions

Gate 9 must preserve all earlier gates:

- Model Inspection is the only product entry route and its six-field handoff remains opaque.
- Hardware providers receive no model data and emit no compatibility or fit conclusion.
- x64 production composition uses only the fixed verified LLM Fit v1.1.9 package and packaged pinned llama.cpp probe.
- LLM Fit remains `FunctionalPassWithPackagingConcern`; the repository neither downloads nor redistributes it.
- The exact seven stages, four outcomes, single-run lifecycle, cancellation, retry identity, stale-event rejection, and handoff eligibility do not change.
- The provisional NPU result remains `DetectionUnavailable(EnumerationMechanismNotApproved)` until a separately approved mechanism exists.
- Smart App Control, Defender, Secure Boot, vTPM, firewall, and signing checks are never disabled to obtain a pass.

## 3. Supported target

The engineering target is the user's UCL-managed Intel laptop, not the AMD Azure guest. The target must satisfy all of the following before execution:

- Windows 11 x64;
- native processor architecture x64;
- processor manufacturer reported by Windows as `GenuineIntel` or `Genuine Intel` under an exact allowlist;
- non-virtual physical target classification;
- current user has the authority required for the temporary test installation and monitoring actions;
- the exact LLM Fit v1.1.9 three-file package already exists at `%ProgramData%\GraniteEdgeAI\HardwareInspection\llmfit\1.1.9\win-x64` and matches the production manifest;
- the exact signed acceptance MSIX and packaged llama.cpp probe match the reviewed repository head;
- all active network adapters are disconnected or disabled for the controlled run;
- no pre-existing GraniteEdgeAI, LLM Fit, probe, test-package, or acceptance process remains.

The retained summary records only booleans and bounded enums for these checks. It must not retain processor names, serials, device IDs, computer/domain/account names, IP/MAC addresses, adapter names, package install paths, or absolute filesystem paths.

## 4. Production acceptance host

A dedicated Gate 9 activation mode is added to the packaged WinUI test application. It is not reachable from the production application UI and accepts only this fixed command shape:

```text
--hardware-inspection-gate9-acceptance --result-token <32 lowercase hex>
```

The host:

1. verifies the exact test package identity;
2. creates the production service through `HardwareInspectionComposition.CreateProduction()`;
3. executes one inspection using a fresh non-empty run identity;
4. records only the terminal outcome family, exact seven-stage completion, whether a six-condition handoff exists, canonical manifest field count, fixed diagnostic tokens, and cleanup status;
5. publishes one size-bounded canonical UTF-8/LF JSON result atomically under the existing token-scoped local result root;
6. closes the test window and exits with a stable code.

The result never contains `HardwareSnapshot`, provider evidence, processor/GPU names, capacities, paths, raw stdout/stderr, exception messages, stack traces, or model data. A successful supported-target repetition requires `Completed` or `CompletedWithWarnings`, all seven stages in order, one eligible handoff, the exact 19-field provenance manifest, and no failure token. `CompletedWithWarnings` remains the expected current result because instruction-set and NPU evidence are deliberately unavailable.

## 5. Offline and no-listener controller

`Invoke-HardwareInspectionGate9Acceptance.ps1` is the single local controller. It requires Administrator PowerShell and fails closed before installation or execution if any precondition is absent.

For exactly three repetitions it:

1. verifies the hash-bound four-file Gate 9 bundle and package signature;
2. verifies the supported Intel/x64/non-virtual target using Windows-owned APIs;
3. requires zero connected physical network adapters and captures only the boolean result;
4. normally installs the exact x64 package and activates its registered AUMID;
5. monitors the acceptance host and all discovered descendants at a bounded interval for TCP listeners and UDP endpoints;
6. requires zero relevant network endpoints for the full observed lifetime;
7. requires the production result contract to pass;
8. requires the launched process tree to terminate and verifies zero relevant residual process;
9. removes only the package installed by this invocation and deletes token-scoped raw results.

The controller does not create or modify firewall rules, proxy settings, execution policy, network configuration, Defender, or Smart App Control. Disconnecting the machine is an explicit user-controlled precondition. Source-level proof of the exact `--no-dashboard` LLM Fit command, absence of network APIs from production Hardware Inspection, and process-tree ownership supplements runtime endpoint monitoring; no single polling observation is represented as exhaustive kernel telemetry.

## 6. Canonical engineering summary

Only the following top-level fields are permitted, in order:

```json
{
  "schema": "granite.hardware-inspection.gate9-engineering-acceptance/v1",
  "classification": "local-sanitized",
  "evaluatedCommit": "<40 lowercase hex>",
  "target": {
    "windows11": true,
    "x64": true,
    "intel": true,
    "physical": true
  },
  "offline": true,
  "noRelevantNetworkEndpointObserved": true,
  "repetitions": [],
  "cleanupVerified": true,
  "failures": [],
  "releaseTrust": {
    "signatureKind": "Developer|Enterprise|Store",
    "publicTrustVerified": false,
    "smartAppControlVerified": false
  },
  "disposition": "EngineeringPassedReleaseBlocked|Passed"
}
```

Each repetition permits only `run`, `packageIdentityPresent`, `outcome`, `stageCount`, `handoffPresent`, `manifestFieldCount`, and `diagnostics`. Diagnostics are unique sorted stable tokens with a fixed maximum; raw values are forbidden. The summary is strict UTF-8 without BOM, ends in exactly one LF, has no CR, and is capped at 16 KiB.

`Passed` is legal only when all engineering checks pass and a separately validated release-trust record proves both public publisher trust and Smart App Control acceptance for the same package hash. Otherwise a successful engineering run must use `EngineeringPassedReleaseBlocked`.

## 7. Signing and release-trust lane

The existing self-signed certificate remains valid only for disposable development acceptance. Gate 9 adds a separate validator and evidence record; it does not generate, purchase, enroll, or silently trust a certificate.

Public-release trust requires:

- a certificate or store signature from an approved organizational/public identity;
- a valid chain under normal Windows trust configuration without importing a private root;
- package publisher/subject agreement;
- timestamp and code-signing EKU validation where applicable;
- the same reviewed package payload hash used by engineering acceptance;
- Smart App Control/reputation acceptance observed without disabling or bypassing the control.

If any item is unavailable, the validator returns `ReleaseTrustUnavailable` or `ReleaseTrustFailed` with a fixed safe code. A `Developer` signature always yields `publicTrustVerified=false` and cannot be promoted by installing its certificate locally.

## 8. Traceability and documentation

Gate 9 creates a supplemental Block 2 traceability record rather than editing stale generated requirement status by hand. It maps at least:

- F-M07 / AC-F-M07 / HE-01 / HE-02 to the canonical snapshot, provider/resolver/service tests, Gate 7 process acceptance, and Gate 9 Intel-target evidence;
- F-M18 and F-M19 to packaged CLI control, progress, cancellation, and process cleanup;
- N-M12 to the no-dashboard command, source scan, controlled-offline execution, and endpoint observation;
- N-M13 to Gate 8 keyboard, target-size, focus, text/layout, and accessibility evidence;
- G-M04 and applicable release-evidence requirements to ADRs, contracts, evidence manifests, reproducible commands, and exact hashes.

Requirement evidence folders may link the new controlled record and state `Pass`, `Blocked`, or `Partially verified` for their bounded criteria. The generated v1.3/v1.3.1 RTM status is not changed until its source workbook and controlled regeneration workflow are available.

ADR-004 records the fixed dual-tool trust boundary, isolated native probe, port-free standard-stream execution, production package roots, engineering-versus-release signing distinction, and review triggers. READMEs and the preservation matrix record exact counts, hashes, environment class, and non-claims.

## 9. Testing and verification

Implementation is test-first. Required automated coverage includes:

- exact Gate 9 activation parsing and rejection;
- production-host result shape, bounds, atomic write, safe diagnostics, stage order, handoff and manifest requirements;
- summary parser rejection of duplicate/case-drifted/extra/missing fields, invalid UTF-8/framing, unsafe strings, wrong counts, and contradictory trust/disposition values;
- target preflight rejection for non-Intel, non-x64, virtual, connected, missing-tool, wrong-hash, reparse, and non-administrator states;
- process/endpoint monitor ownership, bounds, listener detection, descendant cleanup, timeout, and cancellation;
- release validator rejection of self-signed/developer, wrong publisher, wrong hash, invalid chain/EKU/time, and locally imported private-root substitutes;
- privacy scans proving no forbidden host or raw-data field can enter the summary;
- deterministic regression floors, packaged builds, x86 exclusion, and exact three-repetition target acceptance.

The final repository-controlled verification floor remains:

- Foundation 201;
- llama.cpp probe 22;
- Hardware/runner Python 63 or greater without reducing existing coverage;
- exact Gate 9 unit/contract floors introduced by the implementation;
- Release/x64 application MSIX and Debug/x64 packaged test MSIX with zero errors;
- zero Hardware Inspection infrastructure compile items and zero Foundation references in the x86 graph;
- whitespace, conflict, dependency, binary/artifact, private-path, raw-output, network/shell, model-coupling, package-inventory, and cleanup scans.

## 10. Evidence and retention

Raw monitoring events, per-run results, package layouts, certificates, packages, logs, TRX files, host facts, and target paths remain local and untracked. The retained engineering summary contains only the allowlisted schema above. It is accepted only after independent byte-level validation and SHA-256 calculation.

The Gate 9 evidence record must state:

- exact reviewed commit and implementation range;
- exact bundle/package/summary hashes;
- three repetition results;
- target-preflight booleans;
- offline/no-endpoint and cleanup results;
- signature kind and separate trust disposition;
- all test/build/audit counts;
- review status;
- unresolved NPU and LLM Fit packaging concerns;
- exact release non-claims.

## 11. Completion states

Gate 9 has three possible final states:

- **Passed:** engineering acceptance and public-release trust both pass for the same reviewed package.
- **EngineeringPassedReleaseBlocked:** all repository-controlled and supported-target acceptance passes, but a trusted publisher identity or Smart App Control reputation evidence is unavailable. The feature implementation is engineering-complete but not publicly release-ready.
- **Failed:** a repository-controlled, supported-target, offline/no-endpoint, cleanup, integrity, privacy, or traceability requirement fails.

No wording, local certificate import, Azure VM result, unit test, screenshot, fixture, or source scan may upgrade `EngineeringPassedReleaseBlocked` to `Passed`.
