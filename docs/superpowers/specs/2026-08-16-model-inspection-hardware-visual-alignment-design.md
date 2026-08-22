# Model Inspection Hardware-Template Visual Alignment

**Date:** 2026-08-16
**Status:** Approved for implementation

## Authority and scope

The existing Model Inspection plan remains the sole authority for feature purpose, stages, data collection, validation, security, privacy, timing, progress, outcomes, navigation, accessibility meaning, tests, and acceptance criteria.

The Hardware Inspection prototype is a read-only visual authority:

`C:\Users\Arian\source\repos\IBM-Granite-TurboQuant-Intel\.superpowers\brainstorm\1777-1786719024\content\hardware-outcome-recovery-family-v2.html`

Its verified SHA-256 is:

`24D34112F5BDA58A464032915C672EA825705880293BFFB88031CEB97F7B5286`

No Hardware Inspection file will be edited, copied wholesale, merged, or cherry-picked. Hardware wording, stages, evidence fields, recovery instructions, and terminology will not enter Model Inspection.

## Chosen approach

Preserve the existing Model Inspection control tree, bindings, named elements, automation identities, VisualState names, live-region ownership, focus behavior, and retained-instance lifecycle. Align the presentation through existing Model Inspection XAML and theme resources rather than replacing the feature template.

Production changes are limited to presentation XAML/resource dictionaries. Production C# and all functional layers remain unchanged. Existing visual-contract tests may be tightened only where necessary to prove the approved geometry; their identities, behavioral meaning, and discovery counts remain unchanged.

## Visual mapping

### Page frame and rhythm

- Keep the readable content column at approximately 840 px.
- Use 24 px desktop/medium page gutters and 16 px compact gutters.
- Keep 24 px between the page heading region and the first visible card.
- Keep 16 px between visible cards, with collapsed cards contributing neither height nor spacing.
- Preserve the single scrolling page, retained card instances, outgoing progress overlay, and existing responsive breakpoints.

### Shared card treatment

- Use calm light surfaces, subtle neutral borders, restrained or token-backed elevation, and approximately 12 px corner radii.
- Normalize desktop card insets to approximately 24 px and compact insets only where required for narrow layouts.
- Ensure no heading, body copy, badge, disclosure, or action sits against a card edge.
- Retain semantic light, dark, and High Contrast brushes instead of literal presentation colors in controls.

### Heading and selected-model presentation

- Preserve the Model Inspection title, explanatory copy, selected-model information, metadata, and status meaning.
- Match the Hardware template's centered hierarchy and balanced whitespace without changing text or automation order.
- Allow natural wrapping at narrow widths and 200% text scaling without fixed-height clipping.

### Progress presentation

- Preserve the five Model Inspection stages, genuine-stage sequencing, fractions, live-region behavior, and retained row identities.
- Keep 48 px minimum rows with centered vector glyphs, readable copy, trailing status/fraction placement, and continuous connector geometry.
- Use the existing semantic active/success/warning/error/neutral surfaces and complete vector glyph geometry.
- Fraction-only updates remain visually stable and do not restart status motion.

### Outcome and finding presentation

- Preserve every existing success, warning, failure, incomplete, unsupported, and cancelled outcome and its Model Inspection wording.
- Use a shared outcome structure: centered semantic status symbol, clear kicker/title/body hierarchy, restrained tinted surface, and consistent internal spacing.
- Preserve finding rows, status meaning, technical copy, and existing disclosure relationships.

### Disclosures and technical information

- Present "Inspection details" as a full-width, minimum-44-px disclosure header with clear label, helper copy, action text, and chevron.
- Keep "Technical information for IT" nested within the existing details region.
- Expanded content uses an inset, bordered secondary surface; collapsed content leaves layout and tab order.
- Existing keyboard activation, focus retention, accessibility names, and reading order remain unchanged.

### Recovery and actions

- Preserve the exact Model Inspection actions, availability, ordering, commands, and navigation.
- Use the existing responsive count-and-width placement authority while centering the visible primary-action group.
- Maintain at least 44 px interactive targets and comfortable 12-16 px spacing between visible actions.
- Do not add Hardware actions or change any interaction meaning.

### Status symbols, themes, and motion

- Retain complete inline XAML vector geometry where the surrounding circle and inner tick, X, warning, or information mark share one coordinate system.
- Do not reintroduce font glyphs or separately positioned status marks.
- Preserve theme-resource parity across Light, Dark, and High Contrast.
- Preserve system focus visuals, keyboard behavior, reduced-motion handling, and the existing precision-orbit lifecycle.

## Expected presentation files

- `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionTheme.xaml`
- `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml`
- `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionStatusGlyph.xaml`
- `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionOutcomeCard.xaml`
- `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionModelCard.xaml`
- `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionDisclosure.xaml`
- `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml`
- `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionActionCard.xaml`
- Model Inspection Debug Fixture Gallery XAML only if its visual host requires the same spacing resources.

Files without a demonstrated visual delta will remain untouched.

## Verification

Implementation will start with existing visual-contract tests, preserving their identities and behavioral assertions. Verification will cover approximately 1440 px, 900 px, 480 px, and 200% text scaling, including:

- no clipping, collisions, or horizontal scrolling;
- centered status symbols and action groups;
- minimum 44 px interactive targets;
- disclosure keyboard/focus behavior;
- correct Light, Dark, and High Contrast resources;
- reduced-motion behavior;
- unchanged Model Inspection workflow, progress, navigation, outcomes, content, security, privacy, and acceptance contracts.

The immutable Hardware prototype hash and a clean Hardware-file diff will be rechecked before completion.
