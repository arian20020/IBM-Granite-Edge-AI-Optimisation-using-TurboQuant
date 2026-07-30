# Governed OpenVINO quality adapter — Task 1 report

Status: DONE

## Delivered scope

- Added `scripts/testing/official_openvino/quality_worker.py` with the
  `execute_quality_worker(spec)` protocol and `--spec` / `--result` CLI.
- Added `scripts/testing/tests/test_official_openvino_quality_worker.py`.
- The worker validates the exact, score-free spec before importing or creating
  a pipeline, creates one `openvino_genai.LLMPipeline`, and runs P1–P5, P6
  turn 1, and P6 turn 2 serially.
- P6 turn 2 is derived from stripped frozen prompts and the actual unmodified
  P6 turn-1 output. The worker preserves raw prompts/outputs, hashes, order,
  and partial failure facts without judging or scoring them.
- Result publication is atomic and canonical; `worker_result_sha256` hashes
  compact sorted UTF-8 JSON with a trailing newline, excluding only itself.

## TDD evidence

RED, before production module creation:

```powershell
python -m pytest -q -p no:cacheprovider scripts/testing/tests/test_official_openvino_quality_worker.py
# 9 failed in 0.11s
```

Every failure was the expected
`ModuleNotFoundError: scripts.testing.official_openvino.quality_worker`,
including the explicit module-import test.

GREEN after the minimal worker implementation:

```powershell
python -m pytest -q -p no:cacheprovider scripts/testing/tests/test_official_openvino_quality_worker.py
# 9 passed in 0.06s

python -m pytest -q -p no:cacheprovider scripts/testing/tests/test_official_openvino_quality_worker.py scripts/testing/tests/test_official_openvino_quality.py scripts/testing/tests/test_run_official_openvino_quality.py scripts/testing/tests/test_capture_official_openvino_quality.py scripts/testing/tests/test_adjudicate_official_openvino_quality.py
# 68 passed in 1.71s
```

## Self-review

- No real model or inference was launched: the focused tests inject a fake
  `openvino_genai` module.
- The test suite proves one pipeline, seven ordered calls, the exact P6
  history, raw output/prompt hashes, canonical result hashing, partial-failure
  facts, forbidden-field rejection, prompt duplicate/missing rejection,
  frozen generation settings, and atomic CLI publication.
- The production file does not import campaign, controller, adjudicator,
  guard, workbook, or scoring code. No such file was changed.
- The only unresolved operational concern is intentional: a genuine runtime
  invocation requires the later governed controller task and real accepted
  campaign evidence; this task does not run inference.

## Static verification

```powershell
$env:PYTHONPYCACHEPREFIX='C:\Users\Student\AppData\Local\Temp\quality-worker-pycache'; python -m py_compile scripts/testing/official_openvino/quality_worker.py
# exit 0

git diff --check
# exit 0
```

The temporary bytecode prefix is used only because the shared worktree's
existing `__pycache__` file was locked; production source was not changed to
work around that environment condition.

## Quality Task 1 fix round 1/5

RED verification:

```powershell
python -m pytest -q -p no:cacheprovider scripts/testing/tests/test_official_openvino_quality_worker.py
# 10 failed, 15 passed in 0.17s
```

The failures proved that numeric lookalikes bypassed frozen settings, camel-
case/space forbidden fields and tuples bypassed recursive rejection, a P6
turn-1 failure stopped after six pipeline calls and invented a synthetic
failure for turn 2, and a stateful mapping could alter pipeline values after
validation.

GREEN verification:

```powershell
python -m pytest -q -p no:cacheprovider scripts/testing/tests/test_official_openvino_quality_worker.py
# 25 passed in 0.08s

python -m pytest -q -p no:cacheprovider scripts/testing/tests/test_official_openvino_quality_worker.py scripts/testing/tests/test_official_openvino_quality.py scripts/testing/tests/test_run_official_openvino_quality.py scripts/testing/tests/test_capture_official_openvino_quality.py scripts/testing/tests/test_adjudicate_official_openvino_quality.py
# 84 passed in 1.64s
```

## Fix self-review

- `_validate_spec` first snapshots the input mapping and returns a frozen
  normalized model path, device, properties, settings, and prompts. Execution
  never rereads the caller mapping.
- Frozen settings compare both exact type and value. Forbidden-key detection
  normalizes camel case, whitespace, hyphens, and underscores, and recurses
  through non-string sequences.
- A P6 turn-1 failure remains the original raw failure fact. Turn 2 still
  performs the seventh real pipeline call with the same canonical
  `User`/`Assistant`/`User` transcript and an empty assistant segment when no
  raw turn-one output exists; it does not inject a synthetic failure fact.
- The CLI tests now prove exact canonical bytes and retain a prior destination
  while a simulated atomic replacement fails, with temporary cleanup.

Final static verification after formatting-only cleanup:

```powershell
$env:PYTHONPYCACHEPREFIX='C:\Users\Student\AppData\Local\Temp\quality-worker-pycache-round1'; python -m py_compile scripts/testing/official_openvino/quality_worker.py
# exit 0

git diff --check
# exit 0
```

### P6 transcript correction

The final review required the exact P6 transcript shape even after a failed
turn 1. The first correction test was RED:

```powershell
python -m pytest -q -p no:cacheprovider scripts/testing/tests/test_official_openvino_quality_worker.py
# 1 failed, 24 passed in 0.11s
```

It exposed the obsolete `Failure:` line. The final GREEN run was:

```powershell
python -m pytest -q -p no:cacheprovider scripts/testing/tests/test_official_openvino_quality_worker.py scripts/testing/tests/test_official_openvino_quality.py scripts/testing/tests/test_run_official_openvino_quality.py scripts/testing/tests/test_capture_official_openvino_quality.py scripts/testing/tests/test_adjudicate_official_openvino_quality.py
# 84 passed in 1.70s
```

The test now asserts the exact empty `Assistant: ` line and absence of a
`Failure:` transcript while retaining the genuine P6 turn-one failure fields.
