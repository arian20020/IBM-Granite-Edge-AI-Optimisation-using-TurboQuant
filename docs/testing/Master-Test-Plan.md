# Master Test Plan

**Version:** 1.0  
**Campaign:** Granite-TurboQuant Controlled Retest Campaign v1  
**Status:** Initial controlled baseline

## 1. Campaign objectives

1. Reproduce dependable IBM Granite baselines before evaluating experimental optimisation.
2. determine whether each route builds and runs on the intended Windows hardware.
3. verify the actual backend, device and optimisation state.
4. measure memory, latency, throughput, output quality and stability under controlled settings.
5. preserve enough evidence for another person to understand and repeat each result.
6. complete the existing testing workbooks from the authoritative evidence.

## 2. Controlled route order

1. Upstream llama.cpp baseline.
2. AtomicBot TurboQuant.
3. animehacker TQ3_0.
4. Official OpenVINO.
5. Custom OpenVINO TurboQuant.
6. Cross-route comparison.

A later route must not be used to hide a failed prerequisite in an earlier route. Blocked or failed routes remain documented.

## 3. Standard route phases

1. Repository and dependency capture.
2. Clean build validation.
3. Diagnostic or control-model smoke test where required.
4. IBM Granite compatibility test.
5. CPU baseline.
6. Supported standard quantisation or cache baseline.
7. Optimisation activation test.
8. Device and backend confirmation.
9. Memory and performance measurements.
10. Output-quality and instruction-following checks.
11. Stability, failure and repeatability checks.
12. Route decision and workbook completion.

## 4. Entry gates

A test can move to `Ready` only when its ID, purpose, preconditions, command template, expected evidence and pass criteria have been reviewed.

## 5. Exit gates

A route can be marked complete only when:

- required tests have a final classification;
- blocked and failed tests have recorded reasons;
- raw evidence and manifests are committed;
- processed values can be traced to raw files;
- workbook sections are complete;
- the route conclusion states its tested boundaries and limitations.

## 6. Comparison controls

Cross-route comparisons must match, as far as the runtimes permit:

- model family and parameter size;
- model-weight precision;
- prompt and generation length;
- context length;
- sampling configuration and seed;
- machine, power mode and background-load controls;
- warm-up policy and repetition count;
- measurement definitions.

Non-equivalent configurations must be labelled rather than presented as direct comparisons.

## 7. Stop conditions

Stop and record the run when there is a safety risk, repeated system instability, storage exhaustion, thermal or memory pressure beyond the planned limit, corrupted evidence, an unverified silent fallback, or a prerequisite failure that invalidates later measurements.

## 8. Evidence and workbook workflow

`prepare -> execute -> capture -> classify -> validate -> update registers -> update workbook -> commit and push -> continue`
