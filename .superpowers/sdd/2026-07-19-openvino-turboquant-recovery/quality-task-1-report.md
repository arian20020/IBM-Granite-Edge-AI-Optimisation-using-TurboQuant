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
