# E1 R4 independent exact-candidate acceptance

## Disposition

`CHANGES REQUIRED`

The exact candidate cannot be approved because the coordinator's committed issue-closure JSON does not contain `R3-001` through `R3-022`. The controlling E1 gate requires those 22 identifiers exactly once with positive executable evidence. The document contains zero of them and instead contains ten `T1-*` and eight `R4-C0-*` rows. This is a candidate-evidence defect, not an external-environment block, and E1 made no product correction.

## Exact binding and dispatch gate

- Issued base ref: `refs/remotes/origin/integration/ucl-r4-e1-issued-base-v1`
- Tested candidate commit/tree: `429298d3328a62d4fcf2f5f3f12821342be4a23c` / `2e92e172679d1558972846694306d28ad56e2cf4`
- Coordinator documentation ref at dispatch: `refs/remotes/origin/integration/ucl-r4-specialist-reconciliation-v1`
- Coordinator documentation commit/tree: `c6028db9d785a4edd0e10956af3bf57586d95ac1` / `102f2d917f7330db2ef6cfdf496daa7fb276a73e`
- E1 implementation/evidence subject: `e340457ba4cb932cdb58b54a374176e51ca52a28` / `5a9d15530f3083b910610582c0e2a76cfadc18b7`
- Return branch: `test/ucl-e1-native-acceptance-r4`

After fetch, both remote refs resolved to the advertised commits. The issued-base tree matched exactly. The coordinator register marked H1 and Q1 `ACCEPTED`, E1 `DISPATCHED`, and bound E1 to the same issued-base commit/tree. E1 used a fresh short-path worktree created from the immutable issued-base ref, not the documentation commit. Five replayed commits change only `tests/E2ETests/**` and add the prior E1 exact-candidate/evidence veto infrastructure; no production path changed.

## Release-blocking finding

### E1-R4-001 — required R3 closure set absent

- Severity: Critical
- Owner: C0 evidence/closure publication
- Subject: `docs/audits/2026-08-30/evidence/C0-r4-issue-closure.json` at coordinator commit `c6028db9d785a4edd0e10956af3bf57586d95ac1`
- Blob: `32194598e6afa409a6ad950193e8e4de4e74ab62`, 2540 bytes; coordinator-declared SHA-256 `ad28d3e3434839f33589ffd18ec1e142058c8470f5bf448d7c699260f3aedc3e`
- Reproduction: parse `closures[*].id` and count each literal `R3-001` through `R3-022`.
- Expected: every required R3 identifier occurs exactly once and carries positive executable evidence appropriate to its layer.
- Actual: every required R3 identifier occurs zero times. The file cannot prove closure of the 22 inherited release issues.
- Consequence: the exact-candidate preflight is not eligible to proceed to package construction or native lock acquisition. Under the controlling prompt, any one missing row forces `CHANGES REQUIRED`.

## Independent execution evidence

The repository-pinned SDK `10.0.301` was unavailable. Managed verification temporarily selected installed SDK `10.0.400`; `global.json` was restored byte-for-byte before handoff. Visual Studio VSTest 17.14 x64 was used for authoritative E1 discovery and execution because the E1 projects are VSTest-based while repository `dotnet test` selects Microsoft Testing Platform.

| Command/gate | Discovered | Executed | Passed | Failed | Skipped | Result |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| E1 assembly list | 50 | 0 | 0 | 0 | 0 | non-zero discovery |
| E1 deterministic, one worker | 31 | 31 | 31 | 0 | 0 | passed |
| OpenVINO optimization | 55 | 55 | 55 | 0 | 0 | passed |
| OpenVINO worker client | 17 | 17 | 17 | 0 | 0 | passed |
| GGUF quantization contracts | 7 | 7 | 7 | 0 | 0 | passed |
| GGUF quantization worker client | 19 | 19 | 19 | 0 | 0 | passed |
| Q1 identity/lifecycle | 51 | 51 | 51 | 0 | 0 | passed |
| Cross-feature integration | 115 | 115 | 115 | 0 | 0 | passed |
| OpenVINO broad supplementary suite | 496 | 496 | 489 | 0 | 7 | mixed; native/package-gated cases skipped |
| Security audit | 12 | 12 | 11 | 1 | 0 | failed: App Control `0x800711C7` blocked hostile fixture |
| Native/package campaigns | 0 | 0 | 0 | 0 | 0 | not run; candidate-controlled preflight failed before lock |

The non-overlapping passing receipt total is 295 discovered/executed/passed, 0 failed, 0 skipped. The broader OpenVINO suite and security audit are supplementary and excluded from that passing total. The initial `dotnet test` attempts executed zero tests because of the MTP/VSTest runner mismatch and are not counted as test evidence.

## Native lock, package, cleanup, and non-claims

- The shared native lock was absent before and after verification and was never acquired.
- Package construction, signed-package activation, packaged discovery, native GGUF/OpenVINO journeys, real-model Chat/export, screenshots, accessibility, and performance were not run because the candidate-controlled closure preflight failed first.
- The native-receipt store was absent. A legacy handoff directory existed but was not treated as candidate-bound R4 native evidence.
- No candidate-root app, worker, converter, quantizer, llama, or OpenVINO process remained after verification.
- Windows Application Control independently blocked the freshly built hostile security fixture with `0x800711C7`; policy was not weakened. This external condition does not determine the verdict because E1-R4-001 already requires changes.
- Raw TRX/build logs remain under ignored local test output and are not committed. No local path, raw model data, secret, or private diagnostic is committed.
- E1 did not modify production code, package policy, signing, trust roots, firewall, execution policy, or machine security settings, and did not merge to `main`.

## Required C0 intake

C0 must publish a new committed issue-closure artifact containing `R3-001` through `R3-022` exactly once with positive exact-candidate executable evidence, then freeze and issue a new immutable E1 base. That production/evidence correction invalidates this E1 evidence and requires a complete E1 rerun. C0 may record but must not rewrite the E1 disposition.
