# Hardware Inspection Terminal Polish Design

**Status:** Approved by the user on 20 August 2026.

## Purpose

Polish the transient-failure and completed-with-warnings presentations so they feel cleaner, more sophisticated, and more distinctive without changing the approved Hardware Inspection contract, wording, actions, state model, or reading order.

## Approved treatment

- Preserve every existing user-facing string and action.
- Keep the transient outcome card as the semantic error summary, but improve the supporting recovery card through clearer card hierarchy and more deliberate spacing.
- Present each recovery instruction as its own quiet, bordered surface with a larger numbered marker, stronger title/body separation, and consistent alignment. Do not add decorative content, new claims, badges, gradients, or animation.
- Preserve the `Inspection details` disclosure and action order while aligning their sizing and spacing with the refined cards.
- In the warning outcome, centre the review count stack: the numeral `1` sits directly above the centred `detail needs review` label. Keep the stack in the outcome card's trailing column and retain the exact count and wording.
- Continue to use the Hardware-owned theme brushes so Light, Dark, and High Contrast remain semantic and accessible.

## Acceptance

- Native transient-failure capture shows two clearly separated recovery steps with no change to their text.
- Native warning capture shows a visually centred numeral and label.
- Existing typed actions, details behavior, accessibility, responsive reflow, and presentation contracts remain unchanged.
- The packaged Hardware tests, Python contracts, and x64 Debug build pass.
