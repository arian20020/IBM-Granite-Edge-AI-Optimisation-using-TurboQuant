# Model picker routes

**Status:** Living current-state documentation
**Last reviewed:** 2026-08-03
**Reviewed implementation baseline:** `2c51bb0551cb5556e422e63c19888c1f3874d0e5`

[← File-import boundary](../README.md) · [Model Import architecture](../../README.md)

## Purpose

This folder contains the WinUI and Windows App SDK adapters that turn a user's model-format choice into a native file or folder selection.

These classes sit at a platform boundary:

```text
ModelImportPage
    → project-owned selection contract
    → Windows picker API
    → PickFileResult / PickFolderResult or null
```

Keeping them here prevents Windows picker setup from being mixed into quick-scan parsing or reusable card code.

## Responsibility boundary

### This folder owns

- the model-format selection dialog;
- the `None`, `Gguf`, and `OpenVino` selection values;
- construction of native Windows file/folder pickers;
- GGUF file-type filtering;
- returning a selected item or `null` when the user cancels.

### This folder does not own

- parser or runtime validation;
- model metadata extraction;
- page state transitions;
- storing a validated model;
- enabling Continue;
- onboarding navigation.

## Local flow

```text
ModelImportPage.ShowModelFormatSelectionAsync()
        │
        ▼
ModelFormatSelectionCard.ShowAsync()
        │
        ├── Cancel → None
        ├── GGUF   → Gguf
        └── OpenVINO → OpenVino
                         │
                         ▼
ModelImportPage chooses a picker route
        │
        ├── GgufModelFilePicker.PickGGUFAsync()
        └── OpenVINOFolderPicker.PickOpenVINOAsync()
```

A picker result is still untrusted input. Selection only identifies a path; the appropriate scanner must validate the package later.

## File inventory

### `ModelFormatSelectionCard.xaml`

Defines the content dialog that presents the available model formats and cancellation action.

The dialog is a routing choice, not a validation result. Selecting OpenVINO means only that the OpenVINO picker route was chosen.

[Open file](./ModelFormatSelectionCard.xaml)

### `ModelFormatSelectionCard.xaml.cs`

Contains both:

```csharp
ModelFormatSelection
ModelFormatSelectionCard
```

`ModelFormatSelection` values:

```text
None      → the dialog was cancelled
Gguf      → use the GGUF file route
OpenVino  → use the OpenVINO folder route
```

The dialog stores `SelectedFormat`, records the clicked choice, and calls `Hide()` to return control to the page.

Known structural debt: the enum and dialog class currently share one code-behind file. If the selection contract gains additional consumers, moving the enum to its own file would make the boundary clearer.

[Open file](./ModelFormatSelectionCard.xaml.cs)

### `GgufModelFilePicker.cs`

Creates `Microsoft.Windows.Storage.Pickers.FileOpenPicker` using the current application window ID.

Contract:

```csharp
AllowedFileTypes = [".gguf"]
PickGGUFAsync() → PickFileResult?
```

The picker allows one file and filters the visible/selectable file types to `.gguf`. A cancelled picker returns `null`.

The file extension filter is a selection aid, not proof that the file contains valid GGUF data.

[Open file](./GgufModelFilePicker.cs)

### `OpenVINOFolderPicker.cs`

Creates `Microsoft.Windows.Storage.Pickers.FolderPicker` using the current application window ID and returns one selected folder or `null`.

Current contract:

```csharp
PickOpenVINOAsync() → PickFolderResult?
```

The class does not currently verify:

- required `.xml` model structure;
- corresponding `.bin` weights;
- tokenizer assets;
- OpenVINO runtime compatibility.

[Open file](./OpenVINOFolderPicker.cs)

## Platform dependencies

These adapters depend on:

```text
Microsoft.Windows.Storage.Pickers
App.MainWindow.AppWindow.Id
WinUI ContentDialog
Windows desktop application lifetime
```

Because of those dependencies, tests should normally exercise their stable configuration contracts or inject replacement delegates rather than opening interactive system dialogs.

## Cancellation behavior

Cancellation is represented without an exception:

```text
Format dialog cancelled
→ ModelFormatSelection.None

File picker cancelled
→ null PickFileResult

Folder picker cancelled
→ null PickFolderResult
```

`ModelImportPage` treats those values as “no new selection” and returns without starting a scan.

## Tests and evidence

Key tests:

- [`ModelFilePickerTests.cs`](../../../../../tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/FileImport/ModelFilePickerTests.cs)

The page's injected format and picker delegates are covered by:

- [`ModelImportPageStateMachineTests.cs`](../../../../../tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/Controls/ModelImportPageStateMachineTests.cs)

## Implemented now

- format-choice dialog;
- explicit cancellation value;
- GGUF single-file picker;
- `.gguf` file-type filter;
- OpenVINO single-folder picker helper;
- null return for cancelled native selection;
- page-level dependency seams for deterministic tests.

## Not implemented and non-claims

- OpenVINO selection is not connected to a complete quick-scan route;
- no OpenVINO folder/package validation occurs here;
- no drag-and-drop adapters exist;
- no file hashing or integrity verification occurs in the picker;
- picker selection does not prove that a model is safe or supported.

## Change checklist

When adding or changing a picker route, review:

1. `ModelFormatSelection`.
2. `ModelFormatSelectionCard.xaml` and code-behind.
3. The route-specific picker.
4. `ModelImportPage` orchestration.
5. `ModelQuickScanner` routing.
6. Picker, page, and scanner tests.
7. Parent and quick-scan READMEs.

## Related documentation

- [File-import boundary](../README.md)
- [Model Import architecture](../../README.md)
- [Quick-scan architecture](../../QuickScan/README.md)
