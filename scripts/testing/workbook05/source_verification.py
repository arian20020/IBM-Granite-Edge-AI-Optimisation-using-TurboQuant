"""Verify or clone immutable Workbook 05 external source trees."""

from __future__ import annotations

import hashlib
import json
import re
import subprocess
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Mapping, Sequence


ALLOWED_REPOSITORIES = {
    "openvinotoolkit/openvino": "https://github.com/openvinotoolkit/openvino.git",
    "openvinotoolkit/openvino.genai": "https://github.com/openvinotoolkit/openvino.genai.git",
    "EgorDuplensky/openvino": "https://github.com/EgorDuplensky/openvino.git",
}
COMMIT_PATTERN = re.compile(r"^[0-9a-f]{40}$")


@dataclass(frozen=True)
class SourceTreeResult:
    """One source-tree report plus every command record used to derive it."""

    report: dict[str, Any]
    command_records: tuple[dict[str, Any], ...]


def _utc_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def _safe_id(value: str) -> str:
    safe = re.sub(r"[^A-Za-z0-9._-]+", "-", value).strip("-")
    if not safe:
        raise ValueError("Command identifier cannot be empty.")
    return safe[:128]


def _write_text(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8", newline="\n")


def _run_command(
    argv: Sequence[str],
    working_directory: Path,
    evidence_directory: Path,
    command_id: str,
    timeout_seconds: int,
    *,
    check: bool = True,
) -> tuple[subprocess.CompletedProcess[str], dict[str, Any]]:
    """Run one argument-list command and preserve stdout, stderr, and timing."""

    command_id = _safe_id(command_id)
    stdout_path = evidence_directory / f"{command_id}.stdout.txt"
    stderr_path = evidence_directory / f"{command_id}.stderr.txt"
    record_path = evidence_directory / f"{command_id}.command.json"
    started = _utc_now()
    timed_out = False

    try:
        completed = subprocess.run(
            list(argv),
            cwd=working_directory,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=timeout_seconds,
            shell=False,
            check=False,
        )
    except subprocess.TimeoutExpired as error:
        timed_out = True
        stdout = error.stdout if isinstance(error.stdout, str) else ""
        stderr = error.stderr if isinstance(error.stderr, str) else ""
        completed = subprocess.CompletedProcess(list(argv), -1, stdout, stderr)

    ended = _utc_now()
    _write_text(stdout_path, completed.stdout or "")
    _write_text(stderr_path, completed.stderr or "")
    record = {
        "command_id": command_id,
        "argv": list(argv),
        "working_directory": str(working_directory),
        "started_utc": started,
        "ended_utc": ended,
        "timeout_seconds": timeout_seconds,
        "timed_out": timed_out,
        "exit_code": completed.returncode,
        "stdout_path": stdout_path.name,
        "stderr_path": stderr_path.name,
    }
    _write_text(record_path, json.dumps(record, indent=2) + "\n")

    if check and completed.returncode != 0:
        raise RuntimeError(
            f"Command {command_id!r} failed with exit code "
            f"{completed.returncode}; see {stderr_path}."
        )
    return completed, record


def _validate_source_specification(
    specification: Mapping[str, Any],
    *,
    allow_local_origins_for_tests: bool,
) -> tuple[str, str, str]:
    repository = str(specification.get("repository_full_name", ""))
    origin = str(specification.get("origin_url", ""))
    commit = str(specification.get("commit", ""))

    if repository not in ALLOWED_REPOSITORIES:
        raise ValueError(f"Repository is not allowlisted: {repository!r}")
    if not COMMIT_PATTERN.fullmatch(commit):
        raise ValueError(
            f"Commit must be a lowercase 40-character SHA: {commit!r}"
        )
    expected_origin = ALLOWED_REPOSITORIES[repository]
    if not allow_local_origins_for_tests and origin != expected_origin:
        raise ValueError(
            f"Origin for {repository} must be {expected_origin!r}; found {origin!r}."
        )
    if allow_local_origins_for_tests and not origin:
        raise ValueError("Local test origin cannot be empty.")
    return repository, origin, commit


def _parse_gitmodules(text: str) -> dict[str, dict[str, str]]:
    modules: dict[str, dict[str, str]] = {}
    pattern = re.compile(r"^submodule\.(?P<name>.+)\.(?P<field>path|url)\s+(?P<value>.+)$")
    for raw_line in text.splitlines():
        match = pattern.match(raw_line.strip())
        if not match:
            continue
        modules.setdefault(match.group("name"), {})[match.group("field")] = match.group("value")
    return modules


def _parse_submodule_status(
    status_text: str,
    module_configuration: Mapping[str, Mapping[str, str]],
) -> tuple[list[dict[str, str]], bool]:
    by_path = {
        values["path"]: values.get("url", "")
        for values in module_configuration.values()
        if "path" in values
    }
    rows: list[dict[str, str]] = []
    observed_paths: set[str] = set()
    complete = True

    for raw_line in status_text.splitlines():
        if not raw_line:
            continue
        prefix = raw_line[0]
        fields = raw_line[1:].strip().split(maxsplit=2)
        if len(fields) < 2:
            complete = False
            continue
        commit, path = fields[0], fields[1]
        observed_paths.add(path)
        status = {
            " ": "clean",
            "+": "modified",
            "-": "uninitialised",
            "U": "conflict",
        }.get(prefix, "conflict")
        if status != "clean" or not COMMIT_PATTERN.fullmatch(commit):
            complete = False
        rows.append(
            {
                "path": path,
                "url": by_path.get(path, ""),
                "commit": commit if COMMIT_PATTERN.fullmatch(commit) else "0" * 40,
                "status": status,
            }
        )

    if set(by_path) != observed_paths:
        complete = False
        for missing_path in sorted(set(by_path) - observed_paths):
            rows.append(
                {
                    "path": missing_path,
                    "url": by_path[missing_path],
                    "commit": "0" * 40,
                    "status": "uninitialised",
                }
            )
    return rows, complete


def verify_source_tree(
    specification: Mapping[str, Any],
    source_directory: Path,
    evidence_directory: Path,
    timeout_seconds: int,
    *,
    allow_local_origins_for_tests: bool = False,
) -> SourceTreeResult:
    """Clone an absent tree or verify a matching existing tree without reset/clean."""

    repository, expected_origin, expected_commit = _validate_source_specification(
        specification,
        allow_local_origins_for_tests=allow_local_origins_for_tests,
    )
    source_directory = source_directory.resolve()
    evidence_directory = evidence_directory.resolve()
    evidence_directory.mkdir(parents=True, exist_ok=True)
    command_records: list[dict[str, Any]] = []
    source_role = str(specification.get("source_role", "runtime"))
    route_id = str(specification.get("route_id", ""))
    prefix = _safe_id(f"{route_id}-{source_role}")

    if source_directory.exists() and not (source_directory / ".git").exists():
        report = {
            "schema_version": "1.0",
            "campaign_id": "GTQ-WB05-MF-v1",
            "phase_id": "phase-1-source-admission",
            "route_id": route_id,
            "source_role": source_role,
            "repository_full_name": repository,
            "expected_origin_url": expected_origin,
            "actual_origin_url": "",
            "expected_commit": expected_commit,
            "actual_commit": "0" * 40,
            "working_tree_clean": False,
            "submodules_complete": False,
            "submodules": [],
            "command_records": [],
            "decision": "Blocked",
            "decision_reason": "The source directory exists but is not a Git repository; it was not modified.",
        }
        return SourceTreeResult(report, ())

    if not source_directory.exists():
        source_directory.parent.mkdir(parents=True, exist_ok=True)
        commands = (
            (["git", "clone", "--no-checkout", expected_origin, str(source_directory)], source_directory.parent, "clone"),
            (["git", "-C", str(source_directory), "fetch", "--no-tags", "origin", expected_commit], source_directory.parent, "fetch-exact"),
            (["git", "-C", str(source_directory), "checkout", "--detach", expected_commit], source_directory.parent, "checkout-exact"),
            (["git", "-C", str(source_directory), "submodule", "sync", "--recursive"], source_directory.parent, "submodule-sync"),
            (["git", "-C", str(source_directory), "-c", "protocol.file.allow=never", "submodule", "update", "--init", "--recursive"], source_directory.parent, "submodule-update"),
        )
        for argv, cwd, suffix in commands:
            _, record = _run_command(
                argv,
                cwd,
                evidence_directory,
                f"{prefix}-{suffix}",
                timeout_seconds,
            )
            command_records.append(record)

    def run_git(arguments: Sequence[str], suffix: str, *, check: bool = True) -> subprocess.CompletedProcess[str]:
        completed, record = _run_command(
            ["git", "-C", str(source_directory), *arguments],
            source_directory,
            evidence_directory,
            f"{prefix}-{suffix}",
            timeout_seconds,
            check=check,
        )
        command_records.append(record)
        return completed

    origin = run_git(["remote", "get-url", "origin"], "origin").stdout.strip()
    head = run_git(["rev-parse", "HEAD"], "head").stdout.strip()
    status_text = run_git(
        ["status", "--porcelain=v1", "--untracked-files=all"],
        "status",
    ).stdout
    clean = status_text.strip() == ""

    # A dry-run fetch verifies that the controlled commit is still reachable
    # without altering an already verified working tree.
    if origin == expected_origin and head == expected_commit and clean:
        run_git(
            ["fetch", "--no-tags", "--dry-run", "origin", expected_commit],
            "reachability",
        )

    gitmodules_text = ""
    if (source_directory / ".gitmodules").is_file():
        gitmodules = run_git(
            [
                "config",
                "--file",
                ".gitmodules",
                "--get-regexp",
                r"^submodule\..*\.(path|url)$",
            ],
            "gitmodules",
            check=False,
        )
        if gitmodules.returncode not in (0, 1):
            raise RuntimeError("Git could not read .gitmodules.")
        gitmodules_text = gitmodules.stdout

    submodule_status = run_git(
        ["submodule", "status", "--recursive"],
        "submodule-status",
    ).stdout
    submodules, submodules_complete = _parse_submodule_status(
        submodule_status,
        _parse_gitmodules(gitmodules_text),
    )

    reasons: list[str] = []
    if origin != expected_origin:
        reasons.append(
            f"Origin mismatch: expected {expected_origin!r}; found {origin!r}."
        )
    if head != expected_commit:
        reasons.append(
            f"Commit mismatch: expected {expected_commit}; found {head}."
        )
    if not clean:
        reasons.append("The source tree is not clean.")
    if not submodules_complete:
        reasons.append("One or more recursive submodules are incomplete or modified.")

    report = {
        "schema_version": "1.0",
        "campaign_id": "GTQ-WB05-MF-v1",
        "phase_id": "phase-1-source-admission",
        "route_id": route_id,
        "source_role": source_role,
        "repository_full_name": repository,
        "expected_origin_url": expected_origin,
        "actual_origin_url": origin,
        "expected_commit": expected_commit,
        "actual_commit": head if COMMIT_PATTERN.fullmatch(head) else "0" * 40,
        "working_tree_clean": clean,
        "submodules_complete": submodules_complete,
        "submodules": submodules,
        "command_records": [f"commands/{record['command_id']}.command.json" for record in command_records],
        "decision": "Passed" if not reasons else "Blocked",
        "decision_reason": (
            "The exact origin, commit, clean tree, and recursive submodules were verified."
            if not reasons
            else " ".join(reasons)
        ),
    }
    return SourceTreeResult(report, tuple(command_records))
