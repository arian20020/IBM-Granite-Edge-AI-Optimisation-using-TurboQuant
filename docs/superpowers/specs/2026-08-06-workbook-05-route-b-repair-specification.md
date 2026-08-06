# Workbook 05 Route B Disposition and Bounded Repair Specification

## Status

**R7 classification:** `Blocked — fixable source/build exposure`

**Checkpoint:** `B0 — project-owner approval required`

**Campaign:** `GTQ-WB05-MF-v1`

**Route:** `route-b-experimental-qjl-polar`

**Experimental OpenVINO commit:** `1827f6458d049de11c1a8203c793af67c99935dc`

**Experimental base commit:** `7de5a4fbb178a1de43f6bc3cccc95ff656abed05`

**Upstream pull request:** `openvinotoolkit/openvino#35092`

**Controlling Phase 1 evidence:** GitHub Actions run `31058926201`, attempt `2`, artifact `workbook-05-source-admission-31058926201-2`, artifact ID `8952040643`, SHA-256 `04c584f69acc8c2939337122fb7e00399bd852cd68c9b45af564575969c21393`.

This specification is intentionally separate from PR `#46`. It records the R7 decision and defines a possible repair package. It does not modify OpenVINO source, start a build, run a model, or claim that QJL or PolarQuant works.

---

## 1. R7 decision

Route B is not admitted for the documented-build stage in its current state.

It is also not classified as `Blocked — implementation/selectability absent` because the exact pinned source contains all of the following candidate interfaces:

- public internal codec values for TurboQuant+QJL and PolarQuant;
- separate key-cache and value-cache codec properties;
- CPU-plugin property parsing for the requested codecs;
- QJL cache-write code;
- QJL cache-read and correction code;
- PolarQuant cache-write code;
- PolarQuant cache-read code;
- record-size calculations for QJL and PolarQuant;
- independent K/V dispatch through the single-token SDPA path;
- functional-test configurations that request the QJL and PolarQuant modes.

These findings demonstrate that a bounded executable investigation is reasonable. They do **not** prove that the code builds, that the intended tests are included, that a model can select the codecs, that packed storage is used, or that execution avoids fallback.

The correct R7 outcome is therefore:

> **Blocked — fixable source/build exposure.** The candidate implementation and selection paths exist, but the build and test boundary is not sufficiently trustworthy to permit admission.

---

## 2. Why Route B cannot be admitted yet

### 2.1 `RB-SRC-001` — functional-test source discovery is unreliable

The pinned `target_per_test.cmake` assigns `LIST_OF_TEST_ARCH_INSTANCES` and `LIST_OF_TEST_COMMON_INSTANCES` more than once. In CMake, the later `file(GLOB_RECURSE ...)` assignment replaces the earlier list rather than appending to it.

The Phase 1 source audit therefore found source files that are at risk of being omitted. Route B was deliberately not configured, so generated target metadata was unavailable and actual target membership was not proved.

This is a bounded build/test exposure defect. It can be repaired without changing QJL, PolarQuant, TurboQuant, attention mathematics, codebooks, quantisation layouts, or runtime algorithms.

### 2.2 `RB-TEST-002` — the baseline test set can become empty

An upstream review identified that the test helper which selects activation precisions may return an empty vector on hardware without the relevant AMX bf16/fp16 capability. If this happens, `ValuesIn(precisions())` can instantiate zero tests, including the intended f32 baseline.

A green build with zero discovered tests would not be valid evidence. The repair package must guarantee a non-empty f32 baseline independently of optional bf16/fp16 cases.

### 2.3 `RB-TEST-003` — one asymmetric test does not match its name

An upstream review identified a test named as K=f32 and V=TurboQuant whose configuration sets both K and V to TurboQuant. This means the named asymmetric path is not actually covered by that row.

The repair package must make the test configuration match its name and verify the requested and actual K/V modes separately.

### 2.4 QJL remains internally contradictory

The exact codec enum exposes QJL values, while the same source comments that those values are `not yet supported`.

Other source files contain QJL quantisation, projection, sign-correction, record-size and dispatch code. Because both statements exist, the evidence is contradictory rather than absent.

The contradiction must not be removed merely to make the source look complete. It may be resolved only by executable build, activation, storage and no-fallback evidence, followed by a separately reviewed source change if the comment is then demonstrably stale.

### 2.5 The upstream work is unfinished

The pinned upstream pull request remains open and unmerged. Its author stated that the large change would be split into five pull requests. PagedAttention support and prefill compression remain outside the implemented boundary.

This does not prove that the SDPA decode implementation is invalid. It does mean that this project must treat the route as experimental and must not infer maintenance or support guarantees from the pull-request checklist.

---

## 3. Bounded repair package

The repair package is deliberately limited to making the existing candidate code buildable, discoverable and testable. It is not an algorithm-development package.

### 3.1 Permitted source changes

Only these changes are permitted in the fork-derived repair patch:

1. Replace the repeated CMake source-list assignments with separate temporary variables and explicit `list(APPEND ...)` operations.
2. Preserve all intended architecture-specific and common functional-test sources in the final target list.
3. Ensure the precision generator always includes one default/f32 baseline and adds bf16/fp16 cases only when supported.
4. Correct the misconfigured asymmetric test so its requested K and V modes match its test name.
5. Add narrowly scoped diagnostics or test assertions required to prove requested codec, selected codec, test discovery and packed record size.
6. Add or strengthen tests that fail when the repaired source-discovery or asymmetric-selection behaviour regresses.

### 3.2 Prohibited changes

The repair package must not:

