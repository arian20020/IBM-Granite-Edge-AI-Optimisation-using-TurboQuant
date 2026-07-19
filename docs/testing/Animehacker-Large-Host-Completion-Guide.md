# Animehacker WB-03 Large-Host Completion Guide

Use this path only to complete the frozen AH-06, AH-07, and AH-10 measurements on a Windows host with at least 32 GiB installed physical RAM. It preserves the existing configurations and the 2,048 MiB available-RAM emergency floor.

## Safety rules

The operator must not bypass either memory gate, run multiple rows concurrently, substitute a model, alter the frozen matrix, or treat a partial run as complete. A large-host run must not overwrite the laptop evidence under `runtime/` or `runtime-recovery/`; all new evidence belongs under a separate `large-host-completion` run directory.

The launcher performs preflight before starting a server. It rejects insufficient installed RAM, missing files, an active llama process, unsupported test IDs, and AH-10 without Level Zero evidence. There is no command-line RAM override.

## Prerequisites

- A Windows host with at least 32 GiB physical RAM.
- Python 3.11 or newer.
- The exact repository branch and frozen WB-03 matrix.
- The existing CPU and SYCL builds containing `bin/llama-server.exe`.
- The exact Granite 8B GGUF used by the campaign.
- `sycl-ls` and a `level_zero` Intel GPU entry when running AH-10.

Copy the repository and required external builds/models to the larger host. Do not copy evidence into the laptop's prior run directories.

## Run the completion package

From the repository root, use explicit absolute paths. The launcher runs rows serially, resumes valid runtime samples through the existing controller, executes P1-P6 only after complete runtime evidence, applies the frozen harsh quality rubric, and checks cleanup after each row.

```powershell
& .\scripts\testing\Run-Animehacker-LargeHost.ps1 `
  -Matrix .\experiments\manifests\animehacker-tq3-0\retest-matrix.json `
  -PromptSet .\experiments\granite_turboquant_intel\prompts\fixed-feasibility-prompt-set-v1.json `
  -CpuBuild C:\wb03-host\build-cpu `
  -SyclBuild C:\wb03-host\build-sycl `
  -DiagnosticModel C:\wb03-host\models\diagnostic.gguf `
  -Granite3Model C:\wb03-host\models\granite-3b.gguf `
  -Granite8Model C:\wb03-host\models\granite-8b.gguf `
  -OutputParent C:\wb03-host\evidence\large-host-completion `
  -Only AH-06,AH-07,AH-10
```

The run is stored as `AH-LH-YYYY-MM-DD-R####`. It contains `manifest.json`, runtime and quality directories, quality adjudications, controller logs, cleanup records, and resumable state. If interrupted, preserve the entire directory. The existing runtime controller's `--resume` behavior reuses only valid measurements; never delete or hand-edit evidence to force resume.

## Validate evidence for import

After all requested rows finish, validate the transferred run before changing WB-03:

```powershell
python .\scripts\testing\import_animehacker_large_host.py `
  --run-root C:\wb03-host\evidence\large-host-completion\AH-LH-YYYY-MM-DD-R0001 `
  --only AH-06 --only AH-07 --only AH-10
```

The command checks exactly three formal samples, all memory/performance fields, CPU and GPU mean/median/peak utilization, three activation records, P1-P6 hashes and harsh scores, matching model hashes, and zero residual processes. It creates `import-ready.json` only after those checks pass.

The controlled workbook must be updated only from this validated package. Regenerate the DOCX and run WB-03 reconciliation, DOCX structural audit, workbook revision validation, and controlled-workspace validation immediately after importing real host results. Until real evidence passes these gates, AH-06, AH-07, and AH-10 remain accurately safety-classified in WB-03.

## Failure interpretation

- A preflight failure means no workload was launched; correct the reported host, path, process, or Level Zero issue.
- A runtime failure retains logs and measurements but does not authorize quality or workbook completion.
- A quality or adjudication failure retains raw responses and cannot be replaced with an assumed score.
- An import failure means the workbook must remain unchanged until the evidence discrepancy is resolved.
