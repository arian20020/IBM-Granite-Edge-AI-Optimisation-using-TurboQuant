"""Classify diagnostic assets without overstating codec-path equivalence.

A diagnostic model may be useful for testing process supervision even when it
cannot prove that the accepted OpenVINO CPU stateful SDPA/KV-cache path ran.
This module keeps those two claims separate and always keeps later codec,
performance, and quality authorisations disabled.
"""

from __future__ import annotations

import re
from dataclasses import dataclass
from pathlib import PurePosixPath
from typing import Final


EXPECTED_RUNTIME_SOURCE_COMMIT: Final[str] = (
    "b9a1f201c109e0bed74763934f79483cf6c4cbf4"
)
EXPECTED_BACKEND: Final[str] = "OpenVINO CPU stateful SDPA KV-cache path"
APPROVED_CANDIDATE_ORDER: Final[tuple[str, ...]] = (
    "project-generated-stateful-ir",
    "source-matched-cpu-sdpa-target",
    "small-official-text-generation-model",
)

_FULL_COMMIT = re.compile(r"^[0-9a-f]{40}$")
_SHA256 = re.compile(r"^[0-9a-f]{64}$")


@dataclass(frozen=True, slots=True)
class DiagnosticCandidate:
    """One reviewed diagnostic candidate identity."""

    candidate_id: str
    repository: str
    resolved_revision: str
    local_asset_id: str

    def __post_init__(self) -> None:
        # Reject malformed identities before any evaluation is attempted.
        for label, value in (
            ("candidate_id", self.candidate_id),
            ("repository", self.repository),
            ("local_asset_id", self.local_asset_id),
        ):
            if not isinstance(value, str) or not value or value != value.strip():
                raise ValueError(f"{label} must be a non-empty trimmed string.")
        if not _FULL_COMMIT.fullmatch(self.resolved_revision):
            raise ValueError(
                "resolved_revision must be a full lowercase forty-character commit."
            )


@dataclass(frozen=True, slots=True)
class DiagnosticEvidence:
    """Observed execution and direct-path evidence for one candidate."""

    text_generation_completed: bool
    runtime_source_commit: str
    backend: str
    stateful_execution_observed: bool
    sdpa_path_observed: bool
    kv_cache_observed: bool
    fallback_absent: bool
    trace_evidence_path: str | None
    trace_evidence_sha256: str | None

    def __post_init__(self) -> None:
        # Bool fields must be actual booleans rather than truthy integers/strings.
        for field_name in (
            "text_generation_completed",
            "stateful_execution_observed",
            "sdpa_path_observed",
            "kv_cache_observed",
            "fallback_absent",
        ):
            if type(getattr(self, field_name)) is not bool:
                raise ValueError(f"{field_name} must be a boolean.")

        if not _FULL_COMMIT.fullmatch(self.runtime_source_commit):
            raise ValueError(
                "runtime_source_commit must be a full lowercase commit identity."
            )
        if not isinstance(self.backend, str) or not self.backend:
            raise ValueError("backend must be a non-empty string.")

        # Trace path and digest form one inseparable evidence identity.
        if (self.trace_evidence_path is None) != (
            self.trace_evidence_sha256 is None
        ):
            raise ValueError(
                "trace_evidence_path and trace_evidence_sha256 must be supplied together."
            )
        if self.trace_evidence_path is not None:
            path = self.trace_evidence_path
            parsed = PurePosixPath(path)
            if (
                path != parsed.as_posix()
                or parsed.is_absolute()
                or "\\" in path
                or any(part in {"", ".", ".."} for part in parsed.parts)
            ):
                raise ValueError(
                    "trace_evidence_path must be a portable relative path."
                )
            if not _SHA256.fullmatch(self.trace_evidence_sha256 or ""):
                raise ValueError(
                    "trace_evidence_sha256 must be a lowercase SHA-256 digest."
                )


