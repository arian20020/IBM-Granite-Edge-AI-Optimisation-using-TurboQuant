# E1 R4 v2 independent exact-candidate acceptance — C0 intake correction

## Disposition

`BLOCKED BY EXTERNAL ENVIRONMENT`

The immutable candidate remains blocked because the candidate, asset, H1, M1, Q1 and native-executable prerequisites required before package/native execution are absent. `R3-020=true` is supported only by the candidate-bound receipt and the structured, bound missing-candidate-manifest observation. `R3-022=false` because package activation, App Control gate execution, native execution and candidate E2E did not occur.

No package, native, candidate E2E, UI, visual, accessibility, performance or release approval is claimed.

## Exact identity and implementation scope

- Issued base ref: `refs/remotes/origin/integration/ucl-r4-e1-issued-base-v2`
- Tested base commit/tree: `b5d2cd34c57368efb9b122cddf16c2ffa2d3895e` / `a3e4d82095caa9688f30d2463fa5971c788fd33f`
- Final implementation subject commit/tree: `ded192b245e6f220469c4721e2a8cda9435123f1` / `b87a4e03b1b3c4eef4d63bf804fc8a0d0734a20d`
- Superseded return: `e5405f0111c10a10dde49a470902b3d522924318`
- Return branch: `test/ucl-e1-native-acceptance-r4-v2`

The C0 intake correction changes only the Round 3 plan, `R3IssueEvidenceVerifier.cs`, `R4TwoPhaseIssueEvidenceVerifierTests.cs`, `E1EvidenceSupportTests.cs`, and `E1EvidenceSupport.psm1`. Product files, the immutable base and `main` are unchanged.

The intake review found that the missing-prerequisite branch accepted any positive failed command. The final verifier now requires the exact `EXTERNAL-PREREQUISITE-PREFLIGHT` command, and a negative regression proves that an unrelated failed App Control command cannot close R3-020. The artifact boundary advanced to observation v4 and review v6; superseded artifact names are rejected after the final implementation subject.

## Round 3 corrections

The verifier now uses checked exact arithmetic, reconciles aggregate counters with four discrete gate rows, distinguishes positive missing-prerequisite failure evidence from exact App Control pre-discovery `0/0` evidence, and permits zero managed execution only for a valid external-block disposition with four zero gate rows and no pass claims. Approval still requires positive passing package, App Control, native and E2E evidence, a positive native-lock acquisition, passing cleanup, a passing manifest, zero failure/skip and no external block.

The App Control observation contract requires exact candidate/subject identity, `E1-FOCUSED-HARNESS`, `0x800711C7`, `0/0`, attempted/observed flags and assembly digest/bytes matched to manifest evidence. Missing-prerequisite and App Control shapes cannot substitute for one another.

The production Evidence runner now creates or validates the complete `TestResults/Audit-20260830/E1-Evidence` chain with the regular, non-reparse, repository-contained safe-directory primitive before VSTest can run. All Round 2 trusted-tool, subject-boundary, fresh-build, exact-test, strict-TRX and cleanup protections remain.

## Actual final-subject execution

The exact-subject rebuild used canonical Program Files dotnet with `SourceRevisionId=ded192b245e6f220469c4721e2a8cda9435123f1`. Build exit was 0 with zero reported warnings and zero errors. The fresh assembly was 283136 bytes with SHA-256 `21e07d2d2da896e9e154818e61c4a517168bbb78a87c43fee16fe2f8d7bef1bc` and product version `1.0.0+ded192b245e6f220469c4721e2a8cda9435123f1`.

| Command/gate | Discovered | Executed | Passed | Failed | Skipped | Exit/result |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| C0 intake focused harness | 37 | 37 | 37 | 0 | 0 | 0; passed |
| External prerequisite preflight | 1 | 1 | 0 | 1 | 0 | 1; candidate manifest absent |
| Package activation | 0 | 0 | 0 | 0 | 0 | 1; blocked, not executed |
| App Control gate | 0 | 0 | 0 | 0 | 0 | 1; blocked, not executed |
| Native execution | 0 | 0 | 0 | 0 | 0 | 1; blocked, not executed |
| Candidate E2E | 0 | 0 | 0 | 0 | 0 | 1; blocked, not executed |
| Final candidate-bound evaluator | 1 | 1 | 1 | 0 | 0 | 0; passed |

The final canonical non-overlapping total is 38/38. Every test-command row satisfies `discovered == executed` and `executed == passed + failed + skipped`. Blocked and historical results are excluded. The exact final evaluator identity executed once and passed once with zero failures and zero skips.

The rejected return's exact-subject E1 assembly was previously blocked by Windows Application Control with `0x800711C7`. That historical result is not promoted to this successor subject: the successor's first focused attempt executed and passed, so no unchanged-binary retry was performed and no successor App Control observation is claimed.

## Native lock, cleanup and privacy

- Candidate, asset, H1, M1, Q1 and native-executable inputs were absent at the final prerequisite check.
- `C:\UCL-AUDIT-NATIVE.lock` was never acquired and is absent. Acquisitions: 0.
- Candidate process count after execution: 0.
- The isolated Evidence build root is absent; cleanup passed.
- No model data, secrets, usernames, environment dump or private rooted evidence path is committed.

## Reviews and issue result

Earlier reviews found and corrected unchecked `Int64` overflow, incomplete missing-prerequisite `0/0` coverage, and non-exact missing-prerequisite command binding. The final independent implementation review found zero Critical and zero Important findings and returned `PASS_NO_REMAINING_CRITICAL_OR_IMPORTANT`. Historical App Control evidence remains non-promoted; the observed missing `candidateManifest` is the truthful canonical external block.

Final issue result: `R3-020=true`; `R3-022=false`.
