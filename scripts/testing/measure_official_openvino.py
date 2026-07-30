"""Run one fail-closed, fully instrumented OpenVINO WB-04 measurement."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import sys
from collections.abc import Callable, Mapping
from pathlib import Path
from typing import Any

ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from scripts.testing.official_openvino.conversion import (
    validate_artifact_manifest,
)
from scripts.testing.official_openvino.metrics import summarize_samples
from scripts.testing.official_openvino.runtime_measurement import (
    atomic_write_json,
)
from scripts.testing.official_openvino.runtime_process import (
    measurement_sample,
    run_governed_process,
)


MIB = 1024**2
SPEC_SCHEMA = "official-openvino-wb04-worker-spec/v1"
ROLES = frozenset({"pilot", "warmup", "sample-1", "sample-2", "sample-3"})
SEQUENCE_ROLES = ("pilot", "warmup", "sample-1", "sample-2", "sample-3")
CAMPAIGN_SCHEMA = "official-openvino-wb04-campaign-identity/v1"
RECEIPT_SCHEMA = "official-openvino-wb04-sequence-receipt/v1"
SEQUENCE_SCHEMA = "official-openvino-wb04-attempt-sequence/v1"
_ATTEMPT_DIRECTORY = re.compile(r"^attempt-(\d{3})$")


def _directory(path: Path, field: str) -> Path:
    resolved = Path(path).resolve()
    if not resolved.is_dir():
        raise ValueError(f"{field} directory is missing: {resolved}")
    return resolved


def _file(path: Path, field: str) -> Path:
    resolved = Path(path).resolve()
    if not resolved.is_file():
        raise ValueError(f"{field} file is missing: {resolved}")
    return resolved


def _sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with Path(path).open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _sha256_json(value: Any) -> str:
    encoded = json.dumps(
        value,
        sort_keys=True,
        separators=(",", ":"),
        ensure_ascii=True,
        allow_nan=False,
    ).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def _read_json_object(path: Path, field: str) -> dict[str, Any]:
    source = _file(path, field)
    try:
        value = json.loads(source.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError) as error:
        raise ValueError(f"{field} is unreadable: {source}") from error
    if not isinstance(value, dict):
        raise ValueError(f"{field} must contain a JSON object")
    return value


def _directory_content_identity(path: Path, field: str) -> dict[str, Any]:
    root = _directory(path, field)
    files: list[dict[str, Any]] = []
    for source in sorted(
        (
            candidate
            for candidate in root.rglob("*")
            if candidate.is_file()
            and "__pycache__" not in candidate.relative_to(root).parts
            and candidate.suffix.lower() not in {".pyc", ".pyo"}
        ),
        key=lambda candidate: candidate.relative_to(root).as_posix(),
    ):
        files.append(
            {
                "path": source.relative_to(root).as_posix(),
                "bytes": source.stat().st_size,
                "sha256": _sha256_file(source),
            }
        )
    return {
        "path": str(root),
        "files": files,
        "content_sha256": _sha256_json(files),
    }


def _sequence_spec(path: Path) -> dict[str, Any]:
    value = _read_json_object(path, "worker spec")
    if value.get("schema") != SPEC_SCHEMA:
        raise ValueError("worker spec schema is invalid")
    if value.get("role") not in ROLES:
        raise ValueError("worker spec role is invalid")
    for field in ("controlled_test_id", "model_path", "device", "prompt"):
        if not isinstance(value.get(field), str) or not value[field].strip():
            raise ValueError(f"worker spec {field} is required")
    if not isinstance(value.get("context"), int) or isinstance(
        value.get("context"), bool
    ):
        raise ValueError("worker spec context must be an integer")
    if value.get("expected_input_tokens") != value["context"]:
        raise ValueError(
            "worker expected_input_tokens must equal the controlled context"
        )
    if not isinstance(value.get("properties"), dict):
        raise ValueError("worker spec properties must be an object")
    return value


def _matrix_case(path: Path, test_id: str, context: int) -> dict[str, Any]:
    matrix_path = _file(path, "retest matrix")
    matrix = _read_json_object(matrix_path, "retest matrix")
    cases = matrix.get("cases")
    if not isinstance(cases, list):
        raise ValueError("retest matrix cases must be an array")
    matches = [
        row
        for row in cases
        if isinstance(row, dict) and row.get("test_id") == test_id
    ]
    if len(matches) != 1:
        raise ValueError(
            f"retest matrix must contain exactly one case for {test_id}"
        )
    case = dict(matches[0])
    contexts = case.get("contexts")
    if not isinstance(contexts, list) or context not in contexts:
        raise ValueError(
            f"worker context {context} is not declared by matrix case {test_id}"
        )
    return {
        "path": str(matrix_path),
        "file_sha256": _sha256_file(matrix_path),
        "schema_version": matrix.get("schema_version"),
        "case": case,
    }


def build_campaign_identity(
    *,
    spec_path: Path,
    matrix_path: Path,
    artifact_manifest_path: Path,
    build_provenance_path: Path,
    build_root: Path,
    repo_root: Path,
    python_executable: Path,
    python_site_packages: Path,
    openvino_libraries: Path,
) -> dict[str, Any]:
    """Derive one canonical identity from every resumability boundary."""

    spec = _sequence_spec(spec_path)
    model_path = _directory(Path(spec["model_path"]), "model")
    matrix = _matrix_case(
        matrix_path,
        spec["controlled_test_id"],
        spec["context"],
    )
    expected_precision = matrix["case"].get("weight_precision")
    if expected_precision not in {"f16", "u8", "u4"}:
        raise ValueError("matrix case has no recognized weight precision")
    manifest_path = _file(
        artifact_manifest_path,
        "model artifact manifest",
    )
    artifact = validate_artifact_manifest(
        manifest_path,
        expected_precision=expected_precision,
    )
    if os.path.normcase(str(model_path)) != os.path.normcase(
        str(Path(artifact["artifact_root"]).resolve())
    ):
        raise ValueError(
            "worker model path does not match the validated artifact manifest"
        )
    build = _directory(build_root, "build root")
    package = _directory(build / "openvino_genai", "OpenVINO GenAI package")
    modules = sorted(package.glob("py_openvino_genai*.pyd"))
    if len(modules) != 1:
        raise ValueError(
            "build must contain exactly one py_openvino_genai module"
        )
    runtime_dll = _file(
        package / "openvino_genai.dll",
        "OpenVINO GenAI runtime",
    )
    provenance_path = _file(build_provenance_path, "build provenance")
    provenance = _read_json_object(provenance_path, "build provenance")
    if provenance.get("status") != "passed":
        raise ValueError("build provenance does not report passed status")
    python = _file(python_executable, "Python executable")
    repository = _directory(repo_root, "repository root")
    site_packages = _directory(python_site_packages, "Python site-packages")
    libraries = _directory(openvino_libraries, "OpenVINO libraries")
    openvino_package = site_packages / "openvino"
    runtime_source_root = (
        repository / "scripts" / "testing" / "official_openvino"
    )
    controller_source = (
        repository / "scripts" / "testing" / "measure_official_openvino.py"
    )

    config_fields = (
        "device",
        "max_new_tokens",
        "expected_input_tokens",
        "ignore_eos",
        "seed",
        "apply_chat_template",
        "properties",
    )
    identity = {
        "config": {field: spec.get(field) for field in config_fields},
        "context": spec["context"],
        "matrix": matrix,
        "build": {
            "root": str(build),
            "provenance_path": str(provenance_path),
            "provenance_sha256": _sha256_file(provenance_path),
            "python_module": {
                "path": str(modules[0].resolve()),
                "sha256": _sha256_file(modules[0]),
            },
            "runtime_dll": {
                "path": str(runtime_dll),
                "sha256": _sha256_file(runtime_dll),
            },
        },
        "model": {
            "artifact_manifest_path": str(manifest_path),
            "artifact_manifest_sha256": _sha256_file(manifest_path),
            "validated_artifact": artifact,
        },
        "prompt": {
            "utf8_bytes": len(spec["prompt"].encode("utf-8")),
            "sha256": hashlib.sha256(
                spec["prompt"].encode("utf-8")
            ).hexdigest(),
        },
        "runtime": {
            "repository_root": str(repository),
            "repository_runtime_sources": (
                _directory_content_identity(
                    runtime_source_root,
                    "measurement runtime source",
                )
                if runtime_source_root.is_dir()
                else {
                    "path": str(runtime_source_root.resolve()),
                    "files": [],
                    "content_sha256": _sha256_json([]),
                }
            ),
            "controller_source": (
                {
                    "path": str(controller_source.resolve()),
                    "sha256": _sha256_file(controller_source),
                }
                if controller_source.is_file()
                else None
            ),
            "python_executable": {
                "path": str(python),
                "sha256": _sha256_file(python),
            },
            "python_openvino_package": (
                _directory_content_identity(
                    openvino_package,
                    "Python OpenVINO package",
                )
                if openvino_package.is_dir()
                else {
                    "path": str(openvino_package.resolve()),
                    "files": [],
                    "content_sha256": _sha256_json([]),
                }
            ),
            "openvino_libraries": _directory_content_identity(
                libraries,
                "OpenVINO libraries",
            ),
        },
    }
    return {
        "schema": CAMPAIGN_SCHEMA,
        "identity": identity,
        "campaign_identity_sha256": _sha256_json(identity),
    }


class CampaignLock:
    """Hold an operating-system lock for one campaign controller."""

    def __init__(self, campaign_root: Path):
        self._root = Path(campaign_root).resolve()
        self._handle: Any | None = None

    def __enter__(self) -> "CampaignLock":
        self._root.mkdir(parents=True, exist_ok=True)
        handle = (self._root / ".campaign.lock").open("a+b")
        try:
            if handle.seek(0, os.SEEK_END) == 0:
                handle.write(b"\0")
                handle.flush()
                os.fsync(handle.fileno())
            handle.seek(0)
            if os.name == "nt":
                import msvcrt

                msvcrt.locking(handle.fileno(), msvcrt.LK_NBLCK, 1)
            else:
                import fcntl

                fcntl.flock(
                    handle.fileno(),
                    fcntl.LOCK_EX | fcntl.LOCK_NB,
                )
        except OSError as error:
            handle.close()
            raise RuntimeError(
                f"measurement campaign is locked: {self._root}"
            ) from error
        self._handle = handle
        return self

    def __exit__(self, *_: Any) -> None:
        handle = self._handle
        self._handle = None
        if handle is None:
            return
        try:
            handle.seek(0)
            if os.name == "nt":
                import msvcrt

                msvcrt.locking(handle.fileno(), msvcrt.LK_UNLCK, 1)
            else:
                import fcntl

                fcntl.flock(handle.fileno(), fcntl.LOCK_UN)
        finally:
            handle.close()


def build_worker_environment(
    *,
    build_root: Path,
    repo_root: Path,
    python_site_packages: Path,
    openvino_libraries: Path,
    base_environment: Mapping[str, str] | None = None,
) -> dict[str, str]:
    """Bind imports and DLL lookup to one verified build."""

    build = _directory(build_root, "build root")
    repository = _directory(repo_root, "repository root")
    site_packages = _directory(python_site_packages, "Python site-packages")
    libraries = _directory(openvino_libraries, "OpenVINO libraries")
    package = _directory(build / "openvino_genai", "OpenVINO GenAI package")
    _file(package / "__init__.py", "OpenVINO GenAI package initializer")
    modules = sorted(package.glob("py_openvino_genai*.pyd"))
    if len(modules) != 1 or modules[0].stat().st_size <= 0:
        raise ValueError(
            "build must contain exactly one non-empty py_openvino_genai module"
        )
    _file(package / "openvino_genai.dll", "OpenVINO GenAI runtime")

    environment = dict(
        os.environ if base_environment is None else base_environment
    )
    existing_path = environment.get("PATH", "")
    path_entries = [str(package), str(libraries)]
    if existing_path:
        path_entries.append(existing_path)
    environment.update(
        {
            "PATH": os.pathsep.join(path_entries),
            "PYTHONPATH": os.pathsep.join(
                (str(build), str(repository), str(site_packages))
            ),
            "PYTHONNOUSERSITE": "1",
            "PYTHONUTF8": "1",
            "PYTHONHASHSEED": "0",
        }
    )
    return environment


def _load_spec(path: Path, role: str) -> Path:
    if role not in ROLES:
        raise ValueError(f"unsupported measurement role: {role}")
    spec_path = _file(path, "worker spec")
    try:
        value = json.loads(spec_path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError) as error:
        raise ValueError(f"worker spec is unreadable: {spec_path}") from error
    if not isinstance(value, dict) or value.get("schema") != SPEC_SCHEMA:
        raise ValueError("worker spec schema is invalid")
    if value.get("role") != role:
        raise ValueError("worker spec role does not match requested role")
    return spec_path


def run_single_measurement(
    *,
    spec_path: Path,
    output_dir: Path,
    role: str,
    build_root: Path,
    repo_root: Path,
    python_executable: Path,
    python_site_packages: Path,
    openvino_libraries: Path,
    sampler_script: Path,
    timeout_seconds: float = 900.0,
    minimum_available_ram_mib: int = 2048,
    run_process: Callable[..., dict[str, Any]] = run_governed_process,
) -> dict[str, Any]:
    """Execute one role in a fresh process and return its persisted record."""

    spec = _load_spec(spec_path, role)
    python = _file(python_executable, "Python executable")
    sampler = _file(sampler_script, "utilization sampler")
    output = Path(output_dir).resolve()
    if output.exists():
        raise ValueError(f"output directory already exists: {output}")
    if timeout_seconds <= 0:
        raise ValueError("timeout seconds must be positive")
    if (
        isinstance(minimum_available_ram_mib, bool)
        or not isinstance(minimum_available_ram_mib, int)
        or minimum_available_ram_mib < 2048
    ):
        raise ValueError("minimum available RAM must be at least 2048 MiB")

    environment = build_worker_environment(
        build_root=build_root,
        repo_root=repo_root,
        python_site_packages=python_site_packages,
        openvino_libraries=openvino_libraries,
    )
    command = [
        str(python),
        "-m",
        "scripts.testing.official_openvino.measurement_worker",
        "--spec",
        str(spec),
    ]
    return run_process(
        command=command,
        output_dir=output,
        role=role,
        environment=environment,
        sampler_script=sampler,
        timeout_seconds=float(timeout_seconds),
        minimum_available_ram_bytes=minimum_available_ram_mib * MIB,
        sample_interval_seconds=0.25,
    )


def _role_spec(
    template: Mapping[str, Any],
    role: str,
    campaign_identity_sha256: str,
) -> dict[str, Any]:
    value = dict(template)
    value["role"] = role
    value["campaign_identity_sha256"] = campaign_identity_sha256
    return value


def _attempt_directories(role_root: Path) -> list[tuple[int, Path]]:
    if not role_root.exists():
        return []
    attempts: list[tuple[int, Path]] = []
    for candidate in role_root.iterdir():
        match = _ATTEMPT_DIRECTORY.fullmatch(candidate.name)
        if match is not None and candidate.is_dir():
            attempts.append((int(match.group(1)), candidate))
    return sorted(attempts)


def _persisted_record(path: Path) -> dict[str, Any] | None:
    if not path.is_file():
        return None
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return None
    return value if isinstance(value, dict) else None


def _runtime_record_matches_spec(
    record: Mapping[str, Any],
    spec: Mapping[str, Any],
    role: str,
) -> bool:
    worker = record.get("worker")
    if not isinstance(worker, Mapping):
        return False
    expected = {
        "role": role,
        "controlled_test_id": spec["controlled_test_id"],
        "context": spec["context"],
        "expected_input_tokens": spec["expected_input_tokens"],
        "device": spec["device"],
    }
    if any(worker.get(field) != value for field, value in expected.items()):
        return False
    model_path = worker.get("model_path")
    return (
        isinstance(model_path, str)
        and Path(model_path).resolve() == Path(spec["model_path"]).resolve()
    )


def _resumable_attempt(
    *,
    campaign_root: Path,
    role: str,
    role_spec: Mapping[str, Any],
    identity_sha256: str,
) -> tuple[dict[str, Any], dict[str, Any], Path] | None:
    expected_spec_sha256 = _sha256_json(role_spec)
    role_root = campaign_root / "attempts" / role
    accepted: list[tuple[dict[str, Any], dict[str, Any], Path]] = []
    for _, attempt_dir in _attempt_directories(role_root):
        receipt_path = attempt_dir / "sequence-receipt.json"
        receipt = _persisted_record(receipt_path)
        if receipt is None or receipt.get("accepted") is not True:
            continue
        if (
            receipt.get("schema") != RECEIPT_SCHEMA
            or receipt.get("role") != role
            or receipt.get("campaign_identity_sha256") != identity_sha256
            or receipt.get("spec_sha256") != expected_spec_sha256
        ):
            raise RuntimeError(
                f"{role} accepted receipt does not match campaign identity"
            )
        spec_path = attempt_dir / "spec.json"
        record_path = attempt_dir / "run" / "attempt.json"
        if (
            not spec_path.is_file()
            or _sha256_file(spec_path) != receipt.get("spec_file_sha256")
            or not record_path.is_file()
            or _sha256_file(record_path)
            != receipt.get("runtime_record_sha256")
        ):
            raise RuntimeError(f"{role} accepted attempt evidence was modified")
        persisted_spec = _persisted_record(spec_path)
        record = _persisted_record(record_path)
        if (
            persisted_spec != dict(role_spec)
            or record is None
            or record.get("role") != role
            or record.get("valid") is not True
            or record.get("cleanup_process_count") != 0
            or not _runtime_record_matches_spec(record, role_spec, role)
        ):
            raise RuntimeError(f"{role} accepted attempt is not resumable")
        accepted.append((receipt, record, record_path))
    if len(accepted) > 1:
        raise RuntimeError(f"{role} has multiple accepted attempts")
    return accepted[0] if accepted else None


def _next_attempt_directory(campaign_root: Path, role: str) -> tuple[int, Path]:
    role_root = campaign_root / "attempts" / role
    role_root.mkdir(parents=True, exist_ok=True)
    attempts = _attempt_directories(role_root)
    number = attempts[-1][0] + 1 if attempts else 1
    if number > 999:
        raise RuntimeError(f"{role} exhausted immutable attempt numbering")
    target = role_root / f"attempt-{number:03d}"
    target.mkdir()
    return number, target


def _write_sequence_receipt(
    *,
    campaign_root: Path,
    attempt_dir: Path,
    role: str,
    attempt_number: int,
    identity_sha256: str,
    spec_value: Mapping[str, Any],
    record_path: Path | None,
    accepted: bool,
    controller_error: str | None = None,
) -> dict[str, Any]:
    spec_path = attempt_dir / "spec.json"
    receipt = {
        "schema": RECEIPT_SCHEMA,
        "role": role,
        "attempt_number": attempt_number,
        "campaign_identity_sha256": identity_sha256,
        "spec_sha256": _sha256_json(spec_value),
        "spec_path": spec_path.relative_to(campaign_root).as_posix(),
        "spec_file_sha256": _sha256_file(spec_path),
        "runtime_record_path": (
            record_path.relative_to(campaign_root).as_posix()
            if record_path is not None and record_path.is_file()
            else None
        ),
        "runtime_record_sha256": (
            _sha256_file(record_path)
            if record_path is not None and record_path.is_file()
            else None
        ),
        "accepted": accepted,
        "controller_error": controller_error,
    }
    atomic_write_json(attempt_dir / "sequence-receipt.json", receipt)
    return receipt


def _campaign_identity(
    campaign_root: Path,
    identity: Mapping[str, Any],
) -> None:
    destination = campaign_root / "campaign-identity.json"
    existing = _persisted_record(destination)
    if existing is not None:
        if existing != dict(identity):
            raise RuntimeError(
                "campaign root belongs to a different canonical identity"
            )
        return
    if destination.exists():
        raise RuntimeError("campaign identity record is unreadable")
    atomic_write_json(destination, identity)


def _run_measurement_sequence_locked(
    *,
    spec_path: Path,
    campaign_root: Path,
    matrix_path: Path,
    artifact_manifest_path: Path,
    build_provenance_path: Path,
    build_root: Path,
    repo_root: Path,
    python_executable: Path,
    python_site_packages: Path,
    openvino_libraries: Path,
    sampler_script: Path,
    timeout_seconds: float = 900.0,
    minimum_available_ram_mib: int = 2048,
    run_measurement: Callable[..., dict[str, Any]] = run_single_measurement,
) -> dict[str, Any]:
    """Run or resume a complete five-role measurement campaign."""

    root = Path(campaign_root).resolve()
    root.mkdir(parents=True, exist_ok=True)
    template = _sequence_spec(spec_path)
    identity = build_campaign_identity(
        spec_path=spec_path,
        matrix_path=matrix_path,
        artifact_manifest_path=artifact_manifest_path,
        build_provenance_path=build_provenance_path,
        build_root=build_root,
        repo_root=repo_root,
        python_executable=python_executable,
        python_site_packages=python_site_packages,
        openvino_libraries=openvino_libraries,
    )
    identity_sha256 = identity["campaign_identity_sha256"]
    _campaign_identity(root, identity)

    completed: list[
        tuple[dict[str, Any], dict[str, Any], Path]
    ] = []
    for role in SEQUENCE_ROLES:
        spec_value = _role_spec(template, role, identity_sha256)
        resumed = _resumable_attempt(
            campaign_root=root,
            role=role,
            role_spec=spec_value,
            identity_sha256=identity_sha256,
        )
        if resumed is not None:
            completed.append(resumed)
            continue

        attempt_number, attempt_dir = _next_attempt_directory(root, role)
        attempt_spec = attempt_dir / "spec.json"
        atomic_write_json(attempt_spec, spec_value)
        output_dir = attempt_dir / "run"
        try:
            returned = dict(
                run_measurement(
                    spec_path=attempt_spec,
                    output_dir=output_dir,
                    role=role,
                    build_root=build_root,
                    repo_root=repo_root,
                    python_executable=python_executable,
                    python_site_packages=python_site_packages,
                    openvino_libraries=openvino_libraries,
                    sampler_script=sampler_script,
                    timeout_seconds=timeout_seconds,
                    minimum_available_ram_mib=minimum_available_ram_mib,
                )
            )
        except Exception as error:
            record_path = output_dir / "attempt.json"
            _write_sequence_receipt(
                campaign_root=root,
                attempt_dir=attempt_dir,
                role=role,
                attempt_number=attempt_number,
                identity_sha256=identity_sha256,
                spec_value=spec_value,
                record_path=record_path if record_path.is_file() else None,
                accepted=False,
                controller_error=f"{type(error).__name__}: {error}",
            )
            raise

        record_path = output_dir / "attempt.json"
        persisted = _persisted_record(record_path)
        accepted = (
            persisted is not None
            and persisted == returned
            and persisted.get("role") == role
            and persisted.get("valid") is True
            and persisted.get("cleanup_process_count") == 0
            and _runtime_record_matches_spec(persisted, spec_value, role)
        )
        receipt = _write_sequence_receipt(
            campaign_root=root,
            attempt_dir=attempt_dir,
            role=role,
            attempt_number=attempt_number,
            identity_sha256=identity_sha256,
            spec_value=spec_value,
            record_path=record_path if record_path.is_file() else None,
            accepted=accepted,
            controller_error=(
                None
                if accepted
                else "runtime record is absent, differs from the returned "
                "record, or did not pass validation"
            ),
        )
        if not accepted or persisted is None:
            raise RuntimeError(f"{role} attempt did not pass validation")
        completed.append((receipt, persisted, record_path))

    formal_samples = [
        measurement_sample(record, source)
        for _, record, source in completed[2:]
    ]
    metrics = summarize_samples(formal_samples)
    metrics["campaign_identity_sha256"] = identity_sha256
    metrics_path = root / "measurement-summary.json"
    atomic_write_json(metrics_path, metrics)
    receipts = [receipt for receipt, _, _ in completed]
    sequence = {
        "schema": SEQUENCE_SCHEMA,
        "campaign_identity_sha256": identity_sha256,
        "pilot_passed": True,
        "warmup_excluded": True,
        "pilot": receipts[0],
        "warmup": receipts[1],
        "accepted_samples": receipts[2:],
        "accepted_sample_count": 3,
        "cleanup_process_count": 0,
        "measurement_summary_path": metrics_path.relative_to(root).as_posix(),
        "measurement_summary_sha256": _sha256_file(metrics_path),
    }
    atomic_write_json(root / "attempt-sequence.json", sequence)
    return sequence


def run_measurement_sequence(
    *,
    spec_path: Path,
    campaign_root: Path,
    matrix_path: Path,
    artifact_manifest_path: Path,
    build_provenance_path: Path,
    build_root: Path,
    repo_root: Path,
    python_executable: Path,
    python_site_packages: Path,
    openvino_libraries: Path,
    sampler_script: Path,
    timeout_seconds: float = 900.0,
    minimum_available_ram_mib: int = 2048,
    run_measurement: Callable[..., dict[str, Any]] = run_single_measurement,
) -> dict[str, Any]:
    """Run or resume a locked complete five-role measurement campaign."""

    with CampaignLock(campaign_root):
        return _run_measurement_sequence_locked(
            spec_path=spec_path,
            campaign_root=campaign_root,
            matrix_path=matrix_path,
            artifact_manifest_path=artifact_manifest_path,
            build_provenance_path=build_provenance_path,
            build_root=build_root,
            repo_root=repo_root,
            python_executable=python_executable,
            python_site_packages=python_site_packages,
            openvino_libraries=openvino_libraries,
            sampler_script=sampler_script,
            timeout_seconds=timeout_seconds,
            minimum_available_ram_mib=minimum_available_ram_mib,
            run_measurement=run_measurement,
        )


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser()
    parser.add_argument("--spec", type=Path, required=True)
    destination = parser.add_mutually_exclusive_group(required=True)
    destination.add_argument("--output-dir", type=Path)
    destination.add_argument("--campaign-root", type=Path)
    parser.add_argument("--role", choices=sorted(ROLES))
    parser.add_argument("--matrix", dest="matrix_path", type=Path)
    parser.add_argument(
        "--artifact-manifest",
        dest="artifact_manifest_path",
        type=Path,
    )
    parser.add_argument(
        "--build-provenance",
        dest="build_provenance_path",
        type=Path,
    )
    parser.add_argument("--build-root", type=Path, required=True)
    parser.add_argument("--repo-root", type=Path, required=True)
    parser.add_argument("--python-executable", type=Path, required=True)
    parser.add_argument("--python-site-packages", type=Path, required=True)
    parser.add_argument("--openvino-libraries", type=Path, required=True)
    parser.add_argument("--sampler-script", type=Path, required=True)
    parser.add_argument("--timeout-seconds", type=float, default=900.0)
    parser.add_argument("--minimum-available-ram-mib", type=int, default=2048)
    return parser


def main(argv: list[str] | None = None) -> int:
    parser = _parser()
    args = parser.parse_args(argv)
    common = {
        "spec_path": args.spec,
        "build_root": args.build_root,
        "repo_root": args.repo_root,
        "python_executable": args.python_executable,
        "python_site_packages": args.python_site_packages,
        "openvino_libraries": args.openvino_libraries,
        "sampler_script": args.sampler_script,
        "timeout_seconds": args.timeout_seconds,
        "minimum_available_ram_mib": args.minimum_available_ram_mib,
    }
    if args.output_dir is not None:
        if args.role is None:
            parser.error("--role is required with --output-dir")
        record = run_single_measurement(
            **common,
            output_dir=args.output_dir,
            role=args.role,
        )
        print(json.dumps(record, sort_keys=True, allow_nan=False))
        return 0 if record.get("valid") is True else 1

    if args.role is not None:
        parser.error("--role cannot be used with --campaign-root")
    if (
        args.matrix_path is None
        or args.artifact_manifest_path is None
        or args.build_provenance_path is None
    ):
        parser.error(
            "--matrix, --artifact-manifest, and --build-provenance are "
            "required with --campaign-root"
        )
    sequence = run_measurement_sequence(
        **common,
        campaign_root=args.campaign_root,
        matrix_path=args.matrix_path,
        artifact_manifest_path=args.artifact_manifest_path,
        build_provenance_path=args.build_provenance_path,
    )
    print(json.dumps(sequence, sort_keys=True, allow_nan=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
