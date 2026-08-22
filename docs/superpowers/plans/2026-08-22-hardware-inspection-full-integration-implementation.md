# Hardware Inspection Full Integration Implementation Plan

> **For Codex:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Integrate the pinned Hardware Inspection functional and LLM Fit Gate histories into the pinned mainline while preserving the repaired Intel Stage A workflow, the schema-v2 model handoff boundary, and the prohibition on production Block 3/Continue activation.

**Architecture:** Preserve history with conventional merge commits. Treat Model Inspection handoff data as opaque at the Hardware Inspection layer, keep the production hardware service fail-closed until an approved coordinator exists, and retain Gate 1 work as deterministic tooling/evidence only. Close integration regressions with focused tests before changing production code.

**Tech Stack:** C#/.NET 8, WinUI 3, MSTest/VSTest, PowerShell, Python contract tests, GitHub Actions.

---

## Task 1: Record provenance and preservation constraints

**Files:**
- Create: `docs/reviews/2026-08-22-hardware-inspection-integration-preservation-matrix.md`
- Verify: `docs/superpowers/handoffs/2026-08-22-intel-hardware-full-integration-package.md`

1. Record the exact starting, functional, Gate, visual, contract, and decision SHAs from the verified package.
2. Record the package manifest SHA-256 and note that all manifest-listed files were verified before implementation.
3. List the invariants that must survive both merges: Stage A shell formatting, six-field schema-v2 handoff, separate UUID roles, claim/invalidate/reissue lifecycle, provider opacity, fail-closed production service, and inactive Block 3/Continue.
4. Commit the plan and preservation matrix.

Verification:

```powershell
git status --short
git log -4 --oneline
```

Expected: only the two new integration documents are included in the planning commit; the approved handoff documents remain separate cherry-picked commits.

## Task 2: Establish executable baselines

