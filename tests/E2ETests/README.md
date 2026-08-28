# Packaged end-to-end tests

`GraniteEdgeAI.EndToEndTests` is the repository's out-of-process Windows UI Automation suite for the packaged Granite Edge AI application. It launches the exact candidate by package family/application ID, identifies controls through public accessibility IDs or accessible names, waits on observable state with bounded timeouts, and captures screenshots/UIA trees only as failure diagnostics.

The suite does not use Playwright, WinAppDriver, coordinates, OCR, visual pixels for functional assertions, production fixture routes, or arbitrary sleeps. The inbox Windows UI Automation API keeps the runner local and server-free.

## Test layers and guards

- Deterministic tests validate strict candidate/asset manifests, SHA-256 binding, privacy redaction and condition waits. They do not claim a native journey.
- `NativeSmoke` requires `GRANITE_E2E_CANDIDATE_MANIFEST`; malformed-source tests additionally require an authorized local fixture path.
- `NativeFailure` requires the candidate plus the scenario-specific failure input.
- `NativeAcceptance` requires the candidate, a route asset, and exact H1/M1/Q1 evidence manifests. A missing variable is MSTest inconclusive with `Blocked by declared guard`, never a pass.

The candidate manifest may contain the local executable path because it is a git-ignored run input. Its source commit/tree must match the exact immutable C0 integration candidate selected by the runner, not the later E1 test-branch tip. The committed asset manifest format contains only stable IDs, routes, byte lengths and lowercase SHA-256 values; model paths and weights must never be committed. Asset hashes are streamed under a closed regular-file/reparse-point policy.

Run discovery and each stage through:

```powershell
.\tests\E2ETests\GraniteEdgeAI.EndToEndTests\scripts\Invoke-E1EndToEnd.ps1 `
  -CandidateManifest <local-candidate.json> `
  -IntegrationCandidateCommit <exact-new-C0-commit> `
  -IntegrationCandidateTree <exact-new-C0-tree> `
  -Stage List `
  -DotNetHostPath <complete-dotnet.exe>
```

R3 never derives candidate identity from the E1 branch tip or trusts a C0 completion statement. It rejects the previous C0 tip `a5ef3558334e50587889140dafba194853938765`, proves frozen and previous-C0 ancestry, proves the E1 branch descends from the exact new C0 candidate, verifies the exact pushed remote ref, and permits only E1-owned test/planning/report changes after it. It then verifies executable hash/bytes, package family, application ID and installed package root.

Run `-Stage Deterministic` first without candidate arguments. Packaged stages are `List`, `Smoke`, `Failure`, `Acceptance`, `Restart`, `RealModel`, and `All`. Native List/campaign stages additionally require `-IntegrationCandidateRemoteRef`, `-R3ClosureManifest`, and `-R3ClosureRelativePath`. The R3 closure manifest must be committed at the candidate and contain exactly R3-001 through R3-022 with executable GREEN evidence; producer evidence blobs are verified from their separate subject commits/trees. The script builds Debug x64 with the selected complete SDK, rejects stale/missing/empty `.build.appxrecipe` output, uses authoritative Visual Studio VSTest x64 discovery and one-worker settings, and writes unique raw TRX plus arithmetic-checked JSON summaries only below ignored `TestResults/Audit-20260829/E1/`.

The deterministic stage does not acquire the native lock. Before package construction or native lock acquisition, R3 requires sanitized H1/M1/Q1 manifests, closed H1/M1/Q1/F1 native receipts, exact handoff/report/manifest hashes and byte counts, Git-blob evidence-subject joins, command arithmetic, production-reachability records, cleanup verification, and accepted A1/H1/M1/Q1/F1/S1/T1 integration by ancestry or patch equivalence. Any missing product evidence is `CHANGES REQUIRED`, not an external-environment pass. Before releasing the lock, the runner checks for candidate-root processes created during its slot and preserves the lock on ambiguous cleanup. Do not run the app, workers, model tools, conversion, quantisation or Chat concurrently with another native worker.

## Solution proposal for C0

E1 does not edit shared solution composition. C0 can add this exact entry under `/tests/E2ETests/` after reconciliation:

```xml
<Project Path="tests/E2ETests/GraniteEdgeAI.EndToEndTests/GraniteEdgeAI.EndToEndTests.csproj">
  <Platform Solution="*|x64" Project="x64" />
</Project>
```
