# Optimisation Progress and Chat Polish Design

## Status

Approved by the user on 27 August 2026.

## Goal

Make the live optimisation journey visibly centred and trustworthy, then remove onboarding-only chrome from the resulting chat experience and make keyboard submission behave like a modern chat composer.

## Scope

- Keep GGUF and OpenVINO optimisation execution, plans, artifacts, and inference backends unchanged.
- Centre the optimisation page through an explicitly centred responsive content host.
- Render a WinUI `ProgressRing` only for the active optimisation row. Completed, waiting, and not-applicable rows retain static status glyphs.
- Explain that optimisation can take several minutes and that the app should remain open.
- Hide the onboarding stage indicator whenever either route is presenting chat. Restore it before any onboarding page is shown again.
- In both GGUF and OpenVINO chat inputs, plain Enter submits and Shift+Enter inserts a newline.

## Behaviour

The optimisation coordinator remains the only source of active-stage truth. The UI maps `OptimizationStageStatus.Active` to an animated `ProgressRing`; it does not start an independent timer or infer progress. The page remains vertically scrollable and its content fills the available width up to the established maximum while remaining horizontally centred.

Chat is the destination after onboarding rather than another onboarding form. The shell therefore keeps its `Frame` but collapses the stage indicator while chat content is active. Every transition back to model import or another onboarding stage restores the indicator. This avoids moving either route to a second navigation system.

Keyboard submission is captured during preview key handling so the multiline `TextBox` cannot consume plain Enter first. Shift+Enter is deliberately left unhandled for multiline input. Empty prompts and prompts submitted during generation remain rejected by the existing shared submission guard.

## Accessibility and safety

- The active `ProgressRing` has an automation name containing the stage title and in-progress state.
- The row retains a textual `In progress` label, so animation is not the sole status signal.
- Motion is limited to the single active row.
- No model path, prompt, hardware detail, or execution data is added to diagnostics.
- Chat launch/disposal and optimisation cancellation semantics remain unchanged.

## Verification

- Packaged WinUI tests cover centred responsive layout, one active progress ring, shell visibility transitions, and Enter versus Shift+Enter behaviour.
- Existing optimisation, onboarding, GGUF chat, and OpenVINO prompt tests remain green.
- The application is built once after focused tests; it is launched at most once for final manual inspection.

