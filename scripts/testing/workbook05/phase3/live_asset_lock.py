"""Live Workbook 05 C1 model acquisition and record materialisation.

The module keeps network/model work behind explicit CLI subcommands so importing
it during repository tests performs no network call and loads no model. The
PowerShell orchestrator owns stage order and native-process supervision; this
module owns deterministic Python records, immutable Hub resolution, inventories,
and schema validation.
"""

from __future__ import annotations

import argparse
import csv
import json
import os
import re
import shutil
import stat
import sys
from dataclasses import dataclass
from pathlib import Path, PurePosixPath, PureWindowsPath
from typing import Any, Mapping, Sequence

from scripts.testing.workbook05.phase3.contracts import assert_phase3_record
from scripts.testing.workbook05.phase3.conversion import (
    ConversionFile,
    ConversionPackage,
    ConversionRequest,
    collect_conversion_record,
)
from scripts.testing.workbook05.phase3.dependency_acceptance import (
    ACCEPTED_DEPENDENCY_DECISION_SHA256,
    ACCEPTED_OPTIMUM_COMMIT,
    ACCEPTED_OPTIMUM_INTEL_COMMIT,
)
from scripts.testing.workbook05.phase3.disk_preflight import collect_disk_preflight
from scripts.testing.workbook05.phase3.hashing import sha256_file, sha256_tree
from scripts.testing.workbook05.phase3.model_assets import (
    FORMAL_GRANITE_REPOSITORY,
    HubApi,
    ResolvedModel,
    download_snapshot,
    resolve_model,
)


# ---------------------------------------------------------------------------
# Reviewed C1 identities and required conversion outputs.
# ---------------------------------------------------------------------------

CAMPAIGN_ID = "GTQ-WB05-MF-v1"
ROUTE_ID = "route-a-merged-openvino"
FORMAL_ASSET_ID = "MODEL-WB05-GRANITE41-3B-INT4A-G128-R100"
FORMAL_CONVERSION_ID = "CONV-WB05-GRANITE41-3B-INT4A-G128-R100"
REQUESTED_REVISION = "main"
REQUIRED_CONVERTED_FILES = (
    "openvino_model.xml",
    "openvino_model.bin",
    "config.json",
)
_TOKENIZER_NAMES = {
    "added_tokens.json",
    "chat_template.jinja",
    "merges.txt",
    "sentencepiece.bpe.model",
    "special_tokens_map.json",
    "tokenizer.json",
    "tokenizer.model",
    "tokenizer_config.json",
    "vocab.json",
    "vocab.txt",
}
_SHA256_PATTERN = re.compile(r"^[0-9a-f]{64}$")


@dataclass(frozen=True, slots=True)
class FileIdentity:
    """One regular file bound to its portable path, size, and SHA-256."""

    relative_path: str
    size_bytes: int
    sha256: str

    def as_record(self) -> dict[str, Any]:
        """Return the JSON/CSV shape shared by C1 records."""

        return {
            "relative_path": self.relative_path,
            "size_bytes": self.size_bytes,
            "sha256": self.sha256,
        }


class HuggingFaceHubAdapter(HubApi):
    """Narrow live adapter around the pinned huggingface_hub package."""

    def __init__(self) -> None:
        # Import only when the live subcommand is called. Repository tests can
        # import this module without installing or contacting Hugging Face.
        try:
            from huggingface_hub import HfApi, snapshot_download
        except ImportError as error:
            raise ValueError(
                "The accepted dependency environment does not expose huggingface_hub."
            ) from error
        self._api = HfApi()
        self._snapshot_download = snapshot_download

    def model_info(self, repo_id: str, revision: str) -> Any:
        """Resolve one repository request using the pinned Hub client."""

        return self._api.model_info(repo_id=repo_id, revision=revision)

    def snapshot_download(
        self,
        *,
        repo_id: str,
        revision: str,
        local_dir: str,
        allow_patterns: list[str],
    ) -> str:
        """Download exactly the immutable allow-listed snapshot."""

        return str(
            self._snapshot_download(
                repo_id=repo_id,
                revision=revision,
                local_dir=local_dir,
                allow_patterns=allow_patterns,
                local_dir_use_symlinks=False,
            )
        )


