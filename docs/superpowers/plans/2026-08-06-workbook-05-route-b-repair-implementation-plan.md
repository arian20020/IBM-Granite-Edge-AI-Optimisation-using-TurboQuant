# Workbook 05 Bounded Route B Repair Implementation Plan

> Execute this plan only after Checkpoint B0 approval. Do not begin a later task until the previous task and its evidence have been reviewed.

**Goal:** Correct only the confirmed Route B build/test exposure defects so the existing QJL and PolarQuant candidate paths can be built and tested honestly.

**Base source:** `EgorDuplensky/openvino` at `1827f6458d049de11c1a8203c793af67c99935dc`.

**Prohibited outcome:** This plan must not convert source presence into a support claim. It does not authorise Granite model testing.

## Task BR1 — Freeze the external repair workspace

1. Create a separate external Git worktree from the exact base commit.
2. Verify origin, exact HEAD, clean state and recursive submodules.
3. Record the compiler, CMake, Git, Python, Windows SDK and free-space controls.
4. Refuse unexpected existing data; do not reset or clean it automatically.

**Checkpoint BR1:** Exact provenance and workspace evidence pass independent validation.

## Task BR2 — Reproduce the source-discovery defect

1. Add a focused failing test for repeated `GLOB_RECURSE` assignments.
2. Configure the narrow functional-test target without compiling it.
3. Capture generated target metadata and the actual source list.
4. Prove which intended architecture/common sources are absent.

**Checkpoint BR2:** Red evidence reproduces `RB-SRC-001` on the exact source.

## Task BR3 — Repair source-list accumulation

1. Use distinct temporary variables for each glob.
2. Combine them with explicit `list(APPEND ...)` operations.
3. Keep platform branches and existing source paths intact.
4. Reconfigure and prove that every intended source is in generated metadata.

**Checkpoint BR3:** The source-discovery regression test passes and generated target membership is complete.

## Task BR4 — Guarantee a real baseline test

1. Reproduce the empty-precision-list condition.
2. Require a default/f32 entry even when optional bf16/fp16 hardware features are absent.
3. Keep optional reduced-precision cases conditional.
4. Record discovered test names and require a count greater than zero.

**Checkpoint BR4:** At least one f32 baseline is discovered on the target laptop.

## Task BR5 — Correct the asymmetric test definition

1. Reproduce the mismatch between the asymmetric test name and its actual K/V configuration.
2. Change only the test parameters so the requested K and V modes match the name.
3. Add an assertion or diagnostic that records requested and selected K/V codecs independently.

**Checkpoint BR5:** The corrected asymmetric row requests and observes the intended K/V pair.

## Task BR6 — Build the narrow repair targets

1. Follow the exact pinned Windows build instructions.
2. Build only the required CPU functional-test and codec targets first.
3. Capture command, environment, timestamps, exit code, stdout, stderr, warnings, elapsed time, peak build memory and output hashes.
4. Do not build a model application or start inference.

**Checkpoint BR6:** Required targets compile and all outputs are traceable to exact commands.

## Task BR7 — Execute repository conformance tests

1. Run the non-empty f32 baseline.
2. Run QJL 3-bit and 4-bit K/V cases that are actually discovered.
3. Run PolarQuant 3-bit and 4-bit K/V cases that are actually discovered.
4. Run corrected asymmetric cases.
5. Record raw output, selected K/V codecs, record bytes, fallback result and exit status.
6. Preserve failures and skips; do not loosen thresholds during this task.

**Checkpoint BR7:** Each intended case is Passed, Failed, Blocked or Not applicable with raw evidence and non-zero discovery.

## Task BR8 — Validate and decide

1. Package only text, JSON, logs, manifests and hashes.
2. Reject executables, libraries, archives, models, source trees and secrets.
3. Validate the bundle on a clean hosted runner without executing its contents.
4. Reclassify Route B based on the validated evidence.

**Checkpoint BR8:** Present one of these decisions for user approval:

- `Executable candidate — proceed to documented build and activation proof`;
- `Blocked — repair unsuccessful`;
- `Blocked — deeper implementation defect discovered`.

No Granite model or formal performance/quality test begins under this repair plan.
