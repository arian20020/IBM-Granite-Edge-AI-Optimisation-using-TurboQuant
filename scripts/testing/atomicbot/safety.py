"""Pre-launch safety gates for high-memory AtomicBot cases."""

from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True)
class GateResult:
    allowed: bool
    reason: str
    required_bytes: int
    available_bytes: int
    commit_headroom_bytes: int
    reserve_bytes: int


def evaluate_memory_gate(required_bytes: int, available_bytes: int,
                         commit_headroom_bytes: int, reserve_bytes: int) -> GateResult:
    reason = "passed"
    if required_bytes + reserve_bytes > available_bytes:
        reason = "insufficient-available-ram"
    elif required_bytes + reserve_bytes > commit_headroom_bytes:
        reason = "insufficient-commit-headroom"
    return GateResult(reason == "passed", reason, required_bytes, available_bytes,
                      commit_headroom_bytes, reserve_bytes)