**Files:**
- Test: `tests/testing/hardware_inspection/test_intel_runner_stage_a_contract.py`
- Test: `tests/testing/hardware_inspection/test_intel_runner_stage0_contract.py`
- Inspect: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/*.cs`
- Inspect: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Contracts/*.cs`
- Inspect: `tools/HardwareInspection.LlmFitSpike.Tests/**` if present on the Gate branch

1. Run the Stage 0 and Stage A Python contract suites at the pinned mainline baseline.
2. Locate Visual Studio MSBuild and VSTest. Record unavailable prerequisites honestly; do not substitute `dotnet test` for packaged WinUI execution.
3. Run deterministic managed test projects where their required runner exists.
4. Append command, result, and test count evidence to the preservation matrix.

Verification:

```powershell
python -m pytest tests/testing/hardware_inspection/test_intel_runner_stage0_contract.py tests/testing/hardware_inspection/test_intel_runner_stage_a_contract.py -q
```

Expected: the mainline Intel runner contract tests pass before either source merge.

## Task 3: Merge the functional Hardware Inspection history

**Files:**
- Merge: `origin/feat/hardware-inspection-page-v1-functional`
- Preserve: `.gitattributes`
- Review: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/**`
- Review: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Application/ModelInspectionHandoff*.cs`
- Review: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.xaml.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/**`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Contracts/**`

1. Merge the exact functional SHA `f521e9eea81b59f5814fcf100e4f527391ee67d2` with `--no-ff`.
2. Resolve `.gitattributes` additively, retaining mainline policies and functional additions.
3. Inspect the merge diff for accidental model-data access in Hardware Inspection and for any production service other than the unavailable fail-closed implementation.
4. Re-run Stage 0/Stage A contract tests.
5. Build and run the focused packaged WinUI tests when MSBuild/VSTest are available.
6. Update the preservation matrix with the merge commit and verification evidence.

Focused packaged test filter:

```text
FullyQualifiedName~Features.HardwareInspection|FullyQualifiedName~Features.ModelInspection.Contracts|FullyQualifiedName~OnboardingHardwareInspectionNavigationTests
```

Expected: functional UI, handoff, and lifecycle behavior are present; Stage A remains green; production hardware execution remains fail-closed.

## Task 4: Merge the deterministic LLM Fit Gate history

**Files:**
- Merge: `origin/test/hardware-inspection-llmfit-gate1`
- Resolve: `.gitattributes`
- Resolve: `docs/superpowers/specs/2026-08-18-hardware-inspection-intel-runner-configuration-design.md`
- Resolve: `scripts/README.md`
- Review: `tools/HardwareInspection.LlmFitSpike/**`
- Review: `tools/HardwareInspection.LlmFitSpike.Tests/**`
- Review: `docs/testing/runbooks/Hardware-Inspection-LLM-Fit-Gate-1-Runbook.md`

1. Merge the exact Gate SHA `cc2e57ceb94e73e49f34fc383d5440a9047fba21` with `--no-ff`.
2. Resolve overlapping documentation additively and retain the latest repaired mainline Stage A instructions.
3. Confirm the Gate merge does not register runners, acquire models, execute Stage A/B/C/D, make network changes, or activate product Block 3.
4. Run deterministic LLM Fit tests if the managed runner is available.
5. Re-run the mainline Stage 0/Stage A contract suites.
6. Update the preservation matrix with the merge commit and results.

Expected: deterministic Gate assets and blocked evidence are preserved without converting them into an operational gate claim.

## Task 5: Audit the combined product boundary with TDD

**Files:**
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/HardwareInspectionContractTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/HardwareInspectionJourneyTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Contracts/ModelInspectionHandoffTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Contracts/ModelInspectionHandoffRegistryTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/OnboardingHardwareInspectionNavigationTests.cs`
- Modify only if a failing test proves a gap: corresponding production file under `IBM Granite with TurboQuant (Intel)/Features/**`

1. Add or strengthen a focused failing test only for an observed combined-tree gap.
2. Prove the test fails for the intended reason.
3. Make the smallest production change that restores the approved contract.
4. Re-run the focused test and then the complete relevant suite.
5. Refactor only while tests remain green.

Required assertions:

- schema v2 has exactly six canonical fields and serializes to no more than 512 bytes;
- handoff, model-run, and product-hardware-run identifiers retain distinct roles;
- stale or replayed claims are rejected and retries reissue identity correctly;
- Hardware Inspection does not interpret model format, GGUF, or OpenVINO data;
- unavailable provider paths remain visible and recoverable but cannot emit a completed handoff;
- Continue is never enabled outside completed/warning plus all six approved conditions.

Expected: no speculative coordinator or provider is added merely to make the combined tree appear complete.

## Task 6: Verify visual and accessibility preservation

**Files:**
- Inspect: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/HardwareInspectionPage.xaml`
- Inspect: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Presentation/**`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/**`
- Reference: handoff ZIP `visual-references/native-approved/**`
- Reference: handoff ZIP `visual-references/polished-pack/**`

1. Compare the combined XAML and presentation factories against the modern light visual family and the later native-approved corrections.
2. Run source-level presentation, state, details, and journey tests.
3. If native packaged execution is available, capture Debug/x64 states at wide and compact sizes and inspect keyboard/focus, 200% text scale, high contrast, and long-copy wrapping.
4. Do not claim native visual closure from source inspection alone.
5. Record verified and blocked checks separately in the preservation matrix.

Expected: visual deltas are supported by native evidence, or clearly recorded as pending when this machine cannot produce it.

## Task 7: Final combined-tree verification

**Files:**
- Update: `docs/reviews/2026-08-22-hardware-inspection-integration-preservation-matrix.md`

1. Run whitespace and conflict-marker checks.
2. Run all available deterministic and packaged tests relevant to both merged histories.
3. Confirm the working tree contains no generated evidence, acquired model, runner registration, credential, machine path, or operational Gate claim.
4. Review the final diff from pinned mainline and document every unavailable verification prerequisite.
5. Use `superpowers:verification-before-completion`, then `superpowers:requesting-code-review`, and finally `superpowers:finishing-a-development-branch`.

Verification:

```powershell
git diff --check origin/main...HEAD
rg -n "^(<<<<<<<|=======|>>>>>>>)" --glob '!docs/superpowers/plans/*'
git status --short --branch
```

Expected: a clean integration branch with traceable merge history, passing available tests, and no prohibited operational side effects.