def _portable_path(value: str) -> str:
    """Require one canonical relative POSIX file path."""

    if not isinstance(value, str) or not value or value != value.strip():
        raise ValueError("Asset path must be one non-empty canonical string.")
    if "\\" in value or "\x00" in value or value.startswith("/"):
        raise ValueError(f"Asset path is not portable: {value!r}")
    parts = value.split("/")
    if any(part in {"", ".", ".."} for part in parts):
        raise ValueError(f"Asset path contains an unsafe segment: {value!r}")
    posix = PurePosixPath(value)
    windows = PureWindowsPath(value)
    if posix.is_absolute() or windows.is_absolute() or windows.drive:
        raise ValueError(f"Asset path must remain relative: {value!r}")
    if posix.as_posix() != value:
        raise ValueError(f"Asset path is not canonical: {value!r}")
    return value


def _is_tokenizer_path(relative_path: str) -> bool:
    """Classify source files needed to reproduce tokenisation/chat formatting."""

    lowered = relative_path.casefold()
    name = PurePosixPath(relative_path).name.casefold()
    return (
        name in _TOKENIZER_NAMES
        or name.startswith("tokenizer")
        or name.startswith("tokenization_")
        or "/tokenizer" in lowered
    )


def classify_source_paths(
    paths: Sequence[str],
) -> tuple[tuple[str, ...], tuple[str, ...]]:
    """Separate model/config files from tokenizer files without overlap."""

    model: list[str] = []
    tokenizer: list[str] = []
    seen: set[str] = set()
    for raw in paths:
        path = _portable_path(raw)
        identity = path.casefold()
        if identity in seen:
            raise ValueError(f"Source catalogue repeats a Windows path: {path}")
        seen.add(identity)
        if _is_tokenizer_path(path):
            tokenizer.append(path)
        else:
            model.append(path)

    # A formal lock needs at least one actual weight file, not only README/config.
    if not any(
        path.casefold().endswith((".safetensors", ".bin"))
        for path in model
    ):
        raise ValueError("The immutable source catalogue contains no model payload.")
    if not tokenizer:
        raise ValueError("The immutable source catalogue contains no tokenizer payload.")

    key = lambda value: (value.casefold(), value)
    return tuple(sorted(model, key=key)), tuple(sorted(tokenizer, key=key))


def require_converted_outputs(paths: Sequence[str]) -> tuple[str, ...]:
    """Require OpenVINO IR, config, and at least one tokenizer output."""

    canonical = tuple(sorted({_portable_path(path) for path in paths}, key=str.casefold))
    lowered = {path.casefold() for path in canonical}
    for required in REQUIRED_CONVERTED_FILES:
        if required.casefold() not in lowered:
            raise ValueError(f"Converted output is missing required file: {required}")
    if not any(_is_tokenizer_path(path) for path in canonical):
        raise ValueError("Converted output is missing a tokenizer file.")
    return canonical


def _is_link_or_reparse(path: Path) -> bool:
    """Detect links, junctions, and Windows reparse points."""

    if path.is_symlink():
        return True
    attributes = getattr(path.lstat(), "st_file_attributes", 0)
    return bool(attributes & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400))


def _normal_directory(path: Path, label: str) -> Path:
    """Require an existing normal directory."""

    if not path.exists() or not path.is_dir():
        raise ValueError(f"{label} does not exist as a directory: {path}")
    if _is_link_or_reparse(path):
        raise ValueError(f"{label} must not be a link or reparse point: {path}")
    return path.resolve(strict=True)


def _regular_file(path: Path, label: str) -> Path:
    """Require an existing regular non-linked file."""

    if not path.exists() or not path.is_file():
        raise ValueError(f"{label} does not exist as a regular file: {path}")
    if _is_link_or_reparse(path):
        raise ValueError(f"{label} must not be a link or reparse point: {path}")
    return path.resolve(strict=True)


