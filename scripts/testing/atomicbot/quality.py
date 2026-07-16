"""Blind-label quality scoring and controlled perplexity fixture gates."""

from __future__ import annotations

import hashlib
from dataclasses import dataclass
from pathlib import Path


WEIGHTS = {
    "correctness_and_grounding": 0.30,
    "instruction_and_format_adherence": 0.25,
    "completeness_and_fact_retention": 0.20,
    "relevance_clarity_and_coherence": 0.15,
    "stability_and_output_integrity": 0.10,
}


@dataclass(frozen=True)
class QualityResult:
    prompt_id: str
    output: str
    dimensions: dict[str, float]
    deterministic_pass: bool
    critical_caps: tuple[float, ...]
    uncapped_score: float
    final_score: float


@dataclass(frozen=True)
class FixtureGate:
    allowed: bool
    reason: str
    dataset_path: str | None
    sha256: str | None


def score_response(prompt_id: str, output: str, adjudication: dict) -> QualityResult:
    dimensions = adjudication.get("dimensions", {})
    if set(dimensions) != set(WEIGHTS):
        raise ValueError("adjudication must score every controlling dimension")
    if any(not 0 <= float(score) <= 10 for score in dimensions.values()):
        raise ValueError("dimension scores must be between 0 and 10")
    uncapped = round(sum(float(dimensions[name]) * weight for name, weight in WEIGHTS.items()), 4)
    deterministic_pass = adjudication.get("deterministic_pass") is True
    caps = tuple(float(cap) for cap in adjudication.get("critical_caps", ()))
    if not deterministic_pass and not caps:
        caps = (4.0,)
    final = min((uncapped, *caps)) if caps else uncapped
    return QualityResult(prompt_id, output, dict(dimensions), deterministic_pass,
                         caps, uncapped, round(final, 4))


def run_perplexity_gate(dataset: Path | None, expected_sha256: str | None) -> FixtureGate:
    if dataset is None or expected_sha256 is None:
        return FixtureGate(False, "dataset-not-configured", None, None)
    if not dataset.is_file():
        return FixtureGate(False, "dataset-not-found", str(dataset), None)
    digest = hashlib.sha256(dataset.read_bytes()).hexdigest()
    if digest.lower() != expected_sha256.lower():
        return FixtureGate(False, "dataset-hash-mismatch", str(dataset), digest)
    return FixtureGate(True, "passed", str(dataset), digest)
