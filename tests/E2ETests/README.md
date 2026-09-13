# Packaged end-to-end tests

`GraniteEdgeAI.EndToEndTests` is the repository's out-of-process Windows UI Automation suite for the packaged Granite Edge AI application. It launches the exact candidate by package family/application ID, identifies controls through public accessibility IDs or accessible names, waits on observable state with bounded timeouts, and captures screenshots/UIA trees only as failure diagnostics.

The suite does not use Playwright, WinAppDriver, coordinates, OCR, visual pixels for functional assertions, production fixture routes, or arbitrary sleeps. The inbox Windows UI Automation API keeps the runner local and server-free.

## Test layers and guards

- Deterministic tests validate strict candidate/asset manifests, SHA-256 binding, privacy redaction and condition waits. They do not claim a native journey.
- `NativeSmoke` requires `GRANITE_E2E_CANDIDATE_MANIFEST`; malformed-source tests additionally require an authorized local fixture path.
- `NativeFailure` requires the candidate plus the scenario-specific failure input.
- `NativeAcceptance` requires the candidate, a route asset, and exact H1/M1/Q1 evidence manifests. A missing variable is MSTest inconclusive with `Blocked by declared guard`, never a pass.

The candidate manifest may contain the local executable path because it is a git-ignored run input. Its source commit/tree must match the exact integrated branch tip/tree selected by the runner; the runner separately proves that tip descends from the frozen audit source. The committed asset manifest format contains only stable IDs, routes, byte lengths and lowercase SHA-256 values; model paths and weights must never be committed. Asset hashes are streamed under a closed regular-file/reparse-point policy.

Run discovery and each stage through:

```powershell
.\tests\E2ETests\GraniteEdgeAI.EndToEndTests\scripts\Invoke-E1EndToEnd.ps1 `
  -CandidateManifest <local-candidate.json> -Stage List `
  -DotNetHostPath <complete-dotnet.exe>
```

The script verifies frozen ancestry plus the exact candidate tip/tree, builds the candidate and test project as Debug x64 with a selected complete SDK, rejects stale/missing/empty `.build.appxrecipe` output, requires Visual Studio VSTest, and requires at least the expected non-zero test inventory before applying a filter. It uses `tests/runsettings/OneWorker.runsettings` and writes raw results only below ignored `TestResults/Audit-20260828/E1/`.

The deterministic stage does not acquire the native lock. Smoke, failure, acceptance and all-test native stages require the audit lock and predecessor receipts described by the E1 assignment. Before releasing the lock, the runner checks for candidate-root processes created during its slot and preserves the lock on ambiguous cleanup. Do not run the app, workers, model tools, conversion, quantisation or Chat concurrently with another native worker.

## Solution proposal for C0

E1 does not edit shared solution composition. C0 can add this exact entry under `/tests/E2ETests/` after reconciliation:

```xml
<Project Path="tests/E2ETests/GraniteEdgeAI.EndToEndTests/GraniteEdgeAI.EndToEndTests.csproj">
  <Platform Solution="*|x64" Project="x64" />
</Project>
```