def inventory_tree(root: Path) -> tuple[FileIdentity, ...]:
    """Inventory regular files without following metadata links."""

    resolved_root = _normal_directory(root, "Asset directory")
    files: list[FileIdentity] = []
    seen: set[str] = set()
    for candidate in sorted(
        resolved_root.rglob("*"),
        key=lambda item: item.relative_to(resolved_root).as_posix().casefold(),
    ):
        if _is_link_or_reparse(candidate):
            raise ValueError(f"Asset tree contains a link or reparse point: {candidate}")
        if candidate.is_dir():
            continue
        if not candidate.is_file():
            raise ValueError(f"Asset tree contains a non-regular entry: {candidate}")
        relative = candidate.relative_to(resolved_root).as_posix()
        # huggingface_hub may retain local metadata. Its regular files are not
        # part of the immutable model payload and were not advertised siblings.
        if relative.casefold().startswith(".cache/huggingface/"):
            continue
        relative = _portable_path(relative)
        identity = relative.casefold()
        if identity in seen:
            raise ValueError(f"Asset tree has a case-colliding path: {relative}")
        seen.add(identity)
        files.append(
            FileIdentity(
                relative_path=relative,
                size_bytes=candidate.stat().st_size,
                sha256=sha256_file(candidate),
            )
        )
    if not files:
        raise ValueError(f"Asset inventory is empty: {resolved_root}")
    return tuple(files)


def _write_atomic_json(path: Path, value: Mapping[str, Any]) -> None:
    """Write one create-once UTF-8 JSON record atomically."""

    if path.exists():
        raise ValueError(f"Refusing to overwrite live C1 record: {path}")
    temporary = path.with_name(path.name + ".tmp")
    if temporary.exists():
        raise ValueError(f"Live C1 temporary record already exists: {temporary}")
    payload = (json.dumps(value, indent=2, ensure_ascii=False) + "\n").encode("utf-8")
    try:
        with temporary.open("xb") as stream:
            stream.write(payload)
            stream.flush()
            os.fsync(stream.fileno())
        temporary.replace(path)
    except Exception:
        if temporary.exists():
            temporary.unlink()
        raise


