# Quality capture adapter - Task 5 implementation report

Date: 2026-07-30

Status: implementation complete, independently approved, and unstaged

## Scope

Implemented only Task 5 from
`docs/superpowers/plans/2026-07-30-openvino-quality-capture-adapter.md`.

Modified:

- `scripts/testing/adjudicate_official_openvino_quality.py`
- `scripts/testing/tests/test_adjudicate_official_openvino_quality.py`

Created:

- `.superpowers/sdd/2026-07-19-openvino-turboquant-recovery/quality-task-5-report.md`

Not modified:

- Task 4 capture/controller files
- formal inference evidence
- workbook or registers
- `.superpowers/sdd/2026-07-19-openvino-turboquant-recovery/progress.md`

## Controlling failure decision

A governed capture containing any failed turn is validated as evidence and then
rejected from blind scoring with an explicit `failed ... is non-scored`
diagnostic. Nullable `output` and `output_sha256` failure fields are retained
and verified exactly. They are never converted to empty strings, complete
responses, score rows, or passing outcomes.

## Implemented boundary

The collector now dispatches only between a complete legacy root and a
complete governed-capture root. Mixed, stale, missing, or ambiguous forms are
rejected.

For each governed blind-label directory it:

1. snapshots an exact create-only tree and rejects links, reparse aliases,
   hardlinks, missing files, unexpected files, and files that change while
   read;
2. snapshots every component of the lexical raw-root ancestry plus the exact
   blind-label child set, rejecting a symlink, junction, or reparse component
   before any evidence is consumed;
3. parses canonical nullable capture JSON without weakening the existing
   null-free reviewer schema;
4. validates capture-summary, record, response, output, prompt, worker-result,
   governed-receipt, and evidence-file hashes;
5. binds the receipt to the actual worker spec, worker result, worker log, and
   guard-evidence bytes;
6. requires a valid no-timeout, no-low-memory-stop, zero-cleanup,
   zero-survivor guard outcome;
7. validates the frozen worker-spec schema/settings/seven-prompt sequence;
8. validates exact typed worker outcomes and cross-checks them against all six
   capture records;
9. requires exact P1-P6 records and actual P6 two-turn history;
10. validates the capture summary against the six reconstructed records;
11. re-reads every retained row identity and byte snapshot, then rechecks the
    ancestry and aggregate root/child identities at the final return boundary;
    and
12. projects only opaque label, frozen prompt identity, response content,
    content-only hashes, turn evidence, and deterministic gates.

Campaign, runtime, model, build, test ID, context, device, precision, codec,
worker, guard, and record identity fields do not enter blind response rows.
The existing score-sheet contract, objective caps, identical-content equality,
arithmetic, reviewer/notes requirements, and final unblinding remain
unchanged.

## Strict TDD evidence

Initial governed-adapter RED:

```text
python -m pytest -p no:cacheprovider \
  scripts/testing/tests/test_adjudicate_official_openvino_quality.py \
  -k governed -v

10 failed, 11 deselected
```

All ten cases failed at the legacy-only `completion.json` boundary, proving the
new tests exercised the missing governed collector.

Subsequent mutation RED cases were run before their fixes:

- consistently rehashed legacy worker-spec schema: did not raise;
- rehashed Boolean worker-outcome status: did not raise;
- rehashed worker output differing from capture records: did not raise;
- consistently rehashed guard RAM floor below 2,048 MiB: did not raise;
- consistently rehashed stale guard schema, redirected command/spec/result,
  log/evidence paths, environment sentinel, working directory, and invalid
  timeout type: did not raise;
- consistently rehashed absolute non-Python executable while preserving the
  valid worker-module command tail: did not raise;
- rehashed structurally valid but unexecuted worker-spec prompt: did not
  raise;
- a real Windows junction (or directory symlink where applicable) used as the
  raw root: did not raise;
- same-byte atomic replacement of an already-validated earlier row while a
  later row began validation: did not raise; and
- insertion of a new blind-label child while a later row began validation:
  did not raise.

Each was then made GREEN with the smallest corresponding schema/type/content
binding.

## Adversarial coverage

Synthetic, non-inference tests cover:

- six-row blind projection with no private identity;
- actual P6 turn-one and turn-two evidence;
- governed scoring, objective caps, and unblinding;
- forged record self-hash;
- consistently re-signed forged response hash;
- forged capture-summary self-hash;
- actual guard-evidence byte mismatch;
- consistently rehashed guard evidence with the wrong RAM floor;
- consistently rehashed stale guard schema and redirected guard identity
  fields;
- consistently rehashed real absolute `attacker.exe` executable redirection;
- consistently rehashed stale worker-spec schema;
- consistently rehashed unexecuted worker-spec prompt;
- consistently rehashed invalid worker-outcome type;
- consistently rehashed worker output not present in records;
- missing P1-P6 evidence;
- hardlink aliases;
- a real junction/symlink raw-root ancestry alias;
- deterministic same-byte replacement of an earlier row during later-row
  validation;
- deterministic aggregate blind-label child insertion during later-row
  validation; and
- failed P2, P6 turn one, and P6 turn two with exact nullable output
  preservation and no scoring.

Legacy adjudication tests continue to cover deterministic gates, harsh caps,
empty completed output scoring, score-sheet completeness, response hash
binding, identical-content equality, frozen rubric identity, and final
unblinding.

## Fresh verification

Focused:

```text
python -m pytest -p no:cacheprovider \
  scripts/testing/tests/test_adjudicate_official_openvino_quality.py -q

39 passed in 13.42s
```

Producer-to-adjudicator regression:

```text
python -m pytest -p no:cacheprovider \
  scripts/testing/tests/test_capture_official_openvino_quality.py \
  scripts/testing/tests/test_official_openvino_quality_campaign.py \
  scripts/testing/tests/test_adjudicate_official_openvino_quality.py \
  scripts/testing/tests/test_run_official_openvino_quality.py -q

185 passed in 48.70s
```

Syntax:

```text
python -c "compile(...adjudicator...); compile(...test...); print('compile: ok')"

compile: ok
```

Scoped `git diff --check` completed without whitespace errors. Ruff is not
installed in the available Python 3.11 environment, so no Ruff result is
claimed.

## Independent review

Review round 1 found that consistently rehashed stale/redirected guard
identity fields could still enter scoring. Fix round 1 added current guard
schema, exact worker module/spec/result command shape, exact log/evidence
paths, governed working-repository marker, strict environment/timeout fields,
the exact RAM floor, and safe emergency/cleanup validation.

Review round 2 found that an absolute non-Python executable could retain the
valid `-m` command tail. Fix round 2 added an exact failing reproduction using
a real `attacker.exe` and requires an existing regular, non-reparse Windows
Python executable.

Review round 3 found two remaining time-of-check/time-of-use boundaries:
lexical `raw_root` ancestry did not reject a root junction, and the aggregate
child set plus already-validated earlier rows were not all re-read after later
rows. Fix round 3 added real junction coverage, deterministic same-byte
earlier-row replacement and child-set mutation reproductions, immutable
lexical ancestry and aggregate snapshots, up-front retained row byte/identity
snapshots, and final all-row plus ancestry/aggregate revalidation.

Final independent result:

```text
APPROVED - no remaining Important findings in the two reviewed areas.

The real Windows /J junction test passed without skipping. The deterministic
same-byte earlier-row replacement and late child-set insertion tests exercise
the prior gaps directly. Complete focused re-review: 39 passed in 13.54s.
No files were edited, staged, or committed by the reviewer.
```

## Handoff

The independently approved implementation, tests, and this report remain
intentionally unstaged and uncommitted for parent integration.
