"""Strict loading for the controlled animehacker TQ3_0 retest matrix."""

from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path


BACKENDS = {"build", "cpu", "sycl-partial"}
STATUSES = {"planned", "conditional"}
GUARDS = {"none", "memory"}
PHASES = {"build", "runtime"}
ACTIVATION_REQUIREMENTS = {"not-applicable", "standard-cache", "runtime-or-binary-source-linked"}


@dataclass(frozen=True)
class TestCase:
    test_id: str
    phase: str
    description: str
    model_id: str
    model_path_env: str
    format: str
    cache: str
    backend: str
    context: int
    guard: str
    status: str
    activation_requirement: str
    required_metrics: frozenset[str]
    safety_requirements: tuple[str, ...]
    quality_required: bool
    formal_repetitions: int
    excluded_warmup: bool


def validate_matrix(cases: list[TestCase]) -> None:
    ids = [case.test_id for case in cases]
    if len(ids) != len(set(ids)):
        raise ValueError("duplicate test_id in matrix")
    for case in cases:
        for value, allowed, label in (
            (case.phase, PHASES, "phase"),
            (case.backend, BACKENDS, "backend"),
            (case.status, STATUSES, "status"),
            (case.guard, GUARDS, "guard"),
            (case.activation_requirement, ACTIVATION_REQUIREMENTS, "activation requirement"),
        ):
            if value not in allowed:
                raise ValueError(f"invalid {label} for {case.test_id}: {value}")
        if case.context < 0 or case.formal_repetitions < 0:
            raise ValueError(f"invalid run count for {case.test_id}")


def load_matrix(path: Path) -> list[TestCase]:
    payload = json.loads(path.read_text(encoding="utf-8-sig"))
    required = set(TestCase.__dataclass_fields__)
    cases: list[TestCase] = []
    for index, item in enumerate(payload.get("cases", [])):
        missing = required - item.keys()
        extra = item.keys() - required
        if missing or extra:
            raise ValueError(
                f"case {index} schema mismatch: missing={sorted(missing)}, extra={sorted(extra)}"
            )
        normalized = dict(item)
        normalized["required_metrics"] = frozenset(normalized["required_metrics"])
        normalized["safety_requirements"] = tuple(normalized["safety_requirements"])
        cases.append(TestCase(**normalized))
    validate_matrix(cases)
    return cases
