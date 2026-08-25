# Hardware Inspection Presentation Contract v1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement a pure, immutable presentation factory for all 15 approved Hardware Inspection states and exact V0 copy/actions.

**Architecture:** A copy catalog owns the exact seven-stage and outcome strings. An immutable state model carries only bounded display semantics. One factory validates combinations and derives rows/actions/counts so XAML never contains outcome policy.

**Tech Stack:** C# 12, .NET 8, MSTest, existing packaged WinUI test runner.

---

### Task 1: Lock all approved states with RED tests

**Files:**
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/HardwareInspectionPresentationContractTests.cs`

- [ ] Assert the seven exact ordered stages and their active/completed/waiting copy.
- [ ] Assert seven active fixtures have exactly one active row and truthful completed counts from zero through six.
- [ ] Assert exact invalid, stopping, completed, warning, three failure, and cancelled copy.
- [ ] Assert action visibility/enabled rules, including the two-condition Continue predicate.
- [ ] Assert the canonical warning counts one unresolved review item and one resolved informational note.
- [ ] Build and capture failure because presentation types do not exist.

### Task 2: Implement immutable models and copy catalog

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Presentation/State/HardwareInspectionPresentationEnums.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Presentation/State/HardwareInspectionPresentationModels.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Presentation/State/HardwareInspectionCopyCatalog.cs`

- [ ] Add exact enums with no extra members.
- [ ] Add constructor-validated immutable action, stage-row, and screen-state types that defensively copy collections.
- [ ] Add all V0 strings once in the catalog; use British-English `Normalising` and exact punctuation.
- [ ] Build and verify only the factory tests remain red.

### Task 3: Implement the factory

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Presentation/Factories/HardwareInspectionPresentationFactory.cs`

- [ ] Implement invalid-handoff, active-stage, stopping, terminal-success, failed, and cancelled creators.
- [ ] Reject undefined enum values and invalid failure combinations.
- [ ] Derive rows, action state, report/details state, review counts, announcement, and focus key.
- [ ] Run the packaged VSTest recipe from `tests/README.md` filtered to `HardwareInspectionPresentationContractTests`; expect all tests to pass.
- [ ] Commit implementation and tests.

### Task 4: Verify scope and regression

- [ ] Run the full packaged UnitTests recipe; expect zero failures.
- [ ] Run the Hardware contract Python binding test; expect pass.
- [ ] Run `git diff --check origin/main...HEAD` and confirm only Hardware code/tests plus approved design/plan documents changed.
- [ ] Commit documentation updates, leave a clean worktree, and report explicit non-claims: no XAML, navigation, provider, compatibility, workflow, laptop, or candidate action.
