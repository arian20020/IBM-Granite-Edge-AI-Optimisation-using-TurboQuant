# Formal U4 Spec Coverage Plan

## Goal

Allow formal Granite-3B worker-spec generation to include the U4 scalar
matrix row only when an explicit U4 model directory is supplied. This is
template coverage only: it does not change the matrix outcome, execute a
worker, or claim that scalar U4 activates.

## Design

- Keep `generate_formal_u8_granite3b_specs` and the existing
  `--model-path` CLI contract unchanged when no U4 path is provided: select
  only formal U8 Granite-3B rows, producing 20 templates and the existing
  expected-rejection record.
- Add an optional `u4_model_path` parameter and `--u4-model-path` flag.
  When present, validate it as a distinct directory and bind formal rows by
  their exact `weight_precision`; U8 rows use `model_path`, and the sole
  formal U4 Granite-3B row (OV-TQ-02) uses only `u4_model_path`.
- Selection must be fail-closed: a selected row without an exact precision
  binding is an error; no fallback or precision substitution is allowed.
  Expected-rejection OV-TQ-18 stays in `expected-rejections.json`, not a
  generated worker spec.

## TDD Plan

1. Add tests asserting that the explicit U4 binding produces 21 templates,
   binds OV-TQ-02 to the U4 directory, preserves its exact scalar U4
   properties, and leaves OV-TQ-18 ungenerated. Run them and record the
   expected missing-API failure.
2. Add a fail-closed test for a missing U4 directory and run it before the
   production change.
3. Implement the smallest precision-to-model binding and optional CLI
   argument. Re-run focused generator, matrix, and sequence tests, then
   compile-check and inspect the staged diff before a scoped commit.

## TDD and Verification Report

### RED

The new API/CLI tests were run before production changes:

```text
4 failed, 11 deselected
TypeError: generate_formal_u8_granite3b_specs() got an unexpected
keyword argument 'u4_model_path'
generate_official_openvino_specs.py: error: unrecognized arguments:
--u4-model-path ...
```

The failures proved that neither the optional API nor the CLI binding
existed; no inference was invoked.

### GREEN

The minimal change validates a separate U4 directory, rejects a reused U8
directory, selects rows only for exactly bound weight precisions, and uses
that exact path in each template. With no U4 binding, selection remains U8
only, preserving the existing 20-template output. With the explicit binding,
OV-TQ-02 at context 4096 adds the 21st template with `KEY_CACHE_PRECISION`
and `VALUE_CACHE_PRECISION` both `u4`. OV-TQ-18 continues into the rejection
record and never receives a worker spec.

### Fresh verification

```text
Python 3.11: pytest -p no:cacheprovider \
  test_official_openvino_campaign_spec.py \
  test_official_openvino_matrix.py \
  test_measure_official_openvino_sequence.py -q
52 passed in 2.84s

Python 3.13: py_compile of campaign_spec.py,
generate_official_openvino_specs.py, and test_official_openvino_campaign_spec.py
with temporary cfile destinations: exit 0

git diff --check: exit 0
```

The initial direct `py_compile` attempt was unable to replace an existing
protected worktree `__pycache__` file. The temporary-output invocation above
compiled the same source without altering the worktree.