@dataclass(frozen=True, slots=True)
class DiagnosticDecision:
    """Fail-closed diagnostic classification and its claim boundary."""

    candidate_id: str
    status: str
    reasons: tuple[str, ...]
    path_equivalence_authorised: bool
    process_harness_use_authorised: bool
    codec_activation_claim_authorised: bool = False
    packed_storage_claim_authorised: bool = False
    performance_claim_authorised: bool = False
    quality_claim_authorised: bool = False


def _decision(
    candidate: DiagnosticCandidate,
    *,
    status: str,
    reasons: list[str],
    path_equivalent: bool = False,
    harness_use: bool = False,
) -> DiagnosticDecision:
    """Create one closed decision while keeping all scientific claims false."""

    if status not in {"PathEquivalent", "HarnessOnly", "Rejected"}:
        raise ValueError(f"Unsupported diagnostic status: {status}")
    if not reasons:
        raise ValueError("A diagnostic decision requires at least one reason.")
    return DiagnosticDecision(
        candidate_id=candidate.candidate_id,
        status=status,
        reasons=tuple(reasons),
        path_equivalence_authorised=path_equivalent,
        process_harness_use_authorised=harness_use,
    )


def evaluate_diagnostic_candidate(
    candidate: DiagnosticCandidate,
    evidence: DiagnosticEvidence,
) -> DiagnosticDecision:
    """Classify one candidate from direct evidence only.

    `PathEquivalent` is intentionally difficult to obtain: the exact accepted
    Runtime source, exact backend identity, stateful execution, SDPA dispatch,
    KV-cache use, no fallback, and a digest-bound trace are all mandatory.
    Successful generation without that proof remains `HarnessOnly`.
    """

    if candidate.candidate_id not in APPROVED_CANDIDATE_ORDER:
        return _decision(
            candidate,
            status="Rejected",
            reasons=[
                "The candidate is not present in the approved candidate order."
            ],
        )

    if evidence.runtime_source_commit != EXPECTED_RUNTIME_SOURCE_COMMIT:
        return _decision(
            candidate,
            status="Rejected",
            reasons=[
                "Runtime source identity does not match the accepted Route A Runtime."
            ],
        )

    if evidence.backend != EXPECTED_BACKEND:
        return _decision(
            candidate,
            status="Rejected",
            reasons=[
                "Backend identity does not match the required OpenVINO CPU "
                "stateful SDPA KV-cache path."
            ],
        )

    if not evidence.text_generation_completed:
        return _decision(
            candidate,
            status="Rejected",
            reasons=["The candidate did not complete the diagnostic generation boundary."],
        )

    direct_path_flags = (
        evidence.stateful_execution_observed,
        evidence.sdpa_path_observed,
        evidence.kv_cache_observed,
    )
    trace_supplied = evidence.trace_evidence_path is not None

    # A trace that reaches any direct-path boundary but records fallback is a
    # rejection, not a weaker harness admission. This prevents silent fallback
    # from being softened into a positive result.
    if trace_supplied and (any(direct_path_flags) or not evidence.fallback_absent):
        if not evidence.fallback_absent:
            return _decision(
                candidate,
                status="Rejected",
                reasons=["Direct trace evidence records or cannot exclude fallback."],
            )

    if (
        all(direct_path_flags)
        and evidence.fallback_absent
        and trace_supplied
        and evidence.trace_evidence_sha256 is not None
    ):
        return _decision(
            candidate,
            status="PathEquivalent",
            reasons=[
                "The exact accepted Runtime and CPU stateful SDPA/KV-cache path "
                "were observed with digest-bound trace evidence and no fallback."
            ],
            path_equivalent=True,
            harness_use=True,
        )

    return _decision(
        candidate,
        status="HarnessOnly",
        reasons=[
            "Text generation completed, but complete direct stateful SDPA/KV-cache "
            "path evidence is not available."
        ],
        harness_use=True,
    )
