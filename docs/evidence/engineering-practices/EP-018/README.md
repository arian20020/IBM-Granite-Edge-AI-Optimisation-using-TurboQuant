# EP-018 — Executable Testing and CI Foundation Evidence

## Evidence status

The narrowed EP-018 implementation boundary is complete and supported by a successful GitHub-hosted Windows run. Formal RTM validation remains pending until the controlled workbook adopts the narrowed scope and its generated repository snapshots are regenerated.

## Narrowed implementation boundary

> Establish executable test-project structure, testing-platform configuration, Windows CI, test-result generation and preserved CI evidence.

## Completed criteria

| Criterion | Result | Evidence |
|---|---|---|
| Executable MSTest project exists | Pass | `tests/UnitTests/GraniteEdgeAI.UnitTests/` |
| Microsoft Testing Platform is configured | Pass | `global.json`; `GraniteEdgeAI.UnitTests.csproj` |
| Test project is registered in the solution | Pass | `IBM Granite with TurboQuant (Intel).slnx` |
| Release application and test-project builds run in Windows CI | Pass | `CI-Validation-Summary.md` |
| Unit test executes successfully on a GitHub-hosted Windows runner | Pass | `GraniteEdgeAI.UnitTests.trx` |
| TRX evidence is generated, uploaded and permanently preserved | Pass | `CI-Validation-Summary.md`; `GraniteEdgeAI.UnitTests.trx` |
| CI process and local execution are documented | Pass | `docs/architecture/diagrams/CI-Build-and-Test-Workflow.md`; `tests/README.md` |

## Approved fixture and manifest deferral

Fixture files, the fixture manifest and fixture-manifest integrity validation are deferred from EP-018 to EP-023. The first implementation will support REQ:F-M03 and WP:IM-04 during development of deterministic model-input validation. OpenVINO-specific fixture scenarios will additionally trace to WP:IM-06 and, where runtime integration is involved, EP-024.

| Relationship | Stable owner |
|---|---|
| Primary engineering practice | `EP:EP-023` — Implement unit tests for pure logic |
| Primary requirement | `REQ:F-M03` — Validate a selected input before using it |
| Primary work package | `WP:IM-04` — Format detector and GGUF header validation |
| OpenVINO classification | `WP:IM-06` — Other-format recognition and classification |
| Runtime integration | `EP:EP-024` |

## Claim boundary

This evidence proves that the executable MSTest and Windows CI foundation operated successfully on the recorded GitHub-hosted Windows run and that its raw TRX evidence is preserved in version control.

It does not prove model-input validation behavior, fixture correctness, integration or contract behavior, inference, performance, code coverage or release readiness.

## Controlled RTM follow-up

Do not manually edit generated traceability files. The authoritative RTM workbook must adopt the narrowed EP-018 wording, transfer the deferred fixture and manifest scope to EP-023, update statuses through the controlled review process, and regenerate the repository snapshots before formal RTM closure is claimed.