- change QJL projection mathematics;
- change QJL sign-correction mathematics;
- change PolarQuant decomposition, reconstruction, centroids, boundaries or codebooks;
- change TurboQuant rotation or codebook behaviour;
- change packed record layouts or byte formulas merely to make tests pass;
- relax numeric thresholds without a separately justified decision;
- delete failing tests;
- convert skipped, undiscovered or unsupported cases into passes;
- remove the `not yet supported` QJL statement without executable proof;
- add PagedAttention or prefill compression;
- add mixed TurboQuant/PolarQuant K/V support;
- modify the project application or WinUI code;
- claim official OpenVINO support.

Any need to cross this boundary stops the repair package and requires a new design review.

---

## 4. Required implementation workflow after B0 approval

### Step 1 — freeze the repair base

Create a separate external worktree from exact commit:

```text
1827f6458d049de11c1a8203c793af67c99935dc
```

Record the origin, exact base SHA, branch name, clean status and recursive submodules before changing anything.

### Step 2 — reproduce every defect before fixing it

Capture red evidence for:

- overwritten architecture/common CMake source lists;
- missing intended sources in generated target metadata;
- an empty precision list producing zero test instances;
- the incorrectly configured asymmetric test.

No repair is accepted without a failing reproduction tied to the exact pinned source.

### Step 3 — apply the smallest test/build-only correction

Make one focused correction for each reproduced defect. Keep algorithm files unchanged unless a new review explicitly authorises otherwise.

### Step 4 — configure and inspect before full compilation

Run a Route B configure-only probe in its own build directory. Capture:

- the exact CMake command and working directory;
- environment and toolchain versions;
- stdout, stderr, timestamps and exit code;
- `CMakeCache.txt` and SHA-256;
- generated target metadata;
- the complete source list for each relevant functional-test target;
- discovered test names and counts.

The configuration gate fails if any intended QJL/Polar test source is absent or if the f32 baseline count is zero.

### Step 5 — build the narrowest required targets first

Build only the repaired functional-test and codec targets required to prove the repair. Record all commands, warnings, outputs, hashes, elapsed time and peak build memory.

A successful compilation is necessary but is not codec-activation evidence.

### Step 6 — execute conformance before any Granite model

Run repository-level tests that directly exercise:

- TurboQuant+QJL write and read paths;
- PolarQuant write and read paths;
- independent K/V selection;
- the corrected asymmetric case;
- expected packed record bytes;
- deterministic output comparison;
- unsupported-path failures.

Every run must record non-zero discovery, selected test names, exit code and raw output. Loose thresholds already present in the fork must remain visible and cannot be treated as formal model-quality evidence.

### Step 7 — decide Route B again

After independent artifact validation, classify Route B as one of:

- `Executable candidate — proceed to documented build and activation proof`;
- `Blocked — repair unsuccessful`;
- `Blocked — deeper implementation defect discovered`.

Route B still does not become fully admitted for model comparisons until build, activation, packed-storage and no-fallback gates pass.

---

## 5. Evidence and acceptance requirements

The repair package passes only when all of the following are true:

1. Exact base origin and commit are preserved.
2. The patch changes only the approved test/build exposure boundary.
3. Every defect has red evidence before its correction.
4. Generated metadata contains every intended architecture and common test source.
5. At least one f32 baseline is discovered and executed.
6. The corrected asymmetric test requests the intended K and V modes.
7. QJL and PolarQuant tests are discovered by name rather than inferred from source presence.
8. The relevant test targets compile successfully.
9. Repository tests execute with non-zero counts and preserve raw results.
10. Packed record sizes match the pinned formulas or the discrepancy blocks progression.
11. Requested and observed K/V modes are recorded separately.
12. No unsupported fallback is converted into a pass.
13. The evidence artifact passes an independent hosted validator.
14. No executable, library, archive, model, source tree or secret is committed to this project repository.
15. No OpenVINO support, model-quality, performance or Granite-compatibility claim is made from the repair alone.

---

## 6. Explicitly deferred work

The following remains outside R7 and outside the bounded repair package:

- full OpenVINO Runtime and GenAI production build planning;
- Granite model acquisition or conversion;
- Granite 3B or 8B execution;
- performance, memory or context-frontier measurement;
- formal P1-P6 quality scoring;
- perplexity measurement;
- PagedAttention codec support;
- prefill cache compression;
- mixed TurboQuant/PolarQuant K/V pairs;
- algorithm redesign;
- upstream submission or representation as official support.

---

## 7. Checkpoint B0 decision request

R7 is complete when the project owner reviews this disposition and chooses one of these paths:

1. **Approve the bounded repair package.** Proceed to a dedicated repair implementation plan and experimental-source patch before Route B build work.
2. **Decline the repair package.** Close Route B-dependent Workbook 05 rows as blocked and continue with Route A only.
3. **Request a narrower repair.** Revise this specification before touching the experimental source.

Until one path is approved, Route B remains:

```text
Blocked — fixable source/build exposure
```

No subsequent Route B build or source modification is authorised by this document alone.

---

## 8. Engineering-practice basis

This decision uses the project’s five-book engineering baseline:

- **Code Complete** — use construction checklists, explicit quality gates, defensive assumptions and focused reviews before progressing.
- **Designing Secure Software** — keep untrusted source and artifacts outside the application repository, use least privilege, validate boundaries and fail closed when evidence is incomplete.
- **The Art of Unit Testing** — reproduce defects first, require meaningful non-zero test execution and prevent false green results.
- **Why Programs Fail** — preserve observations, isolate causes and distinguish a source symptom from proved runtime behaviour.
- **Refactoring** — make the smallest behaviour-preserving structural correction and avoid mixing cleanup with algorithm changes.

The Windows/OpenVINO build stage must continue to follow the project’s `windows-apps.pdf` Windows toolchain guidance together with the exact pinned OpenVINO build documentation captured by Workbook 05.
