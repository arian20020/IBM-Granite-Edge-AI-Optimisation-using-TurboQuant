"""Deterministic counterbalanced execution and query schedules."""

from __future__ import annotations

from dataclasses import dataclass
import random


CONFIGURATIONS = ("exact", "tq2", "tq3", "tq4")


@dataclass(frozen=True)
class RepetitionSchedule:
    repetition: int
    seed: int
    configuration_order: tuple[str, ...]
    query_order: tuple[int, ...]


def build_schedule(*, seed: int, repetitions: int, query_count: int) -> tuple[RepetitionSchedule, ...]:
    if repetitions <= 0:
        raise ValueError("repetitions must be positive")
    if query_count <= 0:
        raise ValueError("query count must be positive")
    start = seed % len(CONFIGURATIONS)
    result = []
    for repetition in range(repetitions):
        offset = (start + repetition) % len(CONFIGURATIONS)
        order = CONFIGURATIONS[offset:] + CONFIGURATIONS[:offset]
        query_order = list(range(query_count))
        random.Random(seed + repetition * 1_000_003).shuffle(query_order)
        result.append(RepetitionSchedule(repetition + 1, seed + repetition * 1_000_003, order, tuple(query_order)))
    return tuple(result)
