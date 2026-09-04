# Production-Scale v2 Baseline Reconciliation

## Preserved base

The final campaign starts from commit `07998fb7766887689a222dd0c8726d821fa33169` and tree `ed68db942aa26c2e66218c10012c914a0e3c361e` on the clean linked worktree that previously used `test/ucl-turbovec-pdf-feasibility-v1`. Work continues only on `test/turbovec-production-scale-final-evaluation-v2`.

## Controlled harness arithmetic

The older report's 31/31 count described an earlier harness state. At the preserved base, discovery and execution produced 39/39 passing Python tests, with zero failed or skipped. The historical 31/31 statement is retained as history but must not describe the current suite.

## PDF arithmetic and runner classification

The generated Microsoft Testing Platform executable discovered and passed 5/5 PdfPig tests. With the repository-pinned .NET 10.0.301 SDK and Microsoft Testing Platform configuration, `dotnet test` built the same assembly but reported zero discovered tests and exited 5. Direct execution proved that the assembly, MSTest registration, and five tests were intact. The bridge result is an infrastructure/test-entry-point failure, not a PDF-test failure; both observations are preserved.

## Historical experiment reproduction

Diagnostic run `EXP-TV-COMP-001-20260904T025427Z-090` used the pinned OpenVINO Granite model and TurboVec 1.0.0 assets. Recall@1/@5/@10, nDCG, MRR, and serialized index byte counts matched canonical run `EXP-TV-COMP-001-20260902T231605Z-005`. Latency differed and is not admitted as formal evidence because the new machine-readiness and repetition protocol had not begun.

The diagnostic artifacts remain outside the repository under the experiment-owned asset root. Their hashes are recorded in `experiments/manifests/turbovec/production-scale-v2/preflight.json` so their identities can be checked without publishing private absolute paths.
