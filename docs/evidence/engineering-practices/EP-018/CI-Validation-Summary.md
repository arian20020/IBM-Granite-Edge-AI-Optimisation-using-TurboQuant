# EP-018 GitHub-Hosted CI Validation Summary

## Run identity

| Field | Value |
|---|---|
| Workflow | Build and test |
| Workflow run ID | `29463973146` |
| Workflow run number | `6` |
| Job | Build WinUI and run unit tests |
| Job ID | `87513108129` |
| Head commit | `30b311f5576bfdfacf2928891bc7c7adeadb1b90` |
| Runner | GitHub-hosted Windows runner |
| Result | Success |

## Results

| Check | Result |
|---|---|
| Sparse checkout | Passed |
| .NET SDK setup | Passed |
| x64 MSBuild setup | Passed |
| WinUI application restore and Release x64 build | Passed |
| Unit-test project restore and Release build | Passed |
| Microsoft Testing Platform execution | Passed |
| Tests | 1 passed; 0 failed; 0 skipped |
| TRX generation | Passed |
| Artifact upload | Passed |

## Preserved artifact

| Field | Value |
|---|---|
| GitHub artifact | `unit-test-results-29463973146-1` |
| GitHub retention expiry | 15 August 2026 |
| Artifact archive SHA-256 | `8e6e55d72e49be2814ef651bda6bebdced5e1ae5ff7aea8fd63e868cff97274b` |
| Preserved file | `GraniteEdgeAI.UnitTests.trx` |
| Preserved TRX SHA-256 | `11005889320185526ab60a8c53e09b3a6c8b70973bd7dccdc8be5ea7dd735243` |

The committed TRX was extracted byte-for-byte from the verified GitHub artifact. It remains available after GitHub deletes the temporary artifact.

## Validation conclusion

The clean GitHub-hosted Windows run independently confirms that the repository can check out the required build inputs, compile the WinUI application, compile and execute the MSTest project through Microsoft Testing Platform, generate a TRX report and upload that report as a workflow artifact.

This is infrastructure evidence only. The single smoke test does not claim application-feature coverage.
