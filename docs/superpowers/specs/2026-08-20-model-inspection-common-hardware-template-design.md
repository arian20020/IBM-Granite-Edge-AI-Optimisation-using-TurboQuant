# Model Inspection Common Hardware Template Design

**Status:** Approved visual direction; implementation requires written-spec review

## Goal

Restyle the complete functional Model Inspection feature to use the same polished visual template as Hardware Inspection while retaining Model Inspection semantics, bindings, commands, state transitions, accessibility, and backend behavior exactly as they are.

The selected visual authority is `model-inspection-balanced-full-approval-v3.html`, viewed at `http://localhost:59034/model-inspection-balanced-full-approval-v3.html`. The final implementation also adopts the modern Hardware action-button treatment approved on 20 August 2026.

## Non-negotiable scope boundary

Production changes are restricted to Model-owned XAML and Model-owned theme resources. Tests may change only to lock visual structure, resources, accessibility, and responsive behavior.

Do not modify C# production code, ViewModels, services, scanners, classifiers, contracts, workers, runtime processes, commands, navigation, handoffs, data flow, copy catalogs, privacy behavior, fixture meaning, project files, App resources, Hardware Inspection, Model Import, or Onboarding.

If a required result cannot be achieved in XAML and theme resources alone, implementation must stop and obtain explicit user approval before any production C# exception.

## Shared visual grammar

- Use a light-first semantic canvas and an 840-pixel centred content column.
- Use 24-pixel desktop/medium gutters and 16-pixel compact gutters.
- Use 12-pixel card corners, one-pixel semantic borders, restrained surfaces, and no gradients, glass, glow, decorative rails, or ornamental shadows.
- Use the established Inter/Hardware typography scale: 32-pixel page title, 18-pixel section headings, 14-pixel body, 12-pixel helper text, and 10-pixel labels.
- Use 16-pixel major card gaps and natural page scrolling with no nested ordinary-content viewport.
- Keep semantic Light, Dark, and High Contrast resources even though Light is the comparison reference.

## Page hierarchy

The retained page presents these regions in this order when applicable:

1. centred `Model inspection` heading and existing explanatory copy;
2. semantic outcome card for stable outcomes;
3. balanced overview region;
4. warning/finding region when the existing presentation exposes one;
5. inspection progress or inspection-details disclosure using the existing state content;
6. centred action card;
7. the existing shell/footer seam, if supplied by the surrounding shell.

No new region changes the existing visibility, command, or state decisions.

## Balanced overview

At widths of 888 pixels or greater, the overview uses the selected asymmetric layout:

- the dominant left card presents the existing model overview facts in a two-column fact grid;
- the narrower right column stacks existing model configuration/package information above existing result/limit information;
- all values and labels come from current bindings; XAML may only reposition or restyle them;
- missing values remain visibly honest and are never replaced with invented values.

Below 888 pixels, the cards stack in the same reading order. Fact tiles reduce to one column where required by available width or 200% text.

## Progress and outcomes

Progress uses the same measured-checklist grammar as Hardware while retaining exactly Model Inspection's five existing checks and truthful progress values. It has one current-stage emphasis, completed/current/waiting states, and no fabricated percentage.

Ready, warning, unsupported/failure, cancelled, and other existing terminal variants retain their exact outcome classification and user-facing text. Their outcome cards use semantic success, warning, error, or neutral surfaces and optically centred vector glyphs. Tool or runtime failure must never be presented as a hardware or model-quality conclusion beyond the existing state semantics.

## Findings and details

Existing warnings and findings appear in a compact semantic card below the overview. The existing inspection information becomes a full-width native disclosure with:

- information glyph, title, helper text, and absolute Show/Hide action;
- the existing five inspection rows and statuses;
- an existing nested `Technical information for IT` disclosure;
- no raw paths, commands, native errors, or new diagnostic content;
- collapsed content removed from hit testing, tab order, and the accessible tree through native disclosure behavior.

Disclosure state and behavior remain whatever the existing production presentation currently defines; the XAML change does not add state ownership.

## Actions

Action availability, order, labels, commands, help text, and disabled future actions stay unchanged.

Buttons adopt the approved Hardware treatment:

- minimum 44-pixel target;
- 10-pixel corner radius;
- 18-pixel horizontal and 10-pixel vertical padding;
- semibold typography;
- solid semantic blue primary action;
- neutral bordered secondary actions;
- native pointer-over, pressed, disabled, High Contrast, and keyboard-focus behavior;
- horizontal centred grouping at wider widths and full-width vertical stacking below 600 pixels.

## Responsive and accessibility contract

- Wide: 888 pixels and above, asymmetric overview and two-column facts.
- Medium: 600–887 pixels, single-column overview cards while retaining desktop gutters.
- Compact: below 600 pixels, 16-pixel gutters, one-column facts, full-width stacked actions.
- At 200% text, no horizontal clipping or scrolling; reading and focus order stay unchanged.
- Targets remain at least 44 pixels.
- Outcome, progress, stage status, and disclosure labels do not rely on color alone.
- Existing live regions, automation names, keyboard behavior, focus restoration, and reduced-motion behavior remain intact.

## Production file ownership

Expected production files are limited to the existing Model-owned presentation surfaces:

- `Features/ModelInspection/ModelInspectionPage.xaml`
- `Features/ModelInspection/Presentation/ModelInspectionTheme.xaml`
- `Features/ModelInspection/Controls/InspectionOutcomeCard.xaml`
- `Features/ModelInspection/Controls/InspectionModelCard.xaml`
- `Features/ModelInspection/Controls/InspectionContentCard.xaml`
- `Features/ModelInspection/Controls/InspectionDisclosure.xaml`
- `Features/ModelInspection/Controls/InspectionActionCard.xaml`
- `Features/ModelInspection/Controls/InspectionStatusGlyph.xaml`

An implementation need not edit every listed file. No production file outside this allowlist may change without a new explicit approval.

## Acceptance

- Current Model Inspection functional and behavioral tests remain green without modifying their expected semantics.
- New or updated visual tests fail before the XAML changes and pass afterward.
- Native captures cover progress, ready, warning, unsupported/failure, and cancelled at 1440×1100, 900×1000, 480×900, and representative 200% text.
- Captures are compared against the selected balanced mock and the approved Hardware grammar.
- The production diff contains only the approved Model-owned XAML/theme allowlist; tests contain only presentation-contract changes.
- No backend, C# production, project, navigation, Hardware, Model Import, or shared-resource file changes.
