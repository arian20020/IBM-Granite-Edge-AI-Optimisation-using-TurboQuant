"""Validate a self-hosted Workbook 05 preflight bundle as untrusted data."""

from __future__ import annotations

import argparse
import json
from dataclasses import dataclass
from pathlib import Path, PurePath
from typing import Any, Iterable

from scripts.testing.workbook05.hash_manifest import verify_hash_manifest
from scripts.testing.workbook05.schema_validation import validate_json_file
from scripts.testing.workbook05.source_admission import evaluate_source_admission


REQUIRED_PATHS = (
    "preflight-report.json",
    "preflight-summary.md",
    "environment-snapshot.json",
    "controls/campaign-manifest.json",
    "controls/checkpoint.json",
    "controls/route-a-source-admission.json",
    "controls/route-b-source-admission.json",
    "hash-manifest.sha256",
)
FORBIDDEN_SUFFIXES = {".gguf", ".safetensors", ".onnx", ".pt", ".pth", ".ckpt", ".exe", ".dll"}
SECRET_PATTERNS = ("ghp_", "github_pat_", "--token ", "HF_TOKEN=", "HUGGING_FACE_HUB_TOKEN=")


@dataclass(frozen=True)
class BundleIssue:
    """One deterministic validation finding in an untrusted evidence bundle."""

    code: str
    path: str
    message: str


def _add_schema_issues(
    issues: list[BundleIssue],
    instance: Path,
    schema: Path,
    relative: str,
) -> None:
    """Translate shared schema issues into bundle findings."""

    try:
        for problem in validate_json_file(instance, schema):
            issues.append(BundleIssue("SCHEMA_INVALID", relative, f"{problem.json_path}: {problem.message}"))
    except (OSError, json.JSONDecodeError, UnicodeError) as error:
        issues.append(BundleIssue("SCHEMA_INVALID", relative, str(error)))


def _walk_paths(value: Any, location: str = "$") -> list[tuple[str, str]]:
    """Collect explicit *_path fields so parent traversal cannot hide in JSON."""

    found: list[tuple[str, str]] = []
    if isinstance(value, dict):
        for key, child in value.items():
            child_location = f"{location}.{key}"
            if key.lower().endswith("_path") and isinstance(child, str) and child:
                found.append((child_location, child))
            found.extend(_walk_paths(child, child_location))
    elif isinstance(value, list):
        for index, child in enumerate(value):
            found.extend(_walk_paths(child, f"{location}[{index}]"))
    return found


def validate_preflight_bundle(bundle_root: Path, repository_root: Path) -> list[BundleIssue]:
    """Return every integrity, schema, admission, secret, and path issue."""

    bundle_root = bundle_root.resolve()
    repository_root = repository_root.resolve()
    issues: list[BundleIssue] = []
    for relative in REQUIRED_PATHS:
        if not (bundle_root / relative).is_file():
            issues.append(BundleIssue("REQUIRED_PATH_MISSING", relative, "Required evidence file is missing."))

    manifest = bundle_root / "hash-manifest.sha256"
    if manifest.is_file():
        for message in verify_hash_manifest(bundle_root, manifest):
            issues.append(BundleIssue("HASH_MISMATCH", "hash-manifest.sha256", message))

    schema_root = repository_root / "experiments/granite_turboquant_intel/schemas/workbook05"
    schema_bindings = (
        ("preflight-report.json", "preflight-report.schema.json"),
        ("controls/campaign-manifest.json", "campaign-manifest.schema.json"),
        ("controls/checkpoint.json", "checkpoint.schema.json"),
        ("controls/route-a-source-admission.json", "source-admission.schema.json"),
        ("controls/route-b-source-admission.json", "source-admission.schema.json"),
    )
    for relative, schema_name in schema_bindings:
        instance = bundle_root / relative
        if instance.is_file():
            _add_schema_issues(issues, instance, schema_root / schema_name, relative)

    for command_manifest in sorted((bundle_root / "commands").glob("*.json")) if (bundle_root / "commands").is_dir() else []:
        _add_schema_issues(
            issues,
            command_manifest,
            schema_root / "documented-command-manifest.schema.json",
            command_manifest.relative_to(bundle_root).as_posix(),
        )

    for relative in ("controls/route-a-source-admission.json", "controls/route-b-source-admission.json"):
        record_path = bundle_root / relative
        if not record_path.is_file():
            continue
        try:
            record = json.loads(record_path.read_text(encoding="utf-8-sig"))
            decision = evaluate_source_admission(record)
            if record.get("admission_status") == "Admitted" and not decision.permitted:
                issues.append(BundleIssue("FALSE_ADMISSION", relative, "; ".join(decision.reasons)))
        except (KeyError, TypeError, json.JSONDecodeError, UnicodeError) as error:
            issues.append(BundleIssue("FALSE_ADMISSION", relative, str(error)))

    for candidate in sorted(path for path in bundle_root.rglob("*") if path.is_file()):
        relative = candidate.relative_to(bundle_root).as_posix()
        if candidate.suffix.lower() in FORBIDDEN_SUFFIXES:
            issues.append(BundleIssue("FORBIDDEN_BINARY", relative, f"Forbidden evidence suffix: {candidate.suffix}"))
            continue
        try:
            text = candidate.read_text(encoding="utf-8")
        except (UnicodeDecodeError, OSError):
            text = ""
        for pattern in SECRET_PATTERNS:
            if pattern in text:
                issues.append(BundleIssue("SECRET_PATTERN", relative, f"Matched forbidden secret pattern: {pattern}"))
        if candidate.suffix.lower() == ".json" and text:
            try:
                value = json.loads(text)
            except json.JSONDecodeError:
                continue
            for json_location, evidence_path in _walk_paths(value):
                parsed = PurePath(evidence_path)
                if parsed.is_absolute() or ".." in parsed.parts:
                    issues.append(BundleIssue("UNSAFE_EVIDENCE_PATH", relative, f"{json_location}: {evidence_path}"))
    return sorted(issues, key=lambda issue: (issue.code, issue.path, issue.message))


def main(argv: Iterable[str] | None = None) -> int:
    """CLI used by the GitHub-hosted validation job."""

    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bundle-root", type=Path, required=True)
    parser.add_argument("--repository-root", type=Path, required=True)
    parser.add_argument("--summary", type=Path)
    arguments = parser.parse_args(list(argv) if argv is not None else None)
    issues = validate_preflight_bundle(arguments.bundle_root, arguments.repository_root)
    lines = ["# Workbook 05 preflight artifact validation", ""]
    if issues:
        lines.append(f"Validation failed with {len(issues)} issue(s).")
        lines.append("")
        lines.extend(f"- `{issue.code}` `{issue.path}` â€” {issue.message}" for issue in issues)
    else:
        lines.append("Validation passed: hashes, schemas, source-admission boundaries, paths, and secret checks are valid.")
    output = "\n".join(lines) + "\n"
    print(output, end="")
    if arguments.summary:
        arguments.summary.parent.mkdir(parents=True, exist_ok=True)
        arguments.summary.write_text(output, encoding="utf-8")
    return 1 if issues else 0


if __name__ == "__main__":
    raise SystemExit(main())
