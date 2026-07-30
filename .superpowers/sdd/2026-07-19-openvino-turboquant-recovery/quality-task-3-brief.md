# Governed OpenVINO quality adapter — Task 3 brief

Implement Task 3 from
`docs/superpowers/plans/2026-07-30-openvino-quality-capture-adapter.md`
only after Task 2's review is clean.

Modify:

- `scripts/testing/official_openvino/quality_campaign.py`
- `scripts/testing/official_openvino/quality_worker.py` only if the result
  validator needs a shared canonical helper
- `scripts/testing/official_openvino/guarded_build.py`
- `scripts/testing/tests/test_official_openvino_quality_campaign.py`
- `scripts/testing/tests/test_official_openvino_guarded_build.py`

Do not add capture publication/resume, adjudication, CLI dispatch, real
inference, workbook changes, or formal evidence.

Define immutable `GovernedQualityWorkerResult` and
`run_governed_quality_worker(campaign, output_root, timeout_seconds,
run_command=run_guarded_command)`. The result must retain the validated worker
result and SHA-256 plus the exact guard evidence and SHA-256.

## Guard environment contract

Extend `run_guarded_command()` and its private launch helper with
`environment: Mapping[str, str] | None = None`.

- Copy and validate before any launch.
- Reject bool/non-mapping inputs, blank or non-string keys/values, and
  case-insensitive duplicate Windows keys such as `Path` plus `PATH`.
- When supplied, use that mapping as the base. When absent, preserve the
  existing ambient-environment behavior.
- Apply `MSBUILDDISABLENODEREUSE=1` to the effective mapping, pass that exact
  mapping to `Popen`, and record the SHA-256 of canonical compact sorted UTF-8
  JSON for that exact effective mapping.
- Preserve all existing Job Object, RAM, timeout, path-identity, log, and
  cleanup behavior.
- Add exact integer `cleanup_process_count`, derived from the owned Job Object
  survivor proof, to the guard evidence.

## Governed launch contract

- Re-load/revalidate the accepted campaign before launch.
- Require a fresh dedicated output directory and absent spec, result, log, and
  guard-evidence targets; never overwrite prior evidence.
- Canonically publish the worker spec, bind its SHA-256, then launch exactly:
  `<accepted python> -m scripts.testing.official_openvino.quality_worker
  --spec <spec> --result <result>`.
- Use the accepted repository as cwd and the accepted worker environment.
- Use `GuardLimits(minimum_available_ram_bytes=2_048 * 1024 * 1024,
  maximum_runtime_seconds=timeout_seconds)`.
- Pass `expected_exit="zero"`.
- Require guard schema/validity, exact command/cwd/path/environment hash,
  exact RAM floor, no timeout, no low-memory stop, no emergency fallback,
  zero cleanup survivors, exit code zero, and matching evidence bytes/hash.
- Strictly parse the worker result; require the Task-1 schema, exactly seven
  ordered outcomes, canonical `worker_result_sha256`, and exact result bytes/
  hash. Raw failed turns remain governed facts and do not make the process
  evidence disappear.
- Reject any pre-existing/tampered/missing spec, result, log, or guard file.

## Strict TDD

First add RED tests for:

1. environment reaches a real/fake child unchanged except the documented
   MSBuild key and its evidence hash matches the actual `Popen` mapping;
2. invalid/colliding environment keys reject before launch and existing
   no-environment callers still work;
3. exact 2 GiB floor, timeout, command, cwd, spec/result paths, and environment
   are passed to the injected guard;
4. timeout, low-memory, invalid guard, nonzero exit, emergency action,
   nonzero cleanup/survivors, wrong environment hash, and wrong evidence hash
   reject;
5. malformed, missing, pre-existing, reordered, or hash-tampered worker result
   rejects;
6. a governed worker result containing one raw failed turn is retained without
   inventing a score or pass.

Run focused Task-3 tests, Task-1/2 quality tests, all guarded-build tests,
campaign sequence/binding tests, `py_compile` with a temporary bytecode root,
and `git diff --check`.

Write
`.superpowers/sdd/2026-07-19-openvino-turboquant-recovery/quality-task-3-report.md`
and commit only this task.
