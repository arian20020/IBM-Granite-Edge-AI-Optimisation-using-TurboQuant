# Model Import Picker Testing Design

## Goal

Verify the model-format and GGUF selection flow at the lowest test level that can genuinely observe each behaviour, while preserving the existing WinUI classes and `GgufModelFilePicker.PickGGUFAsync()` contract.

## Chosen approach

Convert the existing MSTest project into the Microsoft WinUI Unit Test App shape. This permits `[UITestMethod]` tests to construct `ModelImportPage` and `ModelFormatSelectionCard` on a XAML UI thread. The application keeps its current page, dialog, and picker responsibilities.

`ModelImportPage` receives two internal delegate seams: one returns the format selected by the real dialog, and one returns the path produced by the real GGUF picker. Its public constructor wires those delegates to the existing UI and picker implementations. An internal async method contains the browse orchestration so tests can await it without testing an `async void` event handler. This is smaller than adding a service, ViewModel, workflow class, interface hierarchy, or mocking framework.

`GgufModelFilePicker` exposes its immutable filter configuration for deterministic verification, while its real method still constructs `FileOpenPicker`, applies that configuration, and returns `PickFileResult?`.

## Test levels

- WinUI UI-thread: page initial state, browse interaction request, cancellation state, GGUF selection state, dialog selection state.
- Ordinary deterministic logic inside the WinUI test app: configured GGUF extension.
- Manual smoke: visible dialog rendering, real Windows picker display, OS-level extension restriction, cancellation, and real `PickFileResult` integration.

## State and flow

The page starts with no selected path, no validated model, and Continue disabled. Browse awaits the selected format. `None` and deferred `OpenVino` leave state unchanged. `Gguf` awaits a path; null leaves state unchanged, while a non-null path is retained. File selection never marks the model validated, so Continue remains disabled.

## Error and cancellation handling

Dialog and picker cancellation are normal null/`None` results and do not throw. Unexpected picker exceptions remain observable; they are not swallowed. Repeated successful selection replaces the retained path. Cancelling after a prior selection leaves the prior selection unchanged because the product requirement does not define clearing it.

## CI and manual evidence

CI continues to use Windows. It builds the packaged WinUI test app and attempts its supported test command, retaining TRX output. If the hosted runner cannot execute UI-thread tests interactively, that limitation will be explicit rather than counted as automated coverage. A manual smoke record remains unexecuted until a tester supplies actual results and evidence.

## Alternatives rejected

- Source-text assertions: do not prove runtime behaviour.
- A new Core library or workflow/service/ViewModel: explicitly prohibited and disproportionate.
- Real `FileOpenPicker` in unit tests: nondeterministic and requires user interaction.

