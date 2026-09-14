# Application tests

Run commands from the repository root on Windows. Start with the [build guide](../docs/manuals/Build-and-Installation.md). Native tests need verified workers and model inputs. Missing inputs and skipped tests are not passes.

| Layer | Instructions | Limits |
| --- | --- | --- |
| Packaged WinUI unit tests | Use the restore, build and VSTest app-container steps in the [workflow](../.github/workflows/build-and-test.yml) together. | Do not run the packaged DLL as a console test. Use the workflow's exact filter. |
| Contract tests | Example below. | Other projects may have different dependencies. |
| Worker integration tests | See the [integration projects](IntegrationTests) and their setup instructions. | Prepare the required worker and model first. An empty-input run is not evidence. |
| Three inspection E2E tests | Follow the [input and execution guide](E2ETests/GraniteEdgeAI.EndToEndTests/Journeys/InspectionRouteJourneys.md). | Uses an installed app; does not cover full optimisation, export or chat. |

Example for an executable contract-test project:

```powershell
dotnet run --project tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/GraniteEdgeAI.OpenVino.Contracts.Tests.csproj -c Release -- --report-trx --results-directory TestResults/OpenVinoContracts
```

This example follows the project's runner settings; it was not rerun during the documentation update. See [CI scope](../docs/testing/CI-Test-Scope.md) for exclusions and [saved results](../docs/testing/application-verification/README.md) for checks that actually ran. Read executed, passed, failed and skipped counts in the TRX.

Earlier commands and counts are kept in [historical notes](Historical-Inspection-Notes.md), not current setup instructions. Experiments have a separate [reproduction guide](../docs/testing/final-results/REPRODUCING.md).
