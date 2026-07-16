# Application and System Test Projects

This directory follows the hierarchy required by the supplied project testing standard. Folder creation is preparation only; executable tests and preserved results are the evidence.

```text
tests/
├── UnitTests/
│   └── GraniteEdgeAI.UnitTests/
├── ContractTests/
├── IntegrationTests/
├── E2ETests/
└── TestFixtures/
```

## Current executable test projects

`tests/UnitTests/GraniteEdgeAI.UnitTests/` is the repository's first real MSTest project. It currently contains only a testing-foundation smoke test that proves the project compiles and that Microsoft Testing Platform discovers and executes it. It does not test application behaviour yet.

Application behaviour tests will be added alongside future feature development. TestFixtures, IntegrationTests, ContractTests and E2ETests will be implemented in later milestones.

Run the current unit-test project locally from the repository root:

```powershell
dotnet test ".\tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj" --configuration Release
```

## Continuous integration

The locally implemented `.github/workflows/build-and-test.yml` workflow is designed to restore and build the WinUI application, restore and build the MSTest project, run unit tests through Microsoft Testing Platform, generate a TRX report, and retain that report as a GitHub Actions artifact for 30 days.

The workflow has not yet run on GitHub, so GitHub-hosted validation remains pending. Its engineering process is documented in `docs/architecture/diagrams/CI-Build-and-Test-Workflow.md`.

Contract-test and integration-test execution are planned for later milestones. They are not included in the workflow until real executable projects exist for those testing categories.

## Evidence rule

- Unit tests verify deterministic business logic.
- Contract tests verify each runtime adapter against pinned tool behaviour.
- Integration tests verify component boundaries and process communication.
- End-to-end tests verify complete user journeys and important failure journeys.
- Performance and AI-quality results belong under `experiments/granite_turboquant_intel/` and must cite the matching application test where relevant.
- Fixtures must be small, licensed and safe to commit; model weights are never stored here.
