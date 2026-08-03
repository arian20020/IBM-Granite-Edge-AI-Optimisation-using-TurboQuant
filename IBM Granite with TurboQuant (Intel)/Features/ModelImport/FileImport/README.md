# Model file-import boundary

**Status:** Living current-state documentation  
**Last reviewed:** 2026-08-03  
**Reviewed implementation baseline:** `2c51bb0551cb5556e422e63c19888c1f3874d0e5`

[← Model Import architecture](../README.md)

## Purpose

This folder groups the user-selection boundary between `ModelImportPage` and the Windows picker APIs.

It exists so model-format choice and platform picker details remain separate from:

- page state orchestration;
- quick-scan parsing;
- imported-card presentation;
- onboarding navigation.

The current implementation files are held in the child [`PickerRoute`](./PickerRoute/README.md) folder.

## Responsibility boundary

### This folder owns conceptually

- asking which model-package format the user wants to import;
- selecting the native picker route for that format;
- returning a selected file or folder reference;
- representing user cancellation as no selection.

### This folder does not own

- validating the selected model package;
- deciding whether Continue becomes enabled;
- retaining the current model as validated state;
- loading model tensors or runtimes;
- navigating to Model Inspection.

## Selection flow

```text
ImportModelCard raises BrowseFilesRequested
        │
        ▼
ModelImportPage opens ModelFormatSelectionCard
        │
        ├── None
        │      └── return without changing the current selection
        │
        ├── GGUF
        │      └── GgufModelFilePicker
        │              └── returns one .gguf file or null
        │
        └── OpenVINO
               └── OpenVINOFolderPicker exists
                       └── full import route is not connected yet
```

After selection, `ModelImportPage` decides what validation route to call. The picker layer does not interpret model contents.

## Child folder

### [`PickerRoute`](./PickerRoute/README.md)

Contains:

- the model-format selection dialog;
- the `ModelFormatSelection` enum;
- the native GGUF file picker;
- the native OpenVINO folder picker.

The child README documents every file, platform dependency, cancellation behavior, tests, and current OpenVINO limitation.

## Current contract with `ModelImportPage`

The page injects or uses delegates with these conceptual shapes:

```text
Select format asynchronously
    → ModelFormatSelection

Pick GGUF asynchronously
    → selected path or null
```

Test constructors replace these native actions with deterministic delegates. This prevents unit and UI-thread tests from opening real Windows dialogs.

## Implemented now

- model-format selection between GGUF, OpenVINO, and cancellation;
- native single-file selection restricted to `.gguf` for the GGUF route;
- native single-folder selection helper for OpenVINO;
- null/no-selection handling;
- test seams in `ModelImportPage` for deterministic selection.

## Not implemented and non-claims

- selecting OpenVINO does not currently enter a complete scan and validation workflow;
- the picker layer does not verify that an OpenVINO folder contains a valid `.xml` and `.bin` pair;
- no drag-and-drop route is connected;
- no recommended download is routed through these pickers;
- picker success does not mean model validation succeeded.

## Tests and evidence

Key picker tests:

- [`ModelFilePickerTests.cs`](../../../../tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/FileImport/ModelFilePickerTests.cs)

Page-state and injected picker behavior are also covered by:

- [`ModelImportPageStateMachineTests.cs`](../../../../tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/Controls/ModelImportPageStateMachineTests.cs)
- [`ModelImportNavigationRequestTests.cs`](../../../../tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelImportNavigationRequestTests.cs)

## Known limitations and change hazards

- adding a new format requires coordinated updates to the selection dialog, selection enum, picker route, scanner router, page orchestration, tests, and documentation;
- native picker classes currently depend on `App.MainWindow.AppWindow.Id`, so they are application-platform adapters rather than portable domain services;
- a selected file or folder must remain untrusted until the appropriate scanner validates it;
- OpenVINO picker existence must not be confused with OpenVINO import support.

## Related documentation

- [Picker-route implementation](./PickerRoute/README.md)
- [Quick-scan architecture](../QuickScan/README.md)
- [Model Import controls](../Controls/README.md)
