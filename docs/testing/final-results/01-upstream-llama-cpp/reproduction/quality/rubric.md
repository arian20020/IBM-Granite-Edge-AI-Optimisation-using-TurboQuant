# GTQ-QUALITY-RUBRIC-v1

## Weighted dimensions and critical caps

- `correctness_and_grounding` — weight 0.30; cap: Material contradiction, fabricated required fact or wrong exact answer caps the task at 4/10.
- `instruction_and_format_adherence` — weight 0.25; cap: Failed required format/schema caps the task at 4/10; empty or unnecessary refusal scores 0/10.
- `completeness_and_fact_retention` — weight 0.20; cap: Missing a critical required fact caps the task at 4/10.
- `relevance_clarity_and_coherence` — weight 0.15; cap: Severe incoherence or off-topic output caps the task at 2/10.
- `stability_and_output_integrity` — weight 0.10; cap: Corruption, repeated loops, truncation that prevents completion, or wrong remembered value caps the task at 2/10.

## Anchors

- 10/10: Fully correct, complete, relevant and exactly compliant; no material weakness.
- 8/10: Correct and usable with only a minor, non-material weakness.
- 6/10: Mostly correct, but one meaningful omission, ambiguity or quality weakness remains.
- 4/10: Partially correct or useful, but a critical instruction, fact or format requirement failed.
- 2/10: Minimal usable content; major errors, instability or severe incompleteness.
- 0/10: No usable answer, empty output, unnecessary refusal, or wholly incorrect/corrupted output.

## Procedure

1. Run deterministic gates first.
2. Score baseline and optimised outputs independently with configuration labels hidden.
3. For subjective pairwise comparison, evaluate both presentation orders.
4. Manually adjudicate every critical-gate failure, judge disagreement greater than one point, or ranking reversal.
5. An automated or model judge must never override an objective deterministic failure.
6. Report per-prompt results as well as averages; equal averages must not hide different failures.

Historical per-prompt composite scores are preserved; component-level score increments were not collected.
