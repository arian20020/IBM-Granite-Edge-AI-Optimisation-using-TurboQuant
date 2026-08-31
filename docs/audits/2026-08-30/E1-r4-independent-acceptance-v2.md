# E1 R4 v2 independent exact-candidate acceptance — Round 3

## Disposition

`BLOCKED BY EXTERNAL ENVIRONMENT`

The immutable candidate remains blocked because the candidate, asset, H1, M1, Q1 and native-executable prerequisites required before package/native execution are absent. `R3-020=true` is supported only by the candidate-bound receipt and the structured, bound missing-candidate-manifest observation. `R3-022=false` because package activation, App Control gate execution, native execution and candidate E2E did not occur.

No package, native, candidate E2E, UI, visual, accessibility, performance or release approval is claimed.

## Exact identity and implementation scope

- Issued base ref: `refs/remotes/origin/integration/ucl-r4-e1-issued-base-v2`
- Tested base commit/tree: `b5d2cd34c57368efb9b122cddf16c2ffa2d3895e` / `a3e4d82095caa9688f30d2463fa5971c788fd33f`
- Round 3 implementation subject commit/tree: `fc6790c9920dca0036d5566777ec720164a6723b` / `995df9e0fef43434867438eece2f7c5dbbb8c973`
- Rejected return: `fc4c3b96ef71145b36f450cd3c587d42e0e2bba8`
- Return branch: `test/ucl-e1-native-acceptance-r4-v2`

The implementation range changes exactly seven permitted E1 files: the Round 3 plan, `R3IssueEvidenceVerifier.cs`, `R4TwoPhaseIssueEvidenceVerifierTests.cs`, `E1EvidenceSupportTests.cs`, `E1EndToEndRunnerInvocationTests.cs`, `E1EvidenceSupport.psm1`, and `Invoke-E1EndToEnd.ps1`. Product files, the immutable base and `main` are unchanged.

## Round 3 corrections

The verifier now uses checked exact arithmetic, reconciles aggregate counters with four discrete gate rows, distinguishes positive missing-prerequisite failure evidence from exact App Control pre-discovery `0/0` evidence, and permits zero managed execution only for a valid external-block disposition with four zero gate rows and no pass claims. Approval still requires positive passing package, App Control, native and E2E evidence, a positive native-lock acquisition, passing cleanup, a passing manifest, zero failure/skip and no external block.

The App Control observation contract requires exact candidate/subject identity, `E1-FOCUSED-HARNESS`, `0x800711C7`, `0/0`, attempted/observed flags and assembly digest/bytes matched to manifest evidence. Missing-prerequisite and App Control shapes cannot substitute for one another.

The production Evidence runner now creates or validates the complete `TestResults/Audit-20260830/E1-Evidence` chain with the regular, non-reparse, repository-contained safe-directory primitive before VSTest can run. All Round 2 trusted-tool, subject-boundary, fresh-build, exact-test, strict-TRX and cleanup protections remain.

## Actual final-subject execution

The exact-subject rebuild used canonical Program Files dotnet and SDK `10.0.400` MSBuild with `SourceRevisionId=fc6790c9920dca0036d5566777ec720164a6723b`. Build exit was 0 with zero reported warnings and zero errors. The fresh assembly was 282112 bytes with SHA-256 `68af38887d4a59071d67aac606f1e48ba2ec8dad2d26966c9e23af4e8592aee9` and product version `1.0.0+fc6790c9920dca0036d5566777ec720164a6723b`.

| Command/gate | Discovered | Executed | Passed | Failed | Skipped | Exit/result |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| Round 3 focused harness | 36 | 36 | 36 | 0 | 0 | 0; passed |
| External prerequisite preflight | 1 | 1 | 0 | 1 | 0 | 1; candidate manifest absent |
| Package activation | 0 | 0 | 0 | 0 | 0 | 1; blocked, not executed |
| App Control gate | 0 | 0 | 0 | 0 | 0 | 1; blocked, not executed |
| Native execution | 0 | 0 | 0 | 0 | 0 | 1; blocked, not executed |
| Candidate E2E | 0 | 0 | 0 | 0 | 0 | 1; blocked, not executed |
| Final candidate-bound evaluator | 1 | 1 | 1 | 0 | 0 | 0; passed |

The final canonical non-overlapping total is 37/37. Every test-command row satisfies `discovered == executed` and `executed == passed + failed + skipped`. Blocked and historical results are excluded. The exact final evaluator identity executed once and passed once with zero failures and zero skips.

The rejected return's exact-subject E1 assembly was previously blocked by Windows Application Control with `0x800711C7`. That historical result is not promoted to this successor subject: the successor's first focused attempt executed and passed, so no unchanged-binary retry was performed and no successor App Control observation is claimed.

## Native lock, cleanup and privacy

- Candidate, asset, H1, M1, Q1 and native-executable inputs were absent at the final prerequisite check.
- `C:\UCL-AUDIT-NATIVE.lock` was never acquired and is absent. Acquisitions: 0.
- Candidate process count after execution: 0.
- The isolated Evidence build root is absent; cleanup passed.
- No model data, secrets, usernames, environment dump or private rooted evidence path is committed.

## Reviews and issue result

The first independent implementation review found two Important issues: unchecked `Int64` overflow and incomplete missing-prerequisite `0/0` negative coverage. Both were corrected test-first in the successor subject. The successor implementation review and the final six-artifact review each found zero Critical, zero Important and zero Minor findings and returned `PASS_NO_REMAINING_CRITICAL_OR_IMPORTANT`. The final reviewer confirmed that substituting a successor App Control observation would fabricate evidence because the successor harness executed 36/36; the observed missing `candidateManifest` is therefore the truthful canonical external block.

Final issue result: `R3-020=true`; `R3-022=false`.
