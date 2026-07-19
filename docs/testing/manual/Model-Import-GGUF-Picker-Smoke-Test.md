# Model Import GGUF Picker Smoke Test

## Record

| Field | Value |
|---|---|
| Test ID | MAN-MODEL-IMPORT-GGUF-001 |
| Requirement | Verify visible model-format dialog and Windows GGUF picker integration. |
| Preconditions | Windows desktop session; application installed or launched from a verified x64 build; one known `.gguf` file and one unsupported file available. |
| Exact application build | To be recorded by tester. |
| Exact Windows version | To be recorded by tester. |
| Tester | To be recorded by tester. |
| Date/time | To be recorded by tester. |
| Evidence path | To be recorded by tester. |
| Actual result | Not executed. |
| Pass/fail | Not executed. |

## Steps and expected results

1. Launch the application.
   - Expected: `ModelImportPage` appears and the application remains responsive.
2. Inspect **Continue to model inspection**.
   - Expected: the button is disabled.
3. Select **Browse files**.
   - Expected: `ModelFormatSelectionCard` appears above the unchanged import page.
4. Select **Cancel**.
   - Expected: the dialog closes, no picker opens, no model path is retained, and Continue remains disabled.
5. Open the format dialog again and select **GGUF**.
   - Expected: the format dialog closes and the Windows `FileOpenPicker` appears.
6. Browse to the unsupported file.
   - Expected: the unsupported extension cannot be selected. Extension filtering is only a usability restriction and does not validate GGUF contents.
7. Cancel the Windows picker.
   - Expected: control returns safely to `ModelImportPage`; no crash or freeze occurs; Continue remains disabled.
8. Open the format dialog again, select **GGUF**, and select the known `.gguf` file.
   - Expected: the application returns safely, retains the selected path, and keeps Continue disabled until later model validation.
9. Repeat the dialog using keyboard navigation and activate each action with the keyboard.
   - Expected: focus remains visible, actions are reachable, and the application remains responsive.

## Evidence guidance

Capture the application commit, build configuration, Windows version, and screenshots or screen recording showing the dialog, picker filter, cancellation result, selected-file return, and disabled Continue button. Do not mark this record passed until every step has been executed and the evidence path is populated.

