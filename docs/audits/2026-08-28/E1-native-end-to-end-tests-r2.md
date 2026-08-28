# E1 R2 executable packaged end-to-end and native acceptance

## Status

- Worker: E1
- Disposition: deterministic R2 implementation passed; integrated-candidate native lane blocked
- Frozen commit/tree: `4748fe04f19afdf6b27c4c12502b84db325e7294` / `fe1fa8fb5fe4de8e7c1d867a83e08375bc1d0c91`
- Previous C0 tip/tree: `a5ef3558334e50587889140dafba194853938765` / `90c34ab009b744d7b00866fb93e8dbc86363f1b2`
- Provisional branch: `test/ucl-e1-native-acceptance-r2`
- Evidence-subject implementation commit/tree: `079058d70c22e37b86c547186890b0456139116a` / `b48b283b5d1bdb28b2fe594899f886393fcd903a`
- Report encoding/line endings: UTF-8 / LF in Git

## Executive result

E1 R2 now has a fail-closed integration-candidate contract, cryptographically joined predecessor handoff/evidence/native-receipt verification, exact stage/TRX accounting, stricter JSON and package identity checks, retained process cleanup verification, safer file-dialog scoping, and a deterministic one-to-one catalogue for all 20 required journey rows.

No package or native acceptance was run. C0 has not published a new integration candidate beyond `a5ef3558...`; only A1, T1 and the old E1 handoff receipts exist; only the old E1 native receipt exists; the pinned SDK 10.0.301 installation lacks `MSBuild.dll`; and no exact R2 package/candidate manifest or authorized campaign assets were supplied. The old C0 tip is rejected before candidate resolution or build. No lock, app, worker, model, conversion, optimization, download or Chat process was started.

## Scope and changes

E1 changed only `tests/E2ETests/**` plus this report. No production, shared solution, signing, package, trust, firewall, model, worker or feature behavior was changed.

Implementation commit `079058d7` adds:

- explicit immutable C0 commit/tree inputs and rejection of the previous C0 tip;
- frozen/previous-C0 ancestry, E1-descendant and E1-only-delta checks;
- installed executable hash/bytes/package-family/application-ID/root binding;
- exact A1/H1/M1/Q1/F1/S1/T1 inclusion checks by ancestry or patch equivalence;
- strict H1/M1/Q1/F1 report, handoff, evidence manifest, subject, command arithmetic, native closure and cleanup joins;
- duplicate JSON property rejection and closed manifest size/reparse policy;
- 67-test inventory with 29 deterministic tests, one predecessor preflight, and 37 guarded native tests;
- unique List/Smoke/Failure/Acceptance/Restart/RealModel/All accounting and TRX hashes;
- a unique-dialog file picker boundary instead of desktop-global button lookup;
- cleanup failure when an owned process survives the kill deadline;
- exact 20-row R2 journey catalogue.

## Required journey dispositions

| # | Journey | R2 implementation disposition | Native result |
|---:|---|---|---|
| 1 | Package launch and exactly one onboarding shell | executable UIA count assertion | not run: dependency gate |
| 2 | No onboarding footer/indicator on Chat | executable negative UIA assertion | not run: dependency gate |
| 3 | Picker and Explorer drag/drop ingress | picker executable; drag/drop precise product-seam guard | not run |
| 4 | GGUF/OpenVINO direct Chat when fit | route-specific executable journeys | not run |
| 5 | Optional optimization when fit | exact scenario guard plus optimization actions | not run |
| 6 | Required optimization for admitted alternative | exact scenario guard plus optimization actions | not run |
| 7 | No safe configuration/no execution | executable disabled/UIA-focus assertions | not run |
| 8 | GGUF optimize/reinspect/Chat/exact export | actions encoded; output digest/export seam guarded | not run |
| 9 | OpenVINO persistent conversion/reinspect/Chat/export | actions encoded; output digest/export seam guarded | not run |
| 10 | OpenVINO runtime-only Chat/export prohibited | explicit asset/identity guard | not run |
| 11 | Recommended download success/cancel/integrity/retry/cleanup | no-network authorized-source and fault-seam guard | not run |
| 12 | Changed model/hardware rejection | explicit mutation-seam guard | not run |
| 13 | Optimization cancel/restart/stale rejection | cancellation/restart actions plus stale-identity guard | not run |
| 14 | Duplicate/late publication rejection | explicit worker-publication seam guard | not run |
| 15 | Publication failure preserves original | explicit publication-failure seam guard | not run |
| 16 | Full Chat lifecycle | composer surfaces encoded; cross-reload turn identity guarded | not run |
| 17 | Restart recovery/stale identity | dedicated `NativeRestart` guard | not run |
| 18 | Malformed/import cancellation recovery | two executable smoke cases plus combined campaign guard | not run |
| 19 | Disabled keyboard/automation actions | absent-or-disabled and non-focusable assertion | not run |
| 20 | No orphan processes | per-test retained process tree plus runner slot cleanup | not run |

A guard skip is not counted as a pass. Product seams remain open requirements where UIA cannot observe or safely induce the required identity/failure boundary.

## Verification ledger

