# Model Import Picker Testing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement deterministic WinUI and picker-configuration tests for the model-import flow, plus honest manual coverage for operating-system UI.

**Architecture:** Convert the existing test project to the official WinUI Unit Test App structure. Add two internal delegates and an awaitable browse method directly to `ModelImportPage`; expose immutable picker-filter configuration without changing `PickGGUFAsync()`.

**Tech Stack:** C# 12, .NET 8, WinUI 3, Windows App SDK 2.2, MSTest 4.

## Global Constraints

- Preserve `public async Task<PickFileResult?> PickGGUFAsync()`.
- Do not add a workflow, service, ViewModel, Core project, interface hierarchy, or mocking framework.
- Never launch a real operating-system picker from an automated test.
- Keep XAML event-handler names synchronized with code-behind.

---

### Task 1: Establish WinUI test hosting

**Files:**
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/UnitTestApp.xaml`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/UnitTestApp.xaml.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/UnitTestAppWindow.xaml`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/UnitTestAppWindow.xaml.cs`

- [ ] Convert the project to the official packaged WinUI test-app properties and packages.
- [ ] Add a project reference to the application and expose a UI-thread dispatcher.
- [ ] Build the test project and resolve only concrete template integration errors.

### Task 2: Page initial and orchestration state

**Files:**
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/FileImport/ModelFilePickerTests.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelImportPage.xaml.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Properties/AssemblyInfo.cs`

- [ ] RED: write UI-thread tests for initial state and awaitable browse behaviour.
- [ ] Run the narrow test selection and confirm missing test seams are the failure reason.
- [ ] GREEN: add internal format/path delegates and `BrowseFilesAsync()` with production defaults.
- [ ] Run the selected tests and the complete test project.

### Task 3: Dialog selection behaviour

**Files:**
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/FileImport/ModelFilePickerTests.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/FileImport/ModelFormatSelectionCard.xaml.cs`

- [ ] RED: add tests for initial, cancel, GGUF, and OpenVINO format states.
- [ ] GREEN: route existing handlers through minimal internal selection methods if direct button activation cannot be awaited deterministically.
- [ ] Verify selected-format state and page state separately from visual rendering.

### Task 4: Picker result and configuration behaviour

**Files:**
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/FileImport/ModelFilePickerTests.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/FileImport/GgufModelFilePicker.cs`

- [ ] RED: add cancellation, selected-path, replacement-path, and filter tests.
- [ ] GREEN: expose the immutable extension list and apply it in the real picker.
- [ ] Verify null does not change state and selection never enables Continue.

### Task 5: Manual and CI evidence

**Files:**
- Create: `docs/testing/manual/Model-Import-GGUF-Picker-Smoke-Test.md`
- Modify: `.github/workflows/build-and-test.yml`
- Modify: `IBM Granite with TurboQuant (Intel).slnx`

- [ ] Add the exact manual smoke steps and blank actual-result fields.
- [ ] Update CI commands only as required by the converted test project.
- [ ] Restore, build, run tests with TRX, inspect the final diff, and repeat conflict/duplicate scans.
