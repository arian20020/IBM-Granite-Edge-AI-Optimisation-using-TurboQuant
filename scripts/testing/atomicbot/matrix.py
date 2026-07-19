"""Strict AtomicBot retest matrix loading."""

from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path


BACKENDS = {"build", "cpu", "vulkan-partial", "vulkan-full"}
GUARDS = {"none", "memory"}


@dataclass(frozen=True)
class TestCase:
    test_id: str
    model_id: str
    model_path_env: str
    format: str
    backend: str
    context: int
    guard: str
    turbo_type: str
    flash_attention: bool
    required_metrics: tuple[str, ...]


def validate_matrix(cases: list[TestCase]) -> None:
    ids = [case.test_id for case in cases]
    if len(ids) != len(set(ids)):
        raise ValueError("duplicate test_id in matrix")
    for case in cases:
        if case.backend not in BACKENDS:
            raise ValueError(f"invalid backend for {case.test_id}: {case.backend}")
        if case.guard not in GUARDS:
            raise ValueError(f"invalid guard for {case.test_id}: {case.guard}")
        if case.context < 0:
            raise ValueError(f"invalid context for {case.test_id}: {case.context}")


def load_matrix(path: Path) -> list[TestCase]:
    payload = json.loads(path.read_text(encoding="utf-8-sig"))
    required = {field.name for field in TestCase.__dataclass_fields__.values()}
    cases: list[TestCase] = []
    for index, item in enumerate(payload.get("cases", [])):
        missing = required - item.keys()
        extra = item.keys() - required
        if missing or extra:
            raise ValueError(f"case {index} schema mismatch: missing={sorted(missing)}, extra={sorted(extra)}")
        normalized = dict(item)
        normalized["required_metrics"] = tuple(normalized["required_metrics"])
        cases.append(TestCase(**normalized))
    validate_matrix(cases)
    return cases
