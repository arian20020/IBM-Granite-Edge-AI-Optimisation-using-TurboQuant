"""Capture exact pinned GitHub documents and fenced commands without executing them."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import urllib.request
from dataclasses import asdict, dataclass
from pathlib import Path, PurePosixPath
from typing import Any, Iterable


CAMPAIGN_ID = "GTQ-WB05-MF-v1"
ROUTES = {"route-a-merged-openvino", "route-b-experimental-qjl-polar"}
COMMIT_PATTERN = re.compile(r"^[0-9a-f]{40}$")
REPOSITORY_PATTERN = re.compile(r"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$")


@dataclass(frozen=True)
class CommandBlock:
    """One verbatim fenced block and its source location."""

    command_id: str
    document_id: str
    heading: str
    language: str
    start_line: int
    end_line: int
    verbatim_text: str
    sha256: str


def validate_document_spec(specification: dict[str, Any]) -> None:
    """Reject unpinned or path-escaping source specifications."""

    repository = str(specification.get("repository_full_name", ""))
    commit = str(specification.get("commit", ""))
    repository_path = str(specification.get("path", ""))
    if not REPOSITORY_PATTERN.fullmatch(repository):
        raise ValueError("repository_full_name must be owner/repository")
    if not COMMIT_PATTERN.fullmatch(commit):
        raise ValueError("commit must be a lowercase 40-character SHA")
    if not repository_path or "\\" in repository_path or "#" in repository_path or "?" in repository_path:
        raise ValueError("path must be a clean repository-relative POSIX path")
    parsed = PurePosixPath(repository_path)
    if parsed.is_absolute() or ".." in parsed.parts or "." in parsed.parts:
        raise ValueError("path must be a clean repository-relative POSIX path")


def build_raw_github_url(repository_full_name: str, commit: str, repository_path: str) -> str:
    """Construct the sole allowed document-download URL form."""

    specification = {"repository_full_name": repository_full_name, "commit": commit, "path": repository_path}
    validate_document_spec(specification)
    return f"https://raw.githubusercontent.com/{repository_full_name}/{commit}/{repository_path}"


def extract_fenced_commands(document_id: str, markdown_text: str) -> list[CommandBlock]:
    """Extract fenced blocks verbatim without interpreting shell syntax."""

    lines = markdown_text.splitlines()
    current_heading = "Document root"
    commands: list[CommandBlock] = []
    index = 0
    while index < len(lines):
        line = lines[index]
        if line.startswith("#"):
            current_heading = line.lstrip("#").strip() or "Document root"
            index += 1
            continue
        if not line.startswith("```"):
            index += 1
            continue

        language = line[3:].strip().lower()
        opening_line = index + 1
        index += 1
        content: list[str] = []
        while index < len(lines) and not lines[index].startswith("```"):
            content.append(lines[index])
            index += 1
        if index >= len(lines):
            raise ValueError(f"Unclosed code fence in {document_id} at line {opening_line}")

        verbatim = "\n".join(content)
        command_number = len(commands) + 1
        commands.append(
            CommandBlock(
                command_id=f"{document_id}-C{command_number:03d}",
                document_id=document_id,
                heading=current_heading,
                language=language,
                start_line=opening_line + 1,
                end_line=index,
                verbatim_text=verbatim,
                sha256=hashlib.sha256(verbatim.encode("utf-8")).hexdigest(),
            )
        )
        index += 1
    return commands


def _download(url: str, maximum_bytes: int) -> bytes:
    """Download one bounded UTF-8 document with a finite timeout."""

    request = urllib.request.Request(url, headers={"User-Agent": "GTQ-WB05-MF-v1-document-capture"})
    with urllib.request.urlopen(request, timeout=30) as response:
        data = response.read(maximum_bytes + 1)
    if len(data) > maximum_bytes:
        raise ValueError(f"Document exceeded maximum size of {maximum_bytes} bytes: {url}")
    data.decode("utf-8", errors="strict")
    return data


def capture_documents(config_path: Path, output_directory: Path) -> list[Path]:
    """Capture allowlisted documents and inert command manifests by route."""

    configuration = json.loads(config_path.read_text(encoding="utf-8-sig"))
    allowed_repositories = set(configuration["allowed_repositories"])
    documents_directory = output_directory / "documents"
    commands_directory = output_directory / "commands"
    documents_directory.mkdir(parents=True, exist_ok=True)
    commands_directory.mkdir(parents=True, exist_ok=True)
    route_documents: dict[str, list[dict[str, Any]]] = {route: [] for route in ROUTES}
    route_commands: dict[str, list[dict[str, Any]]] = {route: [] for route in ROUTES}
    created: list[Path] = []

    for specification in configuration["documents"]:
        validate_document_spec(specification)
        repository = specification["repository_full_name"]
        route = specification["route_id"]
        if repository not in allowed_repositories:
            raise ValueError(f"Repository is not allowlisted: {repository}")
        if route not in ROUTES:
            raise ValueError(f"Unknown route: {route}")

        url = build_raw_github_url(repository, specification["commit"], specification["path"])
        data = _download(url, int(specification["maximum_bytes"]))
        snapshot = documents_directory / f"{specification['document_id']}.md"
        snapshot.write_bytes(data)
        created.append(snapshot)
        text = data.decode("utf-8", errors="strict")
        blocks = extract_fenced_commands(specification["document_id"], text)
        route_documents[route].append(
            {
                "document_id": specification["document_id"],
                "repository_full_name": repository,
                "commit": specification["commit"],
                "path": specification["path"],
                "snapshot_path": snapshot.relative_to(output_directory).as_posix(),
                "sha256": hashlib.sha256(data).hexdigest(),
            }
        )
        route_commands[route].extend(asdict(block) for block in blocks)

    for route in sorted(ROUTES):
        manifest = {
            "schema_version": "1.0",
            "campaign_id": CAMPAIGN_ID,
            "route_id": route,
            "execution_allowed": False,
            "documents": route_documents[route],
            "commands": route_commands[route],
        }
        destination = commands_directory / f"{route}-documented-commands.json"
        destination.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
        created.append(destination)
    return created


def main(argv: Iterable[str] | None = None) -> int:
    """CLI for the read-only preflight orchestrator."""

    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config", type=Path, required=True)
    parser.add_argument("--output-directory", type=Path, required=True)
    arguments = parser.parse_args(list(argv) if argv is not None else None)
    paths = capture_documents(arguments.config.resolve(), arguments.output_directory.resolve())
    for created in paths:
        print(f"Captured: {created}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
