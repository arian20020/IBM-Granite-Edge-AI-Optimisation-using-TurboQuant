# Optimisation Progress and Chat Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Centre the optimisation journey, show truthful animated progress, remove onboarding chrome from chat, and support Enter-to-send across GGUF and OpenVINO.

**Architecture:** Preserve the existing optimisation coordinator and route-specific chat backends. Make presentation changes at the WinUI page/control boundary and centralise shell stage-indicator visibility at navigation transitions so GGUF and OpenVINO follow the same rule.

**Tech Stack:** C# 12, .NET 8, WinUI 3 XAML, MSTest packaged UI tests.

**Spec:** `docs/superpowers/specs/2026-08-27-optimization-progress-and-chat-polish-design.md`

## Global Constraints

- Do not change optimisation calculations, execution plans, worker launch, artifact publication, or inference behaviour.
- Only the coordinator-provided active stage may animate.
- Plain Enter sends; Shift+Enter adds a newline.
- The onboarding indicator is hidden only for chat and restored for onboarding.
- Do not expose local paths or prompt content.
- Do not repeatedly launch the application or open visible PowerShell windows.

---

### Task 1: Centred optimisation layout and active-stage animation

**Files:**
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/OptimizationResponsiveLayoutTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/OptimizationProgressCardTests.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/OptimizationPage.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Controls/OptimizationProgressCard.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Controls/OptimizationProgressCard.xaml`

**Interfaces:**
- Consumes: `OptimizationPresentationState.ProgressRows` and `OptimizationStageStatus.Active`.
- Produces: an explicitly centred `OptimizationContentHost` and exactly one active `ProgressRing` for a normal running state.

- [ ] **Step 1: Write failing packaged UI tests**

Assert that the content host is explicitly centred, that the scroll viewer centres its content, and that the `progress-optimise` fixture renders one active `ProgressRing` with an in-progress automation name while other rows render none.

- [ ] **Step 2: Run focused tests and verify RED**

Run the packaged test project with filters for `OptimizationResponsiveLayoutTests` and `OptimizationProgressCardTests`. Expect failures because the host currently stretches and the active row is a static dot.

- [ ] **Step 3: Implement the minimal WinUI change**

Centre the responsive host without removing vertical scrolling. Replace only the active row's static glyph with a `ProgressRing`, keep the textual status, and add concise duration guidance beneath the progress-card introduction.

- [ ] **Step 4: Run focused tests and verify GREEN**

Run both focused classes again and require non-zero discovered tests with no failures.

### Task 2: Hide onboarding chrome while chatting

**Files:**
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/OnboardingShellPageTests.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.xaml.cs`

**Interfaces:**
- Consumes: the shell's existing GGUF `ShowGgufChat` and OpenVINO chat activation paths.
- Produces: one shell-owned method that switches the stage indicator between onboarding-visible and chat-collapsed states.

- [ ] **Step 1: Write failing shell visibility tests**

Assert that the indicator is visible for initial model import, collapses for a chat destination, and is restored when fresh model import navigation succeeds.

- [ ] **Step 2: Run the shell tests and verify RED**

Run `OnboardingShellPageTests`; expect chat visibility assertions to fail because the indicator is permanently visible.

- [ ] **Step 3: Implement one shared visibility rule**

Collapse the indicator at both GGUF and OpenVINO chat success boundaries. Restore it at every entry into an onboarding stage, including fresh model import and returns from optimisation.

- [ ] **Step 4: Run the shell tests and verify GREEN**

Require all `OnboardingShellPageTests` to pass.

### Task 3: Enter-to-send for both chat routes

**Files:**
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Controls/ChatComposerTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/OpenVinoPromptSurfaceTests.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatComposer.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatComposer.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.OpenVino.cs`

**Interfaces:**
- Consumes: the existing `TrySubmitPrompt` guard for GGUF and `PromptSendButton_Click` submission path for OpenVINO.
- Produces: preview-key handlers that consume plain Enter only when submission is attempted and leave Shift+Enter to the text box.

- [ ] **Step 1: Write failing keyboard-submission tests**

Exercise a testable key-decision/submission method for each route. Assert plain Enter submits once, Shift+Enter does not submit, empty input does not submit, and generation-disabled input remains guarded.

- [ ] **Step 2: Run focused chat tests and verify RED**

Run `ChatComposerTests` and `OpenVinoPromptSurfaceTests`; expect failures because the preview submission paths do not yet exist.

- [ ] **Step 3: Implement preview key handling**

Move GGUF handling from bubbling `KeyDown` to `PreviewKeyDown`. Add the equivalent OpenVINO preview handler and route it through the same logic as its Send button without duplicating prompt execution.

- [ ] **Step 4: Run focused chat tests and verify GREEN**

Require both focused classes to pass with non-zero discovered counts.

### Task 4: Regression verification and one controlled application check

**Files:**
- No production files beyond Tasks 1-3.

**Interfaces:**
- Consumes: the completed presentation and input fixes.
- Produces: evidence that the integrated journey still builds and behaves consistently.

- [ ] **Step 1: Run packaged feature suites**

Run filters for `ModelOptimization`, `Onboarding`, `GgufRuntime`, and `OpenVinoPromptSurfaceTests`, requiring non-zero tests and no failures.

- [ ] **Step 2: Build the self-contained x64 app**

Build the application using the existing verified command and require zero errors.

- [ ] **Step 3: Run repository checks**

Run `git diff --check` and inspect the exact changed-path list for unrelated files.

- [ ] **Step 4: Perform one controlled manual inspection**

Close only the exact existing app process if still running, launch exactly one new instance from the verified build, and confirm centred optimisation presentation, one active spinner, hidden chat stepper, Enter-to-send, and Shift+Enter newline behaviour.

- [ ] **Step 5: Commit the implementation**

Commit only the scoped source and test files with a focused message.

