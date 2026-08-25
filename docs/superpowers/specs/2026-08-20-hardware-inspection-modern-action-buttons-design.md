# Hardware Inspection Modern Action Buttons Design

**Status:** Approved visual amendment

## Goal

Refine the Hardware Inspection action buttons so they feel cleaner, more modern, and more sophisticated while preserving the approved screen layouts, copy, action order, and interaction semantics.

## Visual contract

- Keep every button at a minimum height of 44 pixels.
- Use a 10-pixel corner radius: modern and softened, but not pill-shaped.
- Use 18 pixels of horizontal padding and 10 pixels of vertical padding.
- Use semibold action text with the existing Hardware-owned font and semantic foreground resources.
- Keep the primary action as a solid IBM-blue accent treatment. Give it distinct normal, pointer-over, pressed, disabled, and keyboard-focus states.
- Keep secondary actions neutral with a subtle surface, a defined semantic border, and matching pointer-over, pressed, disabled, and keyboard-focus states.
- Use a restrained elevation change on pointer-over only. Do not add gradients, glow, glass, ornamental shadows, icons, or animation.
- Preserve a 12-pixel gap between buttons and the existing centered horizontal layout. Preserve the existing compact-width vertical stacking and action order.
- Maintain at least a 44-pixel target in every responsive state and retain visible High Contrast focus and boundaries.

## Ownership and scope

The change is owned solely by `HardwareInspectionActionCard`. Reusable button resources remain local to the Hardware action control unless an existing Hardware theme resource already expresses the required semantic color.

The amendment must not change the page, outcome cards, details, progress, copy, command routing, action availability, footer seam, Model Inspection resources, shared application resources, or navigation.

## Verification

Component tests must first fail on the absent modern style contract, then pass after implementation. The full packaged WinUI suite and Hardware theme/contract tests must remain green. Native captures must confirm that primary and secondary buttons are balanced in Completed, Warning, recovery, and compact states without disturbing the approved compositions.
