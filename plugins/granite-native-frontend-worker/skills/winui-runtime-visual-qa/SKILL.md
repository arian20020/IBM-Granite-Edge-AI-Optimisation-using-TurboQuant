---
name: winui-runtime-visual-qa
description: Read-only runtime visual QA for Granite WinUI 3. Reuses repository fixture galleries and captures current-revision evidence across states, themes and window sizes.
---

# WinUI Runtime Visual QA

Operate read-only.

## Evidence sources

Prefer, in order:

1. Existing feature fixture catalogue and gallery.
2. Existing screen observer/comparer infrastructure.
3. Safe real user journey.
4. Targeted native UI automation.

## Matrix

Capture every material state at:

- minimum supported, compact, normal, large and maximized windows;
- Light, Dark and High Contrast;
- representative display scale;
- default and stress text scale;
- motion enabled and disabled where relevant;
- long text, filenames, metadata, error, cancellation, retry and stale-session fixtures.

Inspect layout, hierarchy, typography, clipping, wrapping, focus, state clarity, iconography, density, native fidelity, misleading affordances and generic-AI patterns.

Do not update baselines automatically. Every screenshot must identify the current commit and fixture. Return a mismatch ledger ordered BLOCKER, HIGH, MEDIUM, LOW.