| Stage/command | Discovery | Executed | Passed | Failed | Skipped/blocked | Disposition |
|---|---:|---:|---:|---:|---:|---|
| Diagnostic complete-SDK build and Visual Studio VSTest x64 `/ListTests` | 67 | 0 | 0 | 0 | 0 | non-zero diagnostic discovery |
| Deterministic via Visual Studio VSTest x64 and one-worker settings | 29 | 29 | 29 | 0 | 0 | passed |
| Smoke guard audit via diagnostic `dotnet test` | 7 | 7 | 0 | 0 | 7 | skipped by declared guard |
| Failure guard audit via diagnostic `dotnet test` | 8 | 8 | 0 | 0 | 8 | skipped by declared guard |
| Acceptance guard audit via diagnostic `dotnet test` | 20 | 20 | 0 | 0 | 20 | skipped by declared guard |
| Restart guard audit via diagnostic `dotnet test` | 1 | 1 | 0 | 0 | 1 | skipped by declared guard |
| Real-model/download guard audit via diagnostic `dotnet test` | 1 | 1 | 0 | 0 | 1 | skipped by declared guard |
| H1/M1/Q1/F1 exact preflight | 1 | 0 | 0 | 0 | 1 | not run: inputs absent |
| Authoritative packaged List | 0 | 0 | 0 | 0 | all | not run: no new C0 candidate/pinned SDK/package |
| Authoritative Smoke/Failure/Acceptance/Restart/RealModel | 0 | 0 | 0 | 0 | all | not run: dependency gate |

The ignored deterministic TRX is 40,714 bytes with SHA-256 `45c8d3da058bb8ed6e911a34dd183956d7b63c3c3c334590995f0f97b7f3806a`. It is diagnostic because the project was built using the available complete SDK 10.0.400 from a neutral directory; the repository-pinned 10.0.301 SDK is incomplete. VSTest 17.14.0 x64 and the one-worker runsettings were used. PowerShell parsing and `git diff --check` passed.

## Blockers

### E1-R2-B01 — Critical — exact integrated C0 candidate absent

Remote and local C0 inspection found only the previous tip `a5ef3558...`. No newer candidate commit/tree or C0 candidate handoff was published. The R2 runner rejects that previous tip before resolving the package manifest. Owner: C0.

### E1-R2-B02 — Critical — predecessor closure records absent

The handoff directory contains A1, T1 and old E1 only. H1, M1, Q1, F1 and S1 handoffs are absent; H1/M1/Q1/F1 native receipts are absent; sanitized H1/M1/Q1 evidence manifests are unavailable. Therefore evidence-subject, hash/byte, arithmetic and cleanup joins cannot run. Owners: H1/M1/Q1/F1/S1/C0.

### E1-R2-B03 — Critical — pinned SDK incomplete

`global.json` requires SDK 10.0.301. Its installed directory has no `MSBuild.dll`; SDK resolution reports no compatible SDK. SDK 10.0.400 supports diagnostics but is not a substitute for the pinned authoritative build. Visual Studio `vswhere.exe` and VSTest are now available, so test-platform discovery itself is no longer the blocker. Owner: UCL environment administration.

### E1-R2-B04 — Critical — exact package and authorized campaign inputs absent

No candidate manifest bound to a new C0 identity, installed exact R2 package, path-free asset manifest/local asset mapping, producer manifests, or authorized download campaign manifest was supplied. E1 did not download or substitute anything. Owner: C0/UCL operator.

### E1-R2-P01 — Important — required product automation/identity seams absent

The frozen accessibility surface cannot safely induce or observe Explorer drag/drop, publication failure, late/duplicate worker results, mid-journey evidence mutation, persistent/runtime output digest identity, cross-reload Chat turn identity or download integrity failure. Those rows are precise inconclusive guards rather than false passes. Owner: C0 and feature owners.

## Evidence, privacy and cleanup

Raw TRX stays below ignored `TestResults/`. No screenshots or UIA trees were created because no candidate was launched. No committed output contains a username, hostname, absolute worktree/model path, model weight, package install path or machine identity. Evidence manifests remain external sanitized inputs and E1's final `evidenceManifest` is `null`.

`C:\UCL-AUDIT-NATIVE.lock` was never acquired and is absent. Since E1 started no native candidate/process, cleanup is verified for E1's activity. The old `C:\UCL-AUDIT-HANDOFFS\E1.json` and old E1 native receipt are intentionally not replaced: this provisional branch is not the final new-C0 acceptance handoff.

## Required continuation

C0 must publish the exact new integration candidate and specialist closure records. E1 must then create or rebase a collision-safe final R2 branch directly from that candidate, replay only commit `079058d7`, verify all records and installed inputs, run Deterministic then authoritative List/Smoke/Failure/Acceptance/Restart/RealModel under the native lock, verify zero descendants, commit the final measured report, push only E1, and only then atomically replace `E1.json` and publish the new native receipt.

This report does not claim authoritative packaged discovery, native launch, model inspection, hardware inspection, compatibility, optimization, conversion, export, download, Chat, restart, signing/trust, real-model correctness, or combined-candidate acceptance.
