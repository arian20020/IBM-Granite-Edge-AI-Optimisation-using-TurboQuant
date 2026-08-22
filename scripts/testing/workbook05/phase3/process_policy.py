"""Deterministic C2 process-attempt classification and retry policy.

The policy is deliberately independent from the Windows process supervisor.  It
accepts one immutable observation, applies the reviewed precedence rules, and
returns a small decision record that downstream checkpoint and bundle code can
serialize without interpreting operating-system state again.
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Iterable


@dataclass(frozen=True, slots=True)
class AttemptObservation:
    """Facts collected from one supervised child-process attempt."""

    exit_code: int | None
    integrity_errors: tuple[str, ...] = ()
    infrastructure_interrupted: bool = False
    safety_stop_reason: str | None = None
    timeout_triggered: bool = False
    output_integrity_errors: tuple[str, ...] = ()
    model_compatibility_errors: tuple[str, ...] = ()
    activation_unproven: bool = False
    storage_mismatch: bool = False


@dataclass(frozen=True, slots=True)
class AttemptDecision:
    """Stable classification and the next permitted controller action."""

    classification: str
    failure_ids: tuple[str, ...]
    next_action: str


def _stable_ids(values: Iterable[str]) -> tuple[str, ...]:
    """Return non-empty identifiers in deterministic first-seen order."""

    seen: set[str] = set()
    ordered: list[str] = []
    for value in values:
        candidate = str(value).strip()
        if candidate and candidate not in seen:
            seen.add(candidate)
            ordered.append(candidate)
    return tuple(ordered)


def classify_attempt(observation: AttemptObservation) -> AttemptDecision:
    """Classify one attempt using the frozen fail-closed precedence order."""

    integrity = _stable_ids(observation.integrity_errors)
    if integrity:
        return AttemptDecision(
            "IntegrityFailure",
            integrity,
            "Stop. Preserve the attempt and investigate evidence integrity.",
        )

    if observation.safety_stop_reason:
        return AttemptDecision(
            "ResourceSafetyStop",
            (observation.safety_stop_reason,),
            "Stop. Preserve resource evidence; do not retry automatically.",
        )

    if observation.timeout_triggered:
        return AttemptDecision(
            "Timeout",
            ("STAGE_TIMEOUT",),
            "Stop. Preserve timeout and process-tree termination evidence.",
        )

    output_errors = _stable_ids(observation.output_integrity_errors)
    if output_errors:
        return AttemptDecision(
            "OutputIntegrityFailure",
            output_errors,
            "Stop. Invalid output is not an infrastructure retry condition.",
        )

    model_errors = _stable_ids(observation.model_compatibility_errors)
    if model_errors:
        return AttemptDecision(
            "ModelCompatibilityFailure",
            model_errors,
            "Stop. Record the model/runtime compatibility boundary.",
        )

    # C3 reuses this classifier. Activation failure precedes storage mismatch so
    # a cache representation is never interpreted before execution is proved.
    if observation.activation_unproven:
        return AttemptDecision(
            "ActivationUnproven",
            ("ACTIVATION_UNPROVEN",),
            "Stop. Do not attribute storage or performance to the request.",
        )

    if observation.storage_mismatch:
        return AttemptDecision(
            "StorageMismatch",
            ("STORAGE_MISMATCH",),
            "Stop. Reconcile physical K/V storage before comparison.",
        )

    if observation.infrastructure_interrupted:
        return AttemptDecision(
            "InfrastructureInterrupted",
            ("RUNNER_DISCONNECTED",),
            "Retry once after cleanup and a passing cooldown.",
        )

    if observation.exit_code == 0:
        return AttemptDecision("Passed", (), "Advance to independent validation.")

    exit_id = (
        "PROCESS_EXIT_UNKNOWN"
        if observation.exit_code is None
        else f"PROCESS_EXIT_{observation.exit_code}"
    )
    return AttemptDecision(
        "NativeProcessFailure",
        (exit_id,),
        "Stop. Preserve stdout, stderr, events, resources and termination proof.",
    )


def retry_allowed(decision: AttemptDecision, prior_attempt_count: int) -> bool:
    """Permit exactly one retry and only for infrastructure interruption."""

    if prior_attempt_count < 0:
        raise ValueError("prior_attempt_count cannot be negative")
    return (
        decision.classification == "InfrastructureInterrupted"
        and prior_attempt_count == 0
    )
