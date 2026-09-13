# Experiment report pack

Use this page when moving experiment facts into the dissertation. It gives a
short version of the checked results and points to the supporting files. It does
not replace the full evidence package.

## Evidence identity

- Result package: `unified-final-results-2026-09-01-v2`
- Main test computer where recorded: Windows 11 Lenovo laptop, Intel Core
  i5-12450H, Intel UHD integrated graphics and about 16 GB of RAM
- Inference routes: upstream `llama.cpp`, AtomicBot, animehacker, experimental
  OpenVINO and official OpenVINO
- Total attempts: 169
- Terminal results: 81 passed, 6 failed, 28 blocked and 54 artefact unavailable

Source: [campaign summary](final-results/catalog/campaign-summary.csv) and
[final-results overview](final-results/README.md).

## Main findings

### Compatibility

Selected Granite configurations ran through CPU, Vulkan, limited SYCL and the
tested OpenVINO routes on the recorded Intel Windows computer. This does not
show universal support. SYCL had wider edge-test failures, one route was blocked
by Windows policy, some accelerator placement was partial, and several OpenVINO
model groups were missing or stopped by the memory safety check.

### TurboQuant memory trade-off

TurboQuant reduced the explicit KV-cache allocation in several matched
`llama.cpp` cases. For example:

- AtomicBot Granite 8B CPU: 340 MiB with Q8_0 and 125 MiB with Turbo3, a 63.2%
  reduction.
- AtomicBot Granite 3B Vulkan: 320 MiB with F16 and 125 MiB with Turbo3, a
  60.9% reduction.
- AtomicBot Gemma 1B CPU: 26.0 MiB with F16 and 5.1 MiB with Turbo3, an 80.4%
  reduction.

The lower cache allocation did not give a consistent speed or quality gain.
Some TurboQuant cases were slower, and quality moved in both directions.

Source: [AtomicBot route data](final-results/02-atomicbot-turboquant/data/route.json),
[performance summary](final-results/catalog/performance-summary.csv) and the
[cross-route report](final-results/06-cross-route-comparison/reports/cross-route-comparison-report.md).

### OpenVINO comparison

Fifteen OpenVINO cases were matched closely enough for direct descriptive
comparison. Official decode speed was lower in all 15, with differences from
about 0.23% to 11.34%. The short workload used about 23 to 24 input tokens and
32 output tokens, so it does not test long-context KV-cache growth. The result
also cannot isolate one code change because the repositories and runtime
versions differ.

Source: [comparability matrix](final-results/catalog/comparability-matrix.csv)
and [cross-route report](final-results/06-cross-route-comparison/reports/cross-route-comparison-report.md).

### Output quality

Six experimental OpenVINO configurations completed the runtime stages but
produced repeated punctuation or character output and scored zero after the
output-health check. This shows why runtime success and useful output must be
reported separately.

Do not compare the average quality score of one route family with another. The
OpenVINO and `llama.cpp` routes used different prompt sets and scoring methods.

Source: [quality summary](final-results/catalog/quality-summary.csv) and
[quality policy](final-results/standards/quality-comparison-policy.md).

### Lower-memory systems

The tests show what completed or was stopped on the computer with about 16 GB
of RAM. They do not validate a 4 GB or 8 GB computer. They also do not give a
measured error rate for the application's memory estimator.

### TurboVec

At 10,000 chunks, TQ2, TQ3 and TQ4 were faster and smaller than Exact search,
but all failed the two retrieval-quality limits. No format passed Gate A. The
release decision is **demonstrator only** and no application integration is
recommended from this evidence.

Source: [TurboVec supporting rerun](../../experiments/processed-results/EXP-TV-COMP-001/production-scale-v2/supporting-rerun-2026-09-11/README.md)
and [decision record](../architecture/decisions/ADR-TurboVec.md).

## Safe conclusion wording

> On the tested Intel Windows computer, selected TurboQuant configurations
> reduced explicit KV-cache allocation, but they did not give consistent speed
> or output-quality gains. The results support choosing configurations case by
> case. They do not prove the same behaviour on other hardware, models,
> contexts or runtime versions.

For TurboVec:

> TurboVec reduced index size and search time in the supporting test, but every
> compressed format failed the fixed retrieval-quality gate. It was therefore
> kept as a demonstrator and was not recommended for application integration.

## Claims to avoid

- Do not say TurboQuant is always faster, better or more memory efficient.
- Do not call a requested device the actual device unless the run has evidence
  for that claim.
- Do not treat blocked or unavailable cases as failed inference.
- Do not replace missing values with zero.
- Do not rank quality scores made with different prompt sets or rubrics.
- Do not claim validated 4 GB or 8 GB support.
- Do not claim that the experiment files prove the WinUI application works or
  is easy to use.
