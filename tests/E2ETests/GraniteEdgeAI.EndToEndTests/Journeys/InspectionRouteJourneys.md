# Inspection route tests

These three tests use the installed application and public Windows UI Automation. They do not rebuild the app, change runtime packages or bypass safety checks. Their category is `NativeInspectionRoutes`.

- GGUF import, model inspection and hardware inspection must reach a fit, optimisation or memory decision. Unknown and operational failures are not accepted. A memory decision proves only that this stage was reached, not that optimisation can run.
- OpenVINO follows the same route and must offer an enabled configuration slider and Start action. Use a model and free-memory level that allow this. No optimisation is started by the test.
- Invalid GGUF import must leave Continue disabled. Removing it and importing a valid GGUF must then reach the compatibility decision.

These are inspection journeys, not complete optimisation, export and chat tests.

## Inputs

Use the existing candidate and asset manifest formats documented in the parent E2E README. Required environment variables:

- `GRANITE_E2E_CANDIDATE_MANIFEST`, `GRANITE_E2E_CANDIDATE_COMMIT`, `GRANITE_E2E_CANDIDATE_TREE`
- `GRANITE_E2E_ASSET_MANIFEST`
- `GRANITE_E2E_GGUF_FIT_PATH`
- `GRANITE_E2E_OPENVINO_FIT_PATH`
- `GRANITE_E2E_MALFORMED_GGUF_PATH` for recovery: a hash-recorded malformed GGUF that fails the quick scan
- `GRANITE_E2E_RESULTS_ROOT` for private failure diagnostics

The asset manifest must include every selected file, including the invalid fixture. Keep model paths, manifests with private paths, and raw diagnostics outside Git. Do not infer a build revision from its folder name. Verify that the installed package matches the candidate before running.

Close the application before execution. The tests refuse to take over a running instance. Do not open another instance or run other native tests during execution. The test-owned application is closed afterwards by the existing fixture.

Build only this test project. It has no application project reference. Use the installed .NET 8 Windows Desktop host to run the resulting test DLL with:

```text
--filter TestCategory=NativeInspectionRoutes --report-trx --report-trx-filename inspection-routes.trx --results-directory TestResults/InspectionRoutes
```

Do not change application code to make these tests pass. Record an application failure separately. A missing input is inconclusive, not a pass. Check the individual counts: the test runner can print an overall success banner when every case was skipped.

## Current check

To check discovery without opening the app, first build the project as described in the developer guide, then run from the repository root:

```powershell
dotnet tests/E2ETests/GraniteEdgeAI.EndToEndTests/bin/x64/Release/net8.0-windows10.0.19041.0/win-x64/GraniteEdgeAI.EndToEndTests.dll --list-tests --filter TestCategory=NativeInspectionRoutes
```

Expect exactly three tests. This discovery command and the test-only build were checked during the manual review: three found, zero build warnings and errors. Discovery does not run the journeys and is not a new pass result.

The configured live run on 14 September 2026 (UTC) passed all three tests: 3 executed, 3 passed, 0 failed and 0 skipped. The test project built with zero warnings and errors. See [the saved results](../../../../docs/testing/application-verification/results/inspection-routes.redacted.trx).

Earlier attempts were skipped for missing inputs or failed on test-helper control lookups. Those attempts are not counted as passes. The final run used the real installed app, recorded model hashes and corrected picker IDs. No application build or deployment was performed. All 61 recorded top-level application files kept the same hashes.

Private control-tree snapshots are under `TestResults/InspectionRoutesLive/Passed_*`. The existing screenshot helper copies screen pixels and can capture an overlapping window; its images are not accepted as visual evidence for this run. The TRX results are the retained pass evidence. These tests do not verify optimisation execution, export, chat, every slider band or every hardware configuration.
