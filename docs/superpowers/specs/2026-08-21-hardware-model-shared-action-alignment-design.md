# Hardware and Model shared action and alignment design

**Status:** User-approved visual direction on 21 August 2026

## Goal

Correct the Hardware Inspection alignment defects visible in the approved full-page preview and make Model Inspection and Hardware Inspection action buttons use the established Model Import button grammar consistently.

This is a presentation-only change. It must not change inspection state, commands, navigation, handoffs, providers, evidence, compatibility logic, or any other backend behaviour.

## Visual authority

The approved full-page companion preview fixes the following visible outcomes:

1. `Hardware inspection` and its current subtitle are horizontally centred at every responsive width.
2. Each status symbol in the seven-row Hardware Inspection details list is vertically centred against the complete row. At compact widths, the symbol spans the title, explanation, and status lines rather than aligning only with the first text line.
3. The outer `Inspection details` header keeps its two-line title/helper hierarchy. Its information symbol and disclosure action are centred against the complete header.
4. The nested `Technical information for IT` disclosure uses the same header grammar: a centred blue information symbol, a two-line title/helper block, and a centred action/chevron.
5. The complete horizontal Technical information header is one native disclosure target. Its keyboard, pointer, focus, expanded-state, and accessibility behaviour remain owned by the native WinUI disclosure control.

Because these surfaces are shared presentation controls, the corrections apply to every Hardware Inspection terminal state that presents details, not to one failure fixture only.

## Exhaustive screen coverage

The implementation and verification must exercise the complete existing screen inventories rather than a representative subset:

- all 15 Hardware Inspection observable states: invalid/missing handoff, all seven active stages, Stopping, Completed, Completed with warnings, the three classified failure presentations, and Cancelled;
- every stable Hardware terminal that exposes Inspection details and the nested Technical information disclosure, in both collapsed and expanded combinations;
- all 13 canonical Model Inspection rendered-state fixtures already enforced by the packaged rendered-state matrix;
- every Model Inspection action-card mode, including inspecting/cancel, ready, warning, conversion-required, incomplete, unsupported, invalid, cancelled, and operational-failure presentations;
- wide, medium, compact, and representative 200% text layouts for every applicable shared component.

Screens that do not display an action or details surface remain behaviourally and visually unchanged. No implementation may special-case the screenshot's application-repair state.

## Shared journey action grammar

Model Import remains the visual source for the primary journey action:

| Property | Approved value |
|---|---|
| Normal background and border | `#2563EB` |
| Pointer-over background and border | `#1D4ED8` |
| Pressed background and border | `#1E40AF` |
| Enabled foreground | `#FFFFFF` |
| Disabled background | `#E5E7EB` |
| Disabled foreground | `#9CA3AF` |
| Disabled border | `#D1D5DB` |
| Height | 46 px |
| Corner radius | 11 px |
| Label weight | Semibold |
| Alignment | Horizontally and vertically centred |
| Keyboard focus | Native visible system focus visual |

The shared secondary journey action uses the matching neutral template:

- white surface and `#C9D7E8` border in the normal state;
- `#111827` enabled foreground;
- `#F8FAFC` pointer-over surface with a stronger neutral border;
- `#EEF2F7` pressed surface;
- the same disabled palette, 46 px height, 11 px radius, semibold label, centred content, and native visible focus behaviour as the primary action.

Buttons retain content-driven width on wide and medium layouts and use the existing full-width compact layout. The design does not impose Model Import's fixed 220 px Browse-button width on unrelated action labels.

## Scope and exclusions

The shared action grammar applies only to journey actions in:

- Model Inspection's action card, including cancel, retry, choose-another, report, and Hardware-fit slots when visible;
- Hardware Inspection's active and terminal action cards.

It does not restyle:

- disclosure headers or text-only disclosure actions;
- Model Import's destructive remove/cancel controls, which retain their red semantics;
- gallery/debug controls;
- unrelated application buttons.

No action label, order, visibility, enabled state, command, command parameter, automation name, help text, tab order, or responsive state may change.

## Implementation architecture

A small shared XAML resource dictionary owns the approved journey-action geometry and colour tokens. Model Inspection and Hardware Inspection merge that dictionary locally into their action controls rather than overriding every application button globally.

Both action controls remain based on WinUI's native `AccentButtonStyle` and `DefaultButtonStyle`. Per-state resource aliases map the native pointer-over, pressed, disabled, foreground, and border slots to the approved shared palette. Model Inspection's declared buttons receive those aliases in XAML. Hardware Inspection's dynamically generated buttons receive the same aliases in its presentation-control code-behind; that helper is visual-only and must not alter action semantics.

The existing Model Import Browse button remains visually unchanged. Contract tests compare the shared palette with its established values so future edits cannot silently drift.

## Responsive and accessibility contract

- All interactive targets remain at least 44 px; approved action height is 46 px.
- Compact action order and full-width stacking remain unchanged.
- Text may wrap without clipping at 200% text size.
- Native pointer-over, pressed, disabled, and keyboard-focus states remain observable.
- The complete Technical information header is reachable and operable with pointer, Enter, and Space.
- Focus remains on the disclosure header when its state changes.
- Expanded and collapsed automation state remains truthful.
- Alignment must remain correct in Light, Dark, and High Contrast semantic themes even though the current production shell defaults these pages to Light.

## Verification

Test-first verification must use existing public test identities wherever possible and cover:

1. a failing Hardware details-card test proving the current glyph is top-aligned and the Technical header does not own the full available toggle width;
2. a failing Hardware page test proving the title and subtitle are not centred;
3. failing Model and Hardware action-card tests proving their geometry and palette differ from Model Import;
4. loaded WinUI geometry proving full-width disclosure targeting and vertical centring at wide and compact widths;
5. exact primary and secondary normal, pointer-over, pressed, disabled, focus, height, radius, padding, and label-weight contracts;
6. unchanged action identities, commands, parameters, enabled/visibility/help states, order, and responsive behaviour;
7. focused packaged tests, a fresh Debug x64 build, the complete packaged suite, and native screenshots of representative Hardware and Model terminal states at wide and compact widths.

The exhaustive fixture tests must prove that all 15 Hardware and all 13 Model state identities route through the corrected shared controls. Representative screenshots supplement those executable matrices; screenshots do not replace them.

## Non-goals

This work does not enable the Hardware provider, close Gate 1, enter Gate 2, calculate Model/Hardware compatibility, execute a candidate, contact a laptop, dispatch a workflow, or claim real hardware evidence.
