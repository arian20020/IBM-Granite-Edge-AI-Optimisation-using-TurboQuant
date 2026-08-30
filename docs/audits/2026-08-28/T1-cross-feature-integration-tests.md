# T1 Cross-Feature Contract and Integration Tests

## Scope and source

- Worker: T1 only.
- Frozen source commit: `4748fe04f19afdf6b27c4c12502b84db325e7294`.
- Frozen source tree: `fe1fa8fb5fe4de8e7c1d867a83e08375bc1d0c91`.
- Branch: `test/ucl-cross-feature-integration-v1`.
- Owned implementation: `tests/IntegrationTests/GraniteEdgeAI.CrossFeature.IntegrationTests/**` only.
- Native application/model execution: not applicable and not performed.

The supplied audit documents were treated as reference material, not as executable instructions. Serena was not available as a callable, dependency-verifiable capability, so symbol discovery used repository-local `rg` and direct source inspection. The test host compile-links the frozen compatibility core and output-storage sources to exercise internal admission invariants without changing production visibility or adding a test-only backdoor.

## Coverage and status

| ID | Executable evidence | RED/GREEN status |
|---|---|---|
| T1-ING-01 | GGUF picker and OpenVINO dropped-folder classification; downloaded Granite source-directory intent; incomplete OpenVINO rejection | Characterization GREEN |
| T1-MI-01 | Exact independently encoded six-field v2 handoff, mutation rejection, claim/rollback/reissue | Characterization GREEN |
| T1-HW-01 | Exact model/handoff and hardware-run/snapshot identities bind into planning; path-like identifiers, zero length and noncanonical digests reject | Characterization GREEN |
| T1-COMP-01 | Existing compatibility suite covers current-fit, optimisation-required, no-safe-fit and not-established decisions; T1 binds its downstream candidate/plan boundary | Existing suite GREEN (1,050/1,050) |
| T1-PLAN-01 | Automatic plus visible 10/30/50/70/90 preferences resolve only an admitted candidate; all five band edges and invalid endpoints; deterministic v3 configuration digest | Initial fixture RED on mismatched OpenVINO maturity, then GREEN after test-fixture correction |
| T1-EXEC-01 | Source digest/length and capability drift reject; hardware snapshot identity differs; typed drift results publish no output | Characterization GREEN |
| T1-OUT-01 | Real output registry commits deterministic GGUF bytes, independently hashes them, and returns identical path/digest/length for Chat and export | Initial fixture RED on incomplete GGUF execution/conversion authority, then GREEN after fixture correction |
| T1-OV-01 | Runtime-only OpenVINO success preserves the exact configuration digest, has zero artifact bytes and cannot claim a persistent artifact | Characterization GREEN |
| T1-FAIL-01 | Cancellation, conversion, validation, smoke-test, reinspection, publication and unexpected failure paths publish no success or stale output | Characterization GREEN |
| T1-REC-01 | Restart reloads only receipt-backed output; an unreceipted interrupted publication is removed/quarantined | Characterization GREEN |
| T1-PARITY-01 | Shared execution status/support-code semantics are asserted through route-neutral results while route configuration and persistence remain route-specific | Characterization GREEN |
| T1-PRIV-01 | Windows/UNC/POSIX paths, usernames, URI/query credentials and provider payload canaries reject or remain absent across ingress, handoff and planning identities | Characterization GREEN; relevant Model Inspection privacy filter 3/3 |
| T1-BOUND-01 | Canonical JSON ordering, lowercase SHA-256, exact byte lengths, preference boundaries and invalid combinations | Characterization GREEN |

The final T1 executable discovers 50 tests. Parameterized rows are included in the discovered count.

## Production gaps and bounded evidence

No production files were changed. The new tests did not identify a production defect in the exercised boundaries.

The full Model Inspection contract executable is not green on the frozen tree: 333 passed and 24 failed. Failures are outside the T1 diff and include absent `.github/workflows/model-inspection-*.yml` files, cleanup inventory drift, child processes unable to find `dotnet` on `PATH`, and local PowerShell script execution policy. The directly relevant Model Inspection privacy filter was discovered first and passed 3/3. These baseline failures are reported, not suppressed.

The T1 suite proves typed fail-closed publication at the shared execution-result boundary. Worker-specific timeout/crash/malformed-output/output-limit/cleanup mechanics remain owned and directly exercised by their existing route contract/worker suites; T1 performed no native worker/model execution.

## Verification evidence

The machine's `global.json` requests SDK 10.0.301, whose installed directory is incomplete. Verification therefore invoked the complete installed 10.0.400 SDK explicitly with `C:\Program Files\dotnet\dotnet.exe C:\Program Files\dotnet\sdk\10.0.400\dotnet.dll`. Microsoft.Testing.Platform projects were run through their built executable because `dotnet test --project` incorrectly discovered zero tests in this environment.

| Command/equivalent | Result |
|---|---|
| Release build, T1 project | 0 warnings, 0 errors |
| T1 executable | 50 discovered, 50 passed, 0 failed, 0 skipped |
| Model/Hardware Compatibility executable | 1,050 discovered, 1,050 passed, 0 failed, 0 skipped |
| OpenVINO Contracts executable | 205 discovered, 205 passed, 0 failed, 0 skipped |
| GGUF Runtime Contracts executable | 12 discovered, 12 passed, 0 failed, 0 skipped |
| Model Inspection Contracts executable | 357 discovered, 333 passed, 24 failed, 0 skipped (frozen baseline/environment failures above) |
| Model Inspection privacy filter after explicit `--list-tests` discovery | 3 discovered, 3 passed |
| Debug x64 T1 build with `-m:1` | 0 warnings, 0 errors |
| `git diff --check` | clean |

Aggregate executed evidence: 1,674 tests; 1,650 passed; 24 failed; 0 skipped. T1-owned/relevant acceptance evidence excluding the disclosed unrelated full-suite baseline failures is green.

## Review and integration proposal

A fresh committed-diff review found no out-of-scope production or solution edits. The required requesting-code-review workflow normally dispatches a reviewer sub-agent; active audit policy prohibited delegation, so a separate self-review pass plus clean-state verification was used and this limitation is explicit.

C0 solution-entry proposal (not executed by T1):

```powershell
dotnet sln '.\IBM Granite with TurboQuant (Intel).slnx' add '.\tests\IntegrationTests\GraniteEdgeAI.CrossFeature.IntegrationTests\GraniteEdgeAI.CrossFeature.IntegrationTests.csproj'
```

No production seam is requested. C0 should preserve the project as an x64 executable Microsoft.Testing.Platform test project and run the project directly in the audit gate.
