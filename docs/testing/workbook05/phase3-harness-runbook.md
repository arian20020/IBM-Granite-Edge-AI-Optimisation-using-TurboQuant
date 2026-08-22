# Workbook 05 C2 process-harness runbook

## Purpose

C2 supplies the model-independent Windows child-process boundary used by later
activation, storage, performance and quality stages. It proves that a command can
be launched without a shell, observed, stopped safely, retained as evidence and
resumed only under an unchanged execution identity. It does **not** load Granite
or prove any codec result.

## Evidence retained for every attempt

- executable and argument array, never a shell command string;
- working directory and bounded environment record;
- separate stdout and stderr files;
- structured driver events and exact raw output bytes;
- process-tree resource samples;
- normal-exit, timeout or descendant-first termination proof;
- deterministic attempt classification;
- retry relationship where the one infrastructure retry is used;
- manifest SHA-256 coverage for every retained file.

## Watchdog controls

The default C2 controller stops after five consecutive observations of any one
reviewed condition:

| Condition | Threshold |
|---|---:|
| Available physical memory | below 1.5 GiB |
| Windows commit usage | above 90% |
| Heartbeat age | above 900 seconds |

One healthy observation resets the corresponding streak. The synthetic hosted
workflow uses only bounded Python fixtures and cannot access a model or the
self-hosted Lenovo runner.

## Retry and resume

Only `InfrastructureInterrupted` may retry, once, and only after cooldown passes.
Model incompatibility, malformed output, timeout, resource safety stop, evidence
integrity failure, activation uncertainty and storage mismatch do not retry
automatically.

A checkpoint binds repository head, prerequisite proof, asset lock, executable,
request, configuration, prompt and rubric SHA-256 values. Every completed step
also binds the exact evidence file. Any changed identity or evidence byte rejects
resume.

## Repository verification

```powershell
.\scripts\testing\Validate-Workbook05-Phase3.ps1 `
    -RepositoryRoot (Resolve-Path '.').Path `
    -PythonPath 'python'
```

The dedicated hosted workflow first runs this complete gate, then produces one
model-free process bundle and sends the exact same-attempt artifact to a separate
Windows validator job.

## Explicit non-claims

Passing C2 does not prove model conversion, model load, text generation, requested
codec activation, fallback absence, physical K/V storage, TTFT, TPOT, throughput,
maximum context, perplexity or output quality.