def _write_inventory(path: Path, records: Sequence[FileIdentity]) -> None:
    """Write one deterministic create-once CSV inventory."""

    if path.exists():
        raise ValueError(f"Refusing to overwrite asset inventory: {path}")
    with path.open("x", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(
            stream,
            fieldnames=("relative_path", "size_bytes", "sha256"),
            lineterminator="\n",
        )
        writer.writeheader()
        for record in records:
            writer.writerow(record.as_record())


def _aggregate(root: Path, records: Sequence[FileIdentity]) -> str:
    """Bind names, sizes, and bytes through the shared canonical tree hash."""

    return sha256_tree(
        root,
        [root.joinpath(*record.relative_path.split("/")) for record in records],
    )


def _load_object(path: Path, label: str) -> dict[str, Any]:
    """Read one strict UTF-8 JSON object."""

    try:
        value = json.loads(path.read_bytes().decode("utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise ValueError(f"{label} is invalid: {path}: {error}") from error
    if not isinstance(value, dict):
        raise ValueError(f"{label} must contain one JSON object: {path}")
    return value


def _declared_metadata(source_directory: Path) -> dict[str, Any]:
    """Read architecture declarations from the immutable source config."""

    config = _load_object(source_directory / "config.json", "Granite config")
    fields = {
        "layers": config.get("num_hidden_layers"),
        "kv_heads": config.get("num_key_value_heads"),
        "declared_sequence_length": config.get("max_position_embeddings"),
    }
    for name, value in fields.items():
        if isinstance(value, bool) or not isinstance(value, int) or value <= 0:
            raise ValueError(f"Granite config has no valid {name} declaration.")
    return {
        "parameter_family": "3B",
        **fields,
        "observed_runtime_capability": False,
    }


def _resolve_and_download(args: argparse.Namespace) -> None:
    """Resolve main once, download by immutable SHA, and retain source identity."""

    model_root = _normal_directory(args.model_root, "Model root")
    evidence_root = _normal_directory(args.evidence_root, "C1 evidence root")
    sources_root = model_root / "sources"
    if not sources_root.exists():
        sources_root.mkdir()
    sources_root = _normal_directory(sources_root, "Model source root")

    adapter = HuggingFaceHubAdapter()
    model: ResolvedModel = resolve_model(
        adapter,
        FORMAL_GRANITE_REPOSITORY,
        REQUESTED_REVISION,
    )
    source_directory = sources_root / (
        "granite41-3b-" + model.resolved_revision[:8]
    )
    downloaded = download_snapshot(adapter, model, source_directory)
    inventory = inventory_tree(source_directory)
    downloaded_paths = tuple(
        path.relative_to(source_directory).as_posix() for path in downloaded
    )
    if tuple(item.relative_path for item in inventory) != tuple(
        sorted(downloaded_paths, key=lambda value: (value.casefold(), value))
    ):
        raise ValueError("Downloaded snapshot and retained inventory disagree.")

    model_paths, tokenizer_paths = classify_source_paths(downloaded_paths)
    by_path = {item.relative_path: item for item in inventory}
    model_records = tuple(by_path[path] for path in model_paths)
    tokenizer_records = tuple(by_path[path] for path in tokenizer_paths)
    record = {
        "schema_version": "1.0",
        "campaign_id": CAMPAIGN_ID,
        "record_type": "resolved-model",
        "route_id": ROUTE_ID,
        "repository": model.repository,
        "requested_revision": model.requested_revision,
        "resolved_revision": model.resolved_revision,
        "license": "apache-2.0",
        "source_directory": str(source_directory.resolve(strict=True)),
        "declared_model_metadata": _declared_metadata(source_directory),
        "model_files": [item.as_record() for item in model_records],
        "tokenizer_files": [item.as_record() for item in tokenizer_records],
        "aggregate_model_sha256": _aggregate(source_directory, model_records),
        "aggregate_tokenizer_sha256": _aggregate(
            source_directory,
            tokenizer_records,
        ),
        "granite_model_test_authorised": False,
        "activation_claim_authorised": False,
        "packed_storage_claim_authorised": False,
        "performance_claim_authorised": False,
        "quality_claim_authorised": False,
    }
    _write_inventory(evidence_root / "source-files.csv", inventory)
    _write_atomic_json(evidence_root / "resolved-model.json", record)


def _disk_preflight(args: argparse.Namespace) -> None:
    """Record drive capacity and controlled-root inventory without deletion."""

    evidence_root = _normal_directory(args.evidence_root, "C1 evidence root")
    free_bytes = shutil.disk_usage(str(args.drive_root)).free
    record = collect_disk_preflight(
        args.model_root,
        args.probe_root,
        args.run_root,
        free_bytes,
    )
    # Disk capacity is necessary but never independently authorises acquisition.
    record.update(
        {
            "model_download_authorised": False,
            "granite_model_test_authorised": False,
            "activation_claim_authorised": False,
            "packed_storage_claim_authorised": False,
            "performance_claim_authorised": False,
            "quality_claim_authorised": False,
        }
    )
    if record.get("status") != "Passed":
        raise ValueError(
            "C1 disk preflight is blocked: "
            + "; ".join(str(value) for value in record.get("reasons", []))
        )
    _write_atomic_json(evidence_root / "disk-preflight.json", record)


def _normal_package_hashes(report: Mapping[str, Any]) -> dict[str, tuple[str, str]]:
    """Return canonical package version and exact archive digest from pip report."""

    result: dict[str, tuple[str, str]] = {}
    install = report.get("install")
    if not isinstance(install, list):
        raise ValueError("Normal pip report has no install list.")
    for row in install:
        if not isinstance(row, Mapping):
            raise ValueError("Normal pip report contains a non-object install row.")
        metadata = row.get("metadata")
        download = row.get("download_info")
        if not isinstance(metadata, Mapping) or not isinstance(download, Mapping):
            raise ValueError("Normal pip report row lacks metadata/download_info.")
        name = metadata.get("name")
        version = metadata.get("version")
        archive = download.get("archive_info")
        if not isinstance(name, str) or not isinstance(version, str):
            raise ValueError("Normal pip report row has invalid name/version.")
        if not isinstance(archive, Mapping):
            raise ValueError(f"Normal pip report has no archive identity for {name}.")
        hashes = archive.get("hashes")
        digest: object = hashes.get("sha256") if isinstance(hashes, Mapping) else None
        if not isinstance(digest, str) or not _SHA256_PATTERN.fullmatch(digest):
            raise ValueError(f"Normal pip report has no SHA-256 for {name}.")
        key = re.sub(r"[-_.]+", "-", name).casefold()
        if key in result:
            raise ValueError(f"Normal pip report repeats package {name}.")
        result[key] = (version, digest)
    return result


def _conversion_packages(dependency_evidence: Path) -> tuple[ConversionPackage, ...]:
    """Build the exact seven-package conversion identity from accepted evidence."""

    report = _load_object(
        dependency_evidence / "reports/normal-install-report.json",
        "Accepted normal install report",
    )
    normal = _normal_package_hashes(report)
    vcs = _load_object(
        dependency_evidence / "reports/vcs-packages.json",
        "Accepted VCS package report",
    )
    vcs_rows = vcs.get("packages")
    if not isinstance(vcs_rows, list):
        raise ValueError("Accepted VCS package report has no package list.")
    vcs_by_name: dict[str, Mapping[str, Any]] = {}
    for row in vcs_rows:
        if not isinstance(row, Mapping) or not isinstance(row.get("name"), str):
            raise ValueError("Accepted VCS package report contains an invalid row.")
        vcs_by_name[str(row["name"]).casefold()] = row

    packages: list[ConversionPackage] = []
    for name, expected_commit in (
        ("optimum", ACCEPTED_OPTIMUM_COMMIT),
        ("optimum-intel", ACCEPTED_OPTIMUM_INTEL_COMMIT),
    ):
        row = vcs_by_name.get(name)
        if row is None or row.get("commit") != expected_commit:
            raise ValueError(f"Accepted VCS identity differs for {name}.")
        version = row.get("version")
        if not isinstance(version, str):
            raise ValueError(f"Accepted VCS version is missing for {name}.")
        packages.append(
            ConversionPackage(
                name=name,
                version=version,
                source_identity=expected_commit,
            )
        )

    for name in (
        "transformers",
        "huggingface-hub",
        "nncf",
        "openvino",
        "openvino-tokenizers",
    ):
        row = normal.get(name)
        if row is None:
            raise ValueError(f"Accepted normal package is missing: {name}")
        version, digest = row
        packages.append(
            ConversionPackage(
                name=name,
                version=version,
                source_identity="sha256:" + digest,
            )
        )
    return tuple(packages)


def _build_records(args: argparse.Namespace) -> None:
    """Build schema-valid conversion and asset records after native conversion."""

    repository_root = _normal_directory(args.repository_root, "Repository root")
    evidence_root = _normal_directory(args.evidence_root, "C1 evidence root")
    source_directory = _normal_directory(args.source_directory, "Source directory")
    converted_directory = _normal_directory(
        args.converted_directory,
        "Converted directory",
    )
    dependency_evidence = _normal_directory(
        args.dependency_evidence,
        "Accepted dependency evidence",
    )

    resolved = _load_object(evidence_root / "resolved-model.json", "Resolved model")
    dependency_proof = _load_object(
        evidence_root / "dependency-acceptance-proof.json",
        "Dependency acceptance proof",
    )
    command = _load_object(
        evidence_root / "commands/conversion.json",
        "Conversion command record",
    )
    if dependency_proof.get("status") != "Passed":
        raise ValueError("Dependency acceptance proof is not Passed.")
    if dependency_proof.get("decision_sha256") != ACCEPTED_DEPENDENCY_DECISION_SHA256:
        raise ValueError("Dependency acceptance proof has the wrong decision digest.")

    converted_inventory = inventory_tree(converted_directory)
    converted_paths = require_converted_outputs(
        tuple(item.relative_path for item in converted_inventory)
    )
    by_path = {item.relative_path: item for item in converted_inventory}
    converted_records = tuple(by_path[path] for path in converted_paths)
    _write_inventory(evidence_root / "converted-files.csv", converted_records)

    request = ConversionRequest(
        optimum_cli=Path(str(dependency_proof["optimum_cli_path"])),
        source_directory=source_directory,
        output_directory=converted_directory,
    )
    conversion_record = collect_conversion_record(
        request=request,
        conversion_id=FORMAL_CONVERSION_ID,
        asset_id=FORMAL_ASSET_ID,
        source_repository=FORMAL_GRANITE_REPOSITORY,
        resolved_revision=str(resolved["resolved_revision"]),
        aggregate_model_sha256=str(resolved["aggregate_model_sha256"]),
        aggregate_tokenizer_sha256=str(
            resolved["aggregate_tokenizer_sha256"]
        ),
        dependency_preflight_status="Passed",
        dependency_preflight_record_path="dependency/decision.json",
        dependency_preflight_record_sha256=ACCEPTED_DEPENDENCY_DECISION_SHA256,
        python_version="3.12.10",
        executable_sha256=str(dependency_proof["optimum_cli_sha256"]),
        packages=_conversion_packages(dependency_evidence),
        started_at_utc=str(command["started_utc"]),
        completed_at_utc=str(command["ended_utc"]),
        exit_code=int(command["exit_code"]),
        stdout_path="logs/conversion.stdout.txt",
        stderr_path="logs/conversion.stderr.txt",
        output_files=tuple(
            ConversionFile(
                relative_path=item.relative_path,
                size_bytes=item.size_bytes,
                sha256=item.sha256,
            )
            for item in converted_records
        ),
        aggregate_output_sha256=_aggregate(
            converted_directory,
            converted_records,
        ),
        required_output_paths=REQUIRED_CONVERTED_FILES + ("tokenizer.json",),
        unrecorded_output_paths=(),
    )
    assert_phase3_record(
        "model-conversion-record",
        conversion_record,
        repository_root,
    )
    _write_atomic_json(
        evidence_root / "conversion-record.json",
        conversion_record,
    )

    source_files = resolved.get("model_files")
    tokenizer_files = resolved.get("tokenizer_files")
    if not isinstance(source_files, list) or not isinstance(tokenizer_files, list):
        raise ValueError("Resolved model record lacks source/tokenizer inventories.")
    asset_record = {
        "schema_version": "1.0",
        "campaign_id": CAMPAIGN_ID,
        "record_type": "model-asset-lock",
        "route_id": ROUTE_ID,
        "asset_id": FORMAL_ASSET_ID,
        "asset_role": "granite-3b",
        "source": {
            "repository": FORMAL_GRANITE_REPOSITORY,
            "requested_revision": REQUESTED_REVISION,
            "resolved_revision": resolved["resolved_revision"],
            "license": resolved["license"],
        },
        "declared_model_metadata": resolved["declared_model_metadata"],
        "source_directory": str(source_directory),
        "converted_directory": str(converted_directory),
        "source_files": source_files,
        "tokenizer_files": tokenizer_files,
        "conversion_record_path": "conversion-record.json",
        "converted_files": [item.as_record() for item in converted_records],
        "aggregate_model_sha256": resolved["aggregate_model_sha256"],
        "aggregate_tokenizer_sha256": resolved[
            "aggregate_tokenizer_sha256"
        ],
        "status": "Candidate",
        "reasons": [
            "The official Granite 4.1 3B snapshot is bound to an immutable Hub "
            "revision and the reviewed INT4 asymmetric group-128 conversion "
            "completed with complete file provenance. Model loading and all "
            "scientific claims remain later C3/C5 gates."
        ],
        "granite_model_test_authorised": False,
        "activation_claim_authorised": False,
        "packed_storage_claim_authorised": False,
        "performance_claim_authorised": False,
        "quality_claim_authorised": False,
    }
    assert_phase3_record("model-asset-lock", asset_record, repository_root)
    _write_atomic_json(evidence_root / "asset-lock.json", asset_record)


def _parse_args(argv: Sequence[str] | None) -> argparse.Namespace:
    """Build explicit subcommands for each live stage owned by Python."""

    parser = argparse.ArgumentParser(description=__doc__)
    subcommands = parser.add_subparsers(dest="command", required=True)

    disk = subcommands.add_parser("disk-preflight")
    disk.add_argument("--evidence-root", type=Path, required=True)
    disk.add_argument("--model-root", type=Path, required=True)
    disk.add_argument("--probe-root", type=Path, required=True)
    disk.add_argument("--run-root", type=Path, required=True)
    disk.add_argument("--drive-root", type=Path, required=True)

    resolve = subcommands.add_parser("resolve-download")
    resolve.add_argument("--model-root", type=Path, required=True)
    resolve.add_argument("--evidence-root", type=Path, required=True)

    records = subcommands.add_parser("build-records")
    records.add_argument("--repository-root", type=Path, required=True)
    records.add_argument("--evidence-root", type=Path, required=True)
    records.add_argument("--source-directory", type=Path, required=True)
    records.add_argument("--converted-directory", type=Path, required=True)
    records.add_argument("--dependency-evidence", type=Path, required=True)
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    """Run one explicit live C1 data operation and fail closed on any drift."""

    args = _parse_args(argv)
    try:
        if args.command == "disk-preflight":
            _disk_preflight(args)
        elif args.command == "resolve-download":
            _resolve_and_download(args)
        elif args.command == "build-records":
            _build_records(args)
        else:  # argparse prevents this; retain a defensive branch.
            raise ValueError(f"Unsupported live C1 command: {args.command}")
    except (OSError, ValueError) as error:
        print(f"Error: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
