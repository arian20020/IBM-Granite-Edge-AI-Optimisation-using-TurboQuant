"""Declarative migration rules for the compact published-route layout."""

from __future__ import annotations

import hashlib
import json
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Mapping

from .validate import extract_validation_messages, receipt_valid


@dataclass(frozen=True, slots=True)
class PathMove:
    """One lossless source-to-destination file move inside a route."""

    old_path: Path
    new_path: Path
    sha256: str
    role: str
    reason: str


@dataclass(frozen=True, slots=True)
class _MoveSpec:
    pattern: re.Pattern[str]
    destination: str
    role: str
    reason: str


_TOP_LEVEL_COMPONENTS = (
    "README.md",
    "reports",
    "data",
    "evidence",
    "validation",
    "reproduction",
)
_REMOVED_TOP_LEVELS = (
    "workbook",
    "results",
    "quality",
    "failures",
    "protocol",
    "system",
)
_TEXT_EXTENSIONS = {
    ".csv",
    ".json",
    ".jsonl",
    ".md",
    ".txt",
    ".yml",
    ".yaml",
}
_VALIDATION_RECEIPTS = (
    ("coverage-validation.json", "coverage"),
    ("data-validation.json", "data"),
    ("integrity-validation.json", "integrity"),
    ("relationship-validation.json", "relationship"),
    ("visual-validation.json", "visual"),
    ("cross-route-validation.json", "cross_route"),
    ("manual-visual-qa.json", "manual_visual_qa"),
    ("workbook-parity.json", "workbook_parity"),
)
_MOVE_SPECS = (
    _MoveSpec(
        re.compile(r"^README\.md$"),
        "README.md",
        "readme",
        "keep the route landing page at the root",
    ),
    _MoveSpec(
        re.compile(r"^route-manifest\.json$"),
        "data/route.json",
        "data",
        "publish the route manifest beside canonical data tables",
    ),
    _MoveSpec(
        re.compile(r"^workbook/source/(?P<name>[^/]+)-final-report\.md$"),
        "reports/{route_id}-report.md",
        "report",
        "collapse the canonical Markdown report into reports/",
    ),
    _MoveSpec(
        re.compile(r"^workbook/generated/(?P<name>[^/]+)-final-report\.docx$"),
        "reports/{route_id}-report.docx",
        "report",
        "collapse the editable report derivative into reports/",
    ),
    _MoveSpec(
        re.compile(r"^workbook/generated/(?P<name>[^/]+)-final-report\.pdf$"),
        "reports/{route_id}-report.pdf",
        "report",
        "collapse the PDF report derivative into reports/",
    ),
    _MoveSpec(
        re.compile(r"^workbook/generated/(?P<name>[^/]+)-portable-results\.xlsx$"),
        "reports/{route_id}-results.xlsx",
        "report",
        "publish the portable workbook derivative under reports/",
    ),
    _MoveSpec(
        re.compile(r"^workbook/generated/portable-workbook-provenance\.json$"),
        "reports/{route_id}-results-provenance.json",
        "report",
        "keep workbook provenance with the published workbook derivative",
    ),
    _MoveSpec(
        re.compile(r"^results/attempts\.csv$"),
        "data/attempts.csv",
        "data",
        "publish canonical attempts in data/",
    ),
    _MoveSpec(
        re.compile(r"^results/measurements\.csv$"),
        "data/measurements.csv",
        "data",
        "publish canonical measurements in data/",
    ),
    _MoveSpec(
        re.compile(r"^results/summary-results\.csv$"),
        "data/summaries.csv",
        "data",
        "publish canonical summaries in data/",
    ),
    _MoveSpec(
        re.compile(r"^results/availability-matrix\.csv$"),
        "data/availability-matrix.csv",
        "data",
        "preserve route availability accounting in data/",
    ),
    _MoveSpec(
        re.compile(r"^results/resource-observations\.csv$"),
        "data/resource-observations.csv",
        "data",
        "preserve auxiliary resource observations in data/",
    ),
    _MoveSpec(
        re.compile(r"^results/comparability-matrix\.csv$"),
        "data/comparability-matrix.csv",
        "data",
        "publish direct-comparison decisions in data/",
    ),
    _MoveSpec(
        re.compile(r"^results/route-status-summary\.csv$"),
        "data/route-status-summary.csv",
        "data",
        "publish cross-route status accounting in data/",
    ),
    _MoveSpec(
        re.compile(r"^results/source/(?P<tail>.+)$"),
        "evidence/source/{tail}",
        "evidence",
        "source workbooks remain evidence-only after the layout cleanup",
    ),
    _MoveSpec(
        re.compile(r"^quality/scores\.csv$"),
        "data/quality.csv",
        "data",
        "publish canonical quality scores in data/",
    ),
    _MoveSpec(
        re.compile(r"^quality/(?P<tail>.+)$"),
        "reproduction/quality/{tail}",
        "reproduction",
        "retain route-specific quality method material under reproduction/",
    ),
    _MoveSpec(
        re.compile(r"^failures/failure-register\.csv$"),
        "data/failures.csv",
        "data",
        "publish canonical failures in data/",
    ),
    _MoveSpec(
        re.compile(r"^failures/(?P<tail>.+)$"),
        "evidence/failures/{tail}",
        "evidence",
        "retain supporting failure artifacts as evidence",
    ),
    _MoveSpec(
        re.compile(r"^protocol/deviations\.csv$"),
        "data/deviations.csv",
        "data",
        "publish documented route deviations in data/",
    ),
    _MoveSpec(
        re.compile(r"^protocol/(?P<tail>.+)$"),
        "reproduction/protocol/{tail}",
        "reproduction",
        "keep route protocol and method notes under reproduction/",
    ),
    _MoveSpec(
        re.compile(r"^evidence/evidence-index\.csv$"),
        "evidence/evidence-index.csv",
        "evidence",
        "preserve the canonical evidence index in place",
    ),
    _MoveSpec(
        re.compile(r"^evidence/claim-evidence-map\.csv$"),
        "evidence/claim-evidence-map.csv",
        "evidence",
        "preserve the claim-to-evidence map in place",
    ),
    _MoveSpec(
        re.compile(r"^evidence/manifest-sha256\.txt$"),
        "evidence/manifest-sha256.txt",
        "evidence",
        "preserve the route evidence manifest in place",
    ),
    _MoveSpec(
        re.compile(r"^evidence/(?P<tail>.+)$"),
        "evidence/{tail}",
        "evidence",
        "retain additional evidence support files under evidence/",
    ),
    _MoveSpec(
        re.compile(r"^reproduction/(?P<tail>.+)$"),
        "reproduction/{tail}",
        "reproduction",
        "preserve route reproduction material under reproduction/",
    ),
    _MoveSpec(
        re.compile(r"^system/(?P<tail>.+)$"),
        "reproduction/system/{tail}",
        "reproduction",
        "group route system context under reproduction/system/",
    ),
)
_REQUIRED_CORE = (
    "route-manifest.json",
    "workbook/source",
    "workbook/generated",
    "evidence/claim-evidence-map.csv",
    "evidence/manifest-sha256.txt",
    "reproduction/README.md",
    "validation/integrity-validation.json",
    "validation/validation-report.md",
    "validation/workbook-parity.json",
)
_REQUIRED_NON_CROSS = (
    "results/attempts.csv",
    "results/measurements.csv",
    "results/summary-results.csv",
    "quality/scores.csv",
)
_REQUIRED_CROSS = (
    "results/comparability-matrix.csv",
    "results/route-status-summary.csv",
    "validation/cross-route-validation.json",
)


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(65536), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _route_id(route_root: Path) -> str:
    manifest_path = route_root / "route-manifest.json"
    try:
        payload = json.loads(manifest_path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise ValueError(f"missing required source route manifest: {manifest_path}") from error
    route_id = payload.get("route_id")
    if not isinstance(route_id, str) or not route_id:
        raise ValueError(f"missing required source route_id in {manifest_path}")
    return route_id


def _is_cross_route(route_id: str) -> bool:
    return route_id == "cross-route-comparison"


def _iter_files(route_root: Path) -> tuple[Path, ...]:
    return tuple(sorted((path for path in route_root.rglob("*") if path.is_file()), key=lambda path: path.as_posix()))


def _missing_required_sources(route_root: Path, route_id: str) -> tuple[str, ...]:
    missing = [
        relative
        for relative in _REQUIRED_CORE
        if not (route_root / relative).exists()
    ]
    conditional = _REQUIRED_CROSS if _is_cross_route(route_id) else _REQUIRED_NON_CROSS
    missing.extend(relative for relative in conditional if not (route_root / relative).exists())
    if not any(path.suffix == ".md" for path in (route_root / "workbook/source").glob("*-final-report.md")):
        missing.append("workbook/source/*-final-report.md")
    if not any(path.suffix == ".docx" for path in (route_root / "workbook/generated").glob("*-final-report.docx")):
        missing.append("workbook/generated/*-final-report.docx")
    if not any(path.suffix == ".pdf" for path in (route_root / "workbook/generated").glob("*-final-report.pdf")):
        missing.append("workbook/generated/*-final-report.pdf")
    return tuple(sorted(set(missing)))


def _match_move(relative: str, route_id: str) -> PathMove | None:
    for spec in _MOVE_SPECS:
        match = spec.pattern.fullmatch(relative)
        if match is None:
            continue
        destination = spec.destination.format(route_id=route_id, **match.groupdict())
        return PathMove(
            old_path=Path(relative),
            new_path=Path(destination),
            sha256="",
            role=spec.role,
            reason=spec.reason,
        )
    return None


def plan_route_migration(route_root: Path) -> tuple[PathMove, ...]:
    """Return the deterministic old-to-new route file move plan."""
    root = Path(route_root).resolve()
    route_id = _route_id(root)
    missing = _missing_required_sources(root, route_id)
    if missing:
        joined = ", ".join(missing)
        raise ValueError(f"missing required source files: {joined}")

    destinations: dict[Path, Path] = {}
    moves: list[PathMove] = []
    known_validation_files = {name for name, _ in _VALIDATION_RECEIPTS} | {"validation-report.md"}
    for path in _iter_files(root):
        relative = path.relative_to(root).as_posix()
        if relative.startswith("validation/"):
            if Path(relative).name not in known_validation_files:
                raise ValueError(f"unknown role for {relative}")
            continue
        move = _match_move(relative, route_id)
        if move is None:
            raise ValueError(f"unknown role for {relative}")
        if move.new_path in destinations:
            raise ValueError(
                f"duplicate destination {move.new_path.as_posix()} for "
                f"{destinations[move.new_path].as_posix()} and {move.old_path.as_posix()}"
            )
        destinations[move.new_path] = move.old_path
        moves.append(
            PathMove(
                old_path=move.old_path,
                new_path=move.new_path,
                sha256=_sha256(path),
                role=move.role,
                reason=move.reason,
            )
        )
    return tuple(sorted(moves, key=lambda move: move.old_path.as_posix()))


def _legacy_validation_summary(report_path: Path) -> bool | None:
    text = report_path.read_text(encoding="utf-8")
    match = re.search(r"overall result:\s*\*\*(passed|failed)\*\*", text, re.IGNORECASE)
    if match is None:
        return None
    return match.group(1).casefold() == "passed"


def consolidate_validation(route_root: Path) -> dict[str, object]:
    """Collapse legacy validation receipts into one stable payload."""
    root = Path(route_root).resolve()
    route_id = _route_id(root)
    validation_root = root / "validation"
    checks: dict[str, dict[str, object]] = {}
    findings: list[dict[str, str]] = []
    limitations: list[dict[str, str]] = []
    for filename, check_name in _VALIDATION_RECEIPTS:
        path = validation_root / filename
        if not path.is_file():
            continue
        try:
            payload = json.loads(path.read_text(encoding="utf-8"))
        except (OSError, UnicodeError, json.JSONDecodeError) as error:
            raise ValueError(f"invalid validation receipt {path}") from error
        if not isinstance(payload, Mapping):
            raise ValueError(f"invalid validation receipt {path}")
        normalized = dict(payload)
        normalized["valid"] = receipt_valid(payload, default_key="matches")
        checks[check_name] = normalized
        findings.extend(
            {"check": check_name, "message": message}
            for message in extract_validation_messages(payload, "findings")
        )
        findings.extend(
            {"check": check_name, "message": message}
            for message in extract_validation_messages(payload, "issues")
        )
        limitations.extend(
            {"check": check_name, "message": message}
            for message in extract_validation_messages(payload, "limitations")
        )
    if not checks:
        raise ValueError(f"missing required source validation receipts in {validation_root}")
    valid = all(bool(payload["valid"]) for payload in checks.values())
    legacy_summary = validation_root / "validation-report.md"
    if legacy_summary.is_file():
        legacy_valid = _legacy_validation_summary(legacy_summary)
        if legacy_valid is not None and legacy_valid != valid:
            raise ValueError(
                f"validation disagreement between consolidated receipts and {legacy_summary}"
            )
    return {
        "route_id": route_id,
        "valid": valid,
        "status": "passed" if valid else "failed",
        "checks": checks,
        "findings": findings,
        "limitations": limitations,
    }


def render_validation_markdown(payload: Mapping[str, object]) -> str:
    """Render the compact human-readable validation summary from one payload."""
    checks = payload.get("checks", {})
    if not isinstance(checks, Mapping):
        raise ValueError("validation payload must contain checks")
    lines = [
        "# Validation summary",
        "",
        f"Overall result: **{'Passed' if payload.get('valid') else 'Failed'}**",
        "",
        "| Check | Result | Findings | Limitations |",
        "| --- | --- | ---: | ---: |",
    ]
    findings = payload.get("findings", ())
    limitations = payload.get("limitations", ())
    for name, check in checks.items():
        check_mapping = check if isinstance(check, Mapping) else {}
        check_findings = [
            item
            for item in findings
            if isinstance(item, Mapping) and item.get("check") == name
        ]
        check_limitations = [
            item
            for item in limitations
            if isinstance(item, Mapping) and item.get("check") == name
        ]
        lines.append(
            f"| `{name}` | {'Passed' if check_mapping.get('valid') else 'Failed'} | "
            f"{len(check_findings)} | {len(check_limitations)} |"
        )
    if findings:
        lines.extend(("", "## Findings", ""))
        lines.extend(
            f"- `{item['check']}`: {item['message']}"
            for item in findings
            if isinstance(item, Mapping)
            and isinstance(item.get("check"), str)
            and isinstance(item.get("message"), str)
        )
    if limitations:
        lines.extend(("", "## Limitations", ""))
        lines.extend(
            f"- `{item['check']}`: {item['message']}"
            for item in limitations
            if isinstance(item, Mapping)
            and isinstance(item.get("check"), str)
            and isinstance(item.get("message"), str)
        )
    return "\n".join(lines) + "\n"


def _text_files(root: Path) -> tuple[Path, ...]:
    return tuple(
        sorted(
            (
                path
                for path in root.rglob("*")
                if path.is_file() and path.suffix.casefold() in _TEXT_EXTENSIONS
            ),
            key=lambda path: path.as_posix(),
        )
    )


def validate_layout(route_root: Path) -> tuple[str, ...]:
    """Return deterministic contract violations for a compact route layout."""
    root = Path(route_root).resolve()
    issues: list[str] = []
    present = {
        entry.name
        for entry in root.iterdir()
        if entry.is_dir() or entry.name == "README.md"
    } if root.exists() else set()
    for required in _TOP_LEVEL_COMPONENTS:
        if required not in present:
            issues.append(f"missing_layout_part:{required}")
    for removed in _REMOVED_TOP_LEVELS:
        if (root / removed).exists():
            issues.append(f"stale_layout_part:{removed}")
    validation_json = root / "validation/validation.json"
    validation_md = root / "validation/validation.md"
    if not validation_json.is_file():
        issues.append("missing_layout_part:validation/validation.json")
    if not validation_md.is_file():
        issues.append("missing_layout_part:validation/validation.md")
    for filename, _ in _VALIDATION_RECEIPTS:
        if (root / "validation" / filename).exists():
            issues.append(f"stale_removed_path:validation/{filename}")
    if (root / "validation/validation-report.md").exists():
        issues.append("stale_removed_path:validation/validation-report.md")
    manifest = root / "evidence/manifest-sha256.txt"
    if manifest.is_file():
        content = manifest.read_text(encoding="utf-8")
        if re.search(r"(^|\s)evidence/manifest-sha256\.txt$", content, re.MULTILINE):
            issues.append("manifest_self_inclusion:evidence/manifest-sha256.txt")
    stale_prefixes = tuple(f"{name}/" for name in _REMOVED_TOP_LEVELS) + ("route-manifest.json",)
    for path in _text_files(root):
        text = path.read_text(encoding="utf-8")
        relative = path.relative_to(root).as_posix()
        if any(prefix in text for prefix in stale_prefixes):
            issues.append(f"stale_removed_path:{relative}")
    if validation_json.is_file() and validation_md.is_file():
        payload = json.loads(validation_json.read_text(encoding="utf-8"))
        if not isinstance(payload, Mapping):
            issues.append("invalid_validation_payload:validation/validation.json")
        else:
            expected = render_validation_markdown(payload)
            actual = validation_md.read_text(encoding="utf-8")
            if actual != expected:
                issues.append("validation_disagreement:validation/validation.md")
    return tuple(sorted(set(issues)))


__all__ = [
    "PathMove",
    "consolidate_validation",
    "plan_route_migration",
    "render_validation_markdown",
    "validate_layout",
]
