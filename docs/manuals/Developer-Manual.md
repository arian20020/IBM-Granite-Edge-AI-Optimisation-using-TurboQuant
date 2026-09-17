# Developer guide

To run the supplied application rather than develop it, use [Run the app](Run-the-App.md) and [Granite-Start-Here.md](Granite-Start-Here.md). Their commands target the 1.0.4.0 ZIP; a later documentation or test commit does not mean the ZIP was rebuilt. Do not apply the published ZIP checksum to your own build.

Start with [build and installation](Build-and-Installation.md). This page explains where the code lives and how to check a change without confusing experiments, unit tests and the installed app.

## Project map

| Location | Purpose |
| --- | --- |
| `IBM Granite with TurboQuant (Intel)/Features` | WinUI screens and feature integration. |
| `shared` | Shared rules and data contracts, including compatibility and download authority. |
| `infrastructure` | Worker clients, platform integration and process boundaries. |
| `runtime` | Runtime adapters and capability handling. |
| `workers` | Separate executable workers used by the application. These are runtime dependencies, not optional documentation. |
| `third-party` | Pinned external inputs and licence records. |
| `tests` | Application unit, contract, integration and E2E test projects. |
| `experiments` | Experiment scripts, configurations and retained research inputs/results. |
| `docs/testing/final-results` | Published experiment evidence and reproduction entry point. |

The onboarding shell connects model import, model inspection, hardware inspection, compatibility, optimisation and chat. The interface presents the result of those rules; it should not invent a supported configuration or bypass a failed integrity check.

## Rules worth preserving

- An accepted source, runtime and configuration must still match when work starts.
- Current memory and disk checks can rule out an earlier selection.
- Cancellation and late results belong to a specific attempt. A cancelled or replaced attempt must not publish later success or navigate forward.
- Runtime-only choices and conversion to a saved model are different operations.
- Temporary output must not become a published model before validation.
- Test fixtures and gallery screens are not real-model evidence.

If you change one of these rules, review the relevant contracts and tests before changing the screen text.

## Running tests

See [the test index](../../tests/README.md) for the current layers and [CI scope](../testing/CI-Test-Scope.md) for exclusions. Use each project with its intended runner; a WinUI packaged test is not interchangeable with a normal console test.

The new inspection journeys can build independently of the app:

```powershell
dotnet build tests/E2ETests/GraniteEdgeAI.EndToEndTests/GraniteEdgeAI.EndToEndTests.csproj -c Release -p:Platform=x64
```

Supply the inputs in [InspectionRouteJourneys.md](../../tests/E2ETests/GraniteEdgeAI.EndToEndTests/Journeys/InspectionRouteJourneys.md) before running native tests. Close the app first. They use the installed candidate and real model files; missing inputs are not a pass. Use a .NET host with the required Windows Desktop runtime.

Read the TRX results, not just a green runner banner. Confirm the expected tests were discovered and executed and that none were unexpectedly skipped. Record the filter and input identity with each result.

The [CI run for ca0eeba7](https://github.com/arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant/actions/runs/35231533452) passed 1,959 tests. Four saved-GGUF-cache positive cases require the pinned admitted runtime and use `RequiresVerifiedGgufRuntimeClosure`, which the ordinary CI filter excludes. They were run separately in the local pinned-runtime harness; this is not a claim that CI tests those runtime bytes.

Build test hosts and their worker manifests from matching inputs. An incremental test-only build can mix an existing application DLL with a newer worker manifest in `obj`; that is an integrity failure, not a reason to disable verification or change expected hashes. Also give navigation fixtures real isolated source directories where production code reads saved-profile metadata.

## Experiments and application evidence

Follow the existing [experiment reproduction guide](../testing/final-results/REPRODUCING.md). Do not regenerate controlled workbooks or edit their manifests during an unrelated app change.

A successful standalone runtime experiment does not prove application integration. Likewise, an enabled slider does not prove optimisation completed. Keep these evidence types separate in documentation.

## A safe change checklist

1. Record the starting commit and inspect uncommitted changes.
2. Make the smallest relevant change; preserve unrelated work.
3. Run focused checks with the correct dependencies.
4. If packaging changed, test the installed package on both required routes.
5. Check failures, cancellations and unavailable-resource states as well as success.
6. Update the relevant guide and evidence record. Keep private paths, model weights and personal data out of commits.

Do not repair a package error by copying random DLLs into a working installation. Do not describe a test as fixed when it was only excluded. For known exceptions, use the [limitations page](Known-Limitations.md).
