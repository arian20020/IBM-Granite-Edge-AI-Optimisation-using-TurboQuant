"""Classify codec-related source evidence without making runtime support claims."""

from __future__ import annotations

import hashlib
import json
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Any, Mapping, Sequence


@dataclass(frozen=True)
class CapabilityFinding:
    """One exact-file finding from a pinned external source tree."""

    capability_id: str
    classification: str
    source_path: str
    matched_tokens: tuple[str, ...]
    missing_tokens: tuple[str, ...]
    contradictory_tokens: tuple[str, ...]
    sha256: str


def _resolve_source_path(source_root: Path, relative_path: str) -> Path:
    """Resolve a configured source path and reject traversal outside the tree."""

    root = source_root.resolve()
    candidate = (root / relative_path).resolve()
    try:
        candidate.relative_to(root)
    except ValueError as error:
        raise ValueError(
            f"Source capability path escapes the verified source root: {relative_path}"
        ) from error
    return candidate


def _sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _read_source(source_root: Path, relative_path: str) -> tuple[bytes, str]:
    path = _resolve_source_path(source_root, relative_path)
    if not path.is_file():
        return b"", ""
    data = path.read_bytes()
    return data, data.decode("utf-8", errors="replace")


def inspect_route_capabilities(
    source_root: Path,
    requirements: Sequence[Mapping[str, Any]],
) -> list[CapabilityFinding]:
    """Inspect only declared files and classify exact required/contradictory tokens.

    A positive result is deliberately named ``Present in source``. This phase
    cannot prove that the path compiles, dispatches, activates, or avoids fallback.
    """

    findings: list[CapabilityFinding] = []
    for requirement in requirements:
        capability_id = str(requirement["capability_id"])
        source_path = str(requirement["path"])
        required_tokens = tuple(str(token) for token in requirement["required_tokens"])
        contradiction_candidates = tuple(
            str(token) for token in requirement.get("contradictory_tokens", ())
        )
        data, text = _read_source(source_root, source_path)

        matched = tuple(token for token in required_tokens if token in text)
        missing = tuple(token for token in required_tokens if token not in text)
        contradictory = tuple(
            token for token in contradiction_candidates if token in text
        )

        if missing:
            classification = "Missing"
        elif contradictory:
            classification = "Contradictory"
        else:
            classification = "Present in source"

        findings.append(
            CapabilityFinding(
                capability_id=capability_id,
                classification=classification,
                source_path=source_path,
                matched_tokens=matched,
                missing_tokens=missing,
                contradictory_tokens=contradictory,
                sha256=_sha256(data),
            )
        )
    return findings


def _bounded_excerpt(text: str, tokens: Sequence[str], context_lines: int = 2) -> str:
    """Return deterministic line-numbered context around matched evidence tokens."""

    lines = text.splitlines()
    selected: set[int] = set()
    for index, line in enumerate(lines):
        if any(token in line for token in tokens):
            start = max(0, index - context_lines)
            end = min(len(lines), index + context_lines + 1)
            selected.update(range(start, end))
    if not selected:
        return "No configured evidence token was found.\n"
    return "".join(f"{index + 1}: {lines[index]}\n" for index in sorted(selected))


def write_capability_report(
    source_root: Path,
    requirements: Sequence[Mapping[str, Any]],
    route_id: str,
    evidence_directory: Path,
) -> dict[str, Any]:
    """Write bounded excerpts and one schema-ready capability report."""

    evidence_directory.mkdir(parents=True, exist_ok=True)
    excerpt_directory = evidence_directory / "excerpts"
    excerpt_directory.mkdir(parents=True, exist_ok=True)
    findings = inspect_route_capabilities(source_root, requirements)
    rows: list[dict[str, Any]] = []

    for finding in findings:
        _, text = _read_source(source_root, finding.source_path)
        excerpt_path = excerpt_directory / f"{finding.capability_id}.txt"
        excerpt_tokens = (
            *finding.matched_tokens,
            *finding.contradictory_tokens,
        )
        excerpt_path.write_text(
            _bounded_excerpt(text, excerpt_tokens),
            encoding="utf-8",
            newline="\n",
        )
        row = asdict(finding)
        row["source_sha256"] = row.pop("sha256")
        row["matched_tokens"] = list(row["matched_tokens"])
        row["missing_tokens"] = list(row["missing_tokens"])
        row["contradictory_tokens"] = list(row["contradictory_tokens"])
        row["excerpt_path"] = f"excerpts/{excerpt_path.name}"
        rows.append(row)

    classifications = {finding.classification for finding in findings}
    if "Missing" in classifications:
        decision = "Blocked"
        reason = "One or more mandatory source tokens are missing."
    elif "Contradictory" in classifications:
        decision = "Candidate"
        reason = (
            "All mandatory tokens were found, but contradictory source text "
            "prevents a positive source-only conclusion."
        )
    else:
        decision = "Passed"
        reason = (
            "All configured tokens are present in the pinned source. This is "
            "source evidence only, not an executable-support claim."
        )

    report = {
        "schema_version": "1.0",
        "campaign_id": "GTQ-WB05-MF-v1",
        "phase_id": "phase-1-source-admission",
        "route_id": route_id,
        "findings": rows,
        "decision": decision,
        "decision_reason": reason,
    }
    (evidence_directory / "source-capabilities.json").write_text(
        json.dumps(report, indent=2) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    return report
