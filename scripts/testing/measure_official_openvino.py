"""Run one fail-closed, fully instrumented OpenVINO WB-04 measurement."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import re
import sys
import time
from collections.abc import Callable, Mapping
from dataclasses import dataclass
from pathlib import Path
from typing import Any

ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from scripts.testing.official_openvino.conversion import (
    validate_artifact_manifest,
)
from scripts.testing.official_openvino.metrics import summarize_samples
from scripts.testing.official_openvino.owned_process_guard import KillOnCloseJob
from scripts.testing.official_openvino.adaptive_metrics import (
    build_adaptive_runtime_sample,
    summarize_adaptive_runtime_samples,
)
from scripts.testing.official_openvino.matrix import (
    load_matrix,
    load_matrix_metadata,
)
from scripts.testing.official_openvino.runtime_measurement import (
    atomic_write_json,
)
from scripts.testing.official_openvino.runtime_process import (
    measurement_sample,
    run_governed_process,
)
from scripts.testing.official_openvino.workload import build_context_workload


MIB = 1024**2
MIN_LAUNCH_AVAILABLE_RAM_MIB = 4096
MIN_EMERGENCY_AVAILABLE_RAM_MIB = 2048
SPEC_SCHEMA = "official-openvino-wb04-worker-spec/v1"
ADAPTIVE_SPEC_SCHEMA = "official-openvino-adaptive-comparison-runtime-spec/v1"
ROLES = frozenset({"pilot", "warmup", "sample-1", "sample-2", "sample-3"})
SEQUENCE_ROLES = ("pilot", "warmup", "sample-1", "sample-2", "sample-3")
CAMPAIGN_SCHEMA = "official-openvino-wb04-campaign-identity/v1"
RECEIPT_SCHEMA = "official-openvino-wb04-sequence-receipt/v1"
SEQUENCE_SCHEMA = "official-openvino-wb04-attempt-sequence/v1"
_ATTEMPT_DIRECTORY = re.compile(r"^attempt-(\d{3})$")
_ACCEPTED_RECEIPT_FIELDS = frozenset(
    {
        "schema",
        "role",
        "attempt_number",
        "campaign_identity_sha256",
        "spec_sha256",
        "spec_path",
        "spec_file_sha256",
        "runtime_record_path",
        "runtime_record_sha256",
        "accepted",
    }
)
_FROZEN_EXECUTION_FIELDS = (
    "key_cache_precision",
    "value_cache_precision",
    "requested_device",
    "runtime_key_algorithm",
    "runtime_value_algorithm",
    "norm_correction",
    "attention_path",
    "execution_route",
    "expected_outcome",
    "suitable_host_required",
    "numeric_generation_metrics_expected",
)


@dataclass(frozen=True)
class MeasurementFailureRecord:
    role: str
    record_path: Path | None
    record: Mapping[str, Any] | None
    fingerprint: str


class MeasurementSequenceFailure(RuntimeError):
    def __init__(self, message: str, failure: MeasurementFailureRecord):
        super().__init__(message)
        self.failure = failure


def _validate_ram_thresholds(
    launch_minimum_available_ram_mib: int,
    emergency_minimum_available_ram_mib: int,
) -> None:
    if (
        isinstance(launch_minimum_available_ram_mib, bool)
        or not isinstance(launch_minimum_available_ram_mib, int)
        or launch_minimum_available_ram_mib < MIN_LAUNCH_AVAILABLE_RAM_MIB
    ):
        raise ValueError("launch available RAM must be at least 4096 MiB")
    if (
        isinstance(emergency_minimum_available_ram_mib, bool)
        or not isinstance(emergency_minimum_available_ram_mib, int)
        or emergency_minimum_available_ram_mib
        < MIN_EMERGENCY_AVAILABLE_RAM_MIB
    ):
        raise ValueError("emergency available RAM must be at least 2048 MiB")


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


def _project_adaptive_sequence_spec(value: Mapping[str, Any]) -> dict[str, Any]:
    expected_fields = {
        "schema",
        "controlled_test_id",
        "artifact_id",
        "artifact_manifest_path",
        "artifact_manifest_sha256",
        "model_path",
        "device",
        "context_tokens",
        "workload",
        "properties",
        "max_new_tokens",
        "ignore_eos",
        "seed",
        "apply_chat_template",
    }
    if set(value) != expected_fields:
        raise ValueError("adaptive worker spec fields are invalid")
    context = value.get("context_tokens")
    try:
        expected_workload = {
            **build_context_workload(context),
            "actual_input_tokens": context,
        }
    except ValueError as error:
        raise ValueError("adaptive worker spec context is invalid") from error
    if value.get("workload") != expected_workload:
        raise ValueError("adaptive worker spec workload is invalid")
    for field in (
        "controlled_test_id",
        "artifact_id",
        "artifact_manifest_path",
        "artifact_manifest_sha256",
        "model_path",
        "device",
    ):
        if not isinstance(value.get(field), str) or not value[field].strip():
            raise ValueError(f"adaptive worker spec {field} is required")
    if not re.fullmatch(r"[0-9a-f]{64}", value["artifact_manifest_sha256"]):
        raise ValueError("adaptive worker spec artifact manifest hash is invalid")
    if not isinstance(value.get("properties"), dict):
        raise ValueError("adaptive worker spec properties must be an object")
    if (
        type(value.get("max_new_tokens")) is not int
        or value["max_new_tokens"] != 4
        or value.get("ignore_eos") is not True
        or type(value.get("seed")) is not int
        or value["seed"] != 42
        or value.get("apply_chat_template") is not False
    ):
        raise ValueError("adaptive worker spec generation controls are invalid")
    return {
        "schema": SPEC_SCHEMA,
        "role": "pilot",
        "controlled_test_id": value["controlled_test_id"],
        "model_path": value["model_path"],
        "device": value["device"],
        "prompt": expected_workload["prompt"],
        "context": context,
        "expected_input_tokens": context,
        "properties": dict(value["properties"]),
        "max_new_tokens": value["max_new_tokens"],
        "ignore_eos": value["ignore_eos"],
        "seed": value["seed"],
        "apply_chat_template": value["apply_chat_template"],
    }


def _sequence_spec(path: Path) -> dict[str, Any]:
    value = _read_json_object(path, "worker spec")
    if value.get("schema") == ADAPTIVE_SPEC_SCHEMA:
        value = _project_adaptive_sequence_spec(value)
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
    identities = load_matrix_metadata(matrix_path)
    load_matrix(matrix_path)
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
        "source_identity": identities["source_identity"],
        "build_identity": identities["build_identity"],
        "case": case,
    }


def _frozen_case_field(case: Mapping[str, Any], field: str) -> Any:
    if field not in case:
        raise ValueError(f"matrix frozen {field} is missing")
    return case[field]


def _frozen_execution_contract(case: Mapping[str, Any]) -> dict[str, Any]:
    """Read every frozen execution field before considering a worker launch."""

    contract = {
        field: _frozen_case_field(case, field)
        for field in _FROZEN_EXECUTION_FIELDS
    }
    route = contract["execution_route"]
    outcome = contract["expected_outcome"]
    if route == "non-runtime":
        expected_attention = "not-applicable-non-runtime"
    elif outcome == "expected-rejection":
        expected_attention = "not-produced-by-expected-rejection"
    elif route == "patched-stateful":
        expected_attention = "stateful_sdpa_reference_codec"
    elif route in {"upstream-scalar", "stateful-standard", "device-standard"}:
        expected_attention = "stateful_sdpa_standard"
    else:
        expected_attention = None
    if contract["attention_path"] != expected_attention:
        raise ValueError(
            "matrix attention path does not match the frozen route/outcome"
        )
    if type(contract["suitable_host_required"]) is not bool:
        raise ValueError("matrix suitable host requirement is invalid")
    return contract


def _device_family(value: Any, *, field: str) -> str:
    if not isinstance(value, str) or not value.strip():
        raise ValueError(f"{field} is missing")
    family = value.split(".", 1)[0].upper()
    if family not in {"CPU", "GPU"}:
        raise ValueError(f"{field} is unsupported: {value}")
    return family


def validate_worker_spec_against_matrix_case(
    spec: Mapping[str, Any],
    case: Mapping[str, Any],
) -> None:
    """Fail before launch when a worker spec diverges from its matrix row."""

    frozen = _frozen_execution_contract(case)
    if spec.get("controlled_test_id") != case.get("test_id"):
        raise ValueError("worker spec test ID does not match the matrix case")
    if frozen["expected_outcome"] != "pass":
        raise ValueError("matrix expected outcome prohibits worker execution")
    if frozen["execution_route"] not in {
        "patched-stateful",
        "stateful-standard",
        "upstream-scalar",
        "device-standard",
    }:
        raise ValueError("matrix execution route prohibits worker execution")
    if frozen["numeric_generation_metrics_expected"] is not True:
        raise ValueError("matrix numeric generation metrics are not expected")
    if frozen["suitable_host_required"]:
        raise ValueError(
            "matrix suitable host requirement prohibits local worker execution"
        )
    contexts = case.get("contexts")
    if not isinstance(contexts, list) or spec.get("context") not in contexts:
        raise ValueError("worker spec context does not match the matrix case")
    expected_device = _device_family(
        frozen["requested_device"], field="matrix requested device"
    )
    if _device_family(spec.get("device"), field="worker spec device") != (
        expected_device
    ):
        raise ValueError("worker spec device does not match the matrix case")

    properties = spec.get("properties")
    if not isinstance(properties, Mapping):
        raise ValueError("worker spec properties are missing")
    expected_key = frozen["runtime_key_algorithm"]
    expected_value = frozen["runtime_value_algorithm"]
    turboquant = expected_key != "STANDARD" or expected_value != "STANDARD"
    if turboquant:
        if properties.get("TURBOQUANT_KEY_ALGORITHM") != expected_key:
            raise ValueError(
                "worker spec TurboQuant key algorithm does not match "
                "the matrix case"
            )
        if properties.get("TURBOQUANT_VALUE_ALGORITHM") != expected_value:
            raise ValueError(
                "worker spec TurboQuant value algorithm does not match "
                "the matrix case"
            )
        if properties.get("TURBOQUANT_NORM_CORRECTION") is not (
            frozen["norm_correction"]
        ):
            raise ValueError(
                "worker spec norm correction does not match the matrix case"
            )
    elif any(
        field in properties
        for field in (
            "TURBOQUANT_KEY_ALGORITHM",
            "TURBOQUANT_VALUE_ALGORITHM",
            "TURBOQUANT_NORM_CORRECTION",
        )
    ):
        raise ValueError(
            "worker spec requested TurboQuant for a STANDARD matrix case"
        )

    for side, algorithm, precision_field in (
        ("KEY", expected_key, "key_cache_precision"),
        ("VALUE", expected_value, "value_cache_precision"),
    ):
        if algorithm != "STANDARD":
            if f"{side}_CACHE_PRECISION" in properties:
                raise ValueError(
                    f"worker spec {side.lower()} cache precision must be "
                    "owned by TurboQuant"
                )
            continue
        precision = frozen[precision_field]
        if precision in {"f16", "bf16", "u8", "u4"} and (
            properties.get(f"{side}_CACHE_PRECISION") != precision
        ):
            raise ValueError(
                f"worker spec {side.lower()} cache precision does not match "
                "the matrix case"
            )


def validate_runtime_record_against_matrix_case(
    record: Mapping[str, Any],
    case: Mapping[str, Any],
) -> None:
    """Bind accepted activation evidence to the controlled matrix semantics."""

    activation = record.get("activation")
    if not isinstance(activation, Mapping):
        raise ValueError("runtime activation evidence is missing")
    if activation.get("fallback") is not False:
        raise ValueError("runtime activation reports fallback")

    if _frozen_case_field(case, "expected_outcome") != "pass":
        raise ValueError("matrix expected outcome prohibits runtime execution")
    if _frozen_case_field(case, "execution_route") not in {
        "patched-stateful",
        "stateful-standard",
        "upstream-scalar",
        "device-standard",
    }:
        raise ValueError("matrix execution route prohibits runtime execution")
    if _frozen_case_field(case, "numeric_generation_metrics_expected") is not True:
        raise ValueError("matrix numeric generation metrics are not expected")
    expected_key = _frozen_case_field(case, "runtime_key_algorithm")
    expected_value = _frozen_case_field(case, "runtime_value_algorithm")
    expected_device = _device_family(
        _frozen_case_field(case, "requested_device"), field="matrix requested device"
    )
    if _device_family(
        activation.get("device"), field="activation requested device"
    ) != expected_device:
        raise ValueError("activation requested device differs from the matrix")
    if _device_family(
        activation.get("actual_device"), field="activation actual device"
    ) != expected_device:
        raise ValueError("activation actual device differs from the matrix")

    for side, expected in (("key", expected_key), ("value", expected_value)):
        if activation.get(f"requested_{side}_algorithm") != expected:
            raise ValueError(
                f"activation requested {side} algorithm differs from the matrix"
            )
        if activation.get(f"activated_{side}_algorithm") != expected:
            raise ValueError(
                f"activation activated {side} algorithm differs from the matrix"
            )

    turboquant = expected_key != "STANDARD" or expected_value != "STANDARD"
    expected_status = "activated" if turboquant else "not_requested"
    if activation.get("status") != expected_status:
        raise ValueError("activation status differs from the matrix route")
    if activation.get("norm_correction") is not _frozen_case_field(
        case, "norm_correction"
    ):
        raise ValueError("activation norm correction differs from the matrix")
    expected_attention = _frozen_case_field(case, "attention_path")
    if activation.get("attention_path") != expected_attention:
        raise ValueError("activation attention path differs from the matrix")

    scalar_control = _frozen_case_field(case, "execution_route") == "upstream-scalar"
    for side, algorithm, precision_field in (
        ("key", expected_key, "key_cache_precision"),
        ("value", expected_value, "value_cache_precision"),
    ):
        precision = _frozen_case_field(case, precision_field)
        requested = activation.get(
            f"requested_{side}_cache_precision"
        )
        activated = activation.get(
            f"activated_{side}_cache_precision"
        )
        observed = activation.get(f"observed_{side}_state_precision")
        if precision in {"f16", "bf16", "u8", "u4", "u3"}:
            if requested != precision:
                raise ValueError(
                    f"activation requested {side} cache precision differs "
                    "from the matrix"
                )
            if activated != precision:
                raise ValueError(
                    f"activation activated {side} cache precision differs "
                    "from the matrix"
                )
        if algorithm in {"TBQ3", "TBQ4"}:
            if observed != "u8+f32+i32":
                raise ValueError(
                    f"TurboQuant {side} state precision evidence is invalid"
                )
        elif scalar_control and observed != precision:
            raise ValueError(
                f"scalar cache precision was not concretely observed for {side}"
            )
        elif not isinstance(observed, str) or not observed.strip():
            raise ValueError(
                f"STANDARD {side} state precision evidence is missing"
            )


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

    source_spec = _read_json_object(spec_path, "worker spec")
    spec = _sequence_spec(spec_path)
    model_path = _directory(Path(spec["model_path"]), "model")
    matrix = _matrix_case(
        matrix_path,
        spec["controlled_test_id"],
        spec["context"],
    )
    validate_worker_spec_against_matrix_case(spec, matrix["case"])
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
    if source_spec.get("schema") == ADAPTIVE_SPEC_SCHEMA:
        claimed_manifest = Path(source_spec["artifact_manifest_path"]).resolve()
        if (
            os.path.normcase(str(claimed_manifest))
            != os.path.normcase(str(manifest_path))
            or source_spec["artifact_manifest_sha256"]
            != _sha256_file(manifest_path)
            or source_spec["artifact_id"] != artifact.get("artifact_id")
        ):
            raise ValueError(
                "adaptive artifact manifest provenance does not match the "
                "validated measurement input"
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
    launch_minimum_available_ram_mib: int = MIN_LAUNCH_AVAILABLE_RAM_MIB,
    emergency_minimum_available_ram_mib: int = MIN_EMERGENCY_AVAILABLE_RAM_MIB,
    campaign_job: KillOnCloseJob | None = None,
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
    _validate_ram_thresholds(
        launch_minimum_available_ram_mib,
        emergency_minimum_available_ram_mib,
    )

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
        launch_minimum_available_ram_bytes=(
            launch_minimum_available_ram_mib * MIB
        ),
        emergency_minimum_available_ram_bytes=(
            emergency_minimum_available_ram_mib * MIB
        ),
        campaign_job=campaign_job,
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
    """Read a persisted object with strict JSON syntax.

    JSON null remains permitted for optional runtime telemetry and rejected
    attempts. Accepted receipts apply their own non-null closed schema below.
    """

    if not path.is_file():
        return None

    def reject_constant(value: str) -> None:
        raise ValueError(f"non-finite JSON value: {value}")

    def reject_duplicate_keys(
        pairs: list[tuple[str, Any]],
    ) -> dict[str, Any]:
        result: dict[str, Any] = {}
        for key, value in pairs:
            if key in result:
                raise ValueError(f"duplicate JSON key: {key}")
            result[key] = value
        return result

    def contains_nonfinite(value: Any) -> bool:
        if isinstance(value, float):
            return not math.isfinite(value)
        if isinstance(value, Mapping):
            return any(contains_nonfinite(item) for item in value.values())
        if isinstance(value, list):
            return any(contains_nonfinite(item) for item in value)
        return False

    try:
        value = json.loads(
            path.read_text(encoding="utf-8-sig"),
            parse_constant=reject_constant,
            object_pairs_hook=reject_duplicate_keys,
        )
    except (OSError, UnicodeError, json.JSONDecodeError, ValueError):
        return None
    return value if isinstance(value, dict) and not contains_nonfinite(value) else None


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
    matrix_case: Mapping[str, Any],
) -> tuple[dict[str, Any], dict[str, Any], Path] | None:
    expected_spec_sha256 = _sha256_json(role_spec)
    role_root = campaign_root / "attempts" / role
    accepted: list[tuple[dict[str, Any], dict[str, Any], Path]] = []
    root = Path(campaign_root).resolve()
    for attempt_number, attempt_dir in _attempt_directories(role_root):
        receipt_path = attempt_dir / "sequence-receipt.json"
        receipt = _persisted_record(receipt_path)
        if receipt is None:
            if receipt_path.is_file():
                raise RuntimeError(f"{role} accepted receipt is invalid")
            continue
        if receipt.get("accepted") is not True:
            continue
        spec_path = attempt_dir / "spec.json"
        record_path = attempt_dir / "run" / "attempt.json"
        if (
            set(receipt) != _ACCEPTED_RECEIPT_FIELDS
            or type(receipt.get("attempt_number")) is not int
            or receipt["attempt_number"] != attempt_number
            or receipt.get("spec_path")
            != spec_path.relative_to(root).as_posix()
            or receipt.get("runtime_record_path")
            != record_path.relative_to(root).as_posix()
        ):
            raise RuntimeError(f"{role} accepted receipt chain is invalid")
        if (
            receipt.get("schema") != RECEIPT_SCHEMA
            or receipt.get("role") != role
            or receipt.get("campaign_identity_sha256") != identity_sha256
            or receipt.get("spec_sha256") != expected_spec_sha256
        ):
            raise RuntimeError(
                f"{role} accepted receipt does not match campaign identity"
            )
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
        try:
            validate_runtime_record_against_matrix_case(
                record,
                matrix_case,
            )
        except ValueError as error:
            raise RuntimeError(
                f"{role} accepted attempt contradicts the matrix: {error}"
            ) from error
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
    cleanup_proof: Mapping[str, Any] | None = None,
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
    }
    if controller_error is not None:
        receipt["controller_error"] = controller_error
    if cleanup_proof is not None:
        receipt["cleanup_proof"] = dict(cleanup_proof)
    atomic_write_json(attempt_dir / "sequence-receipt.json", receipt)
    return receipt


def _completed_role_cleanup_proof(
    completed: list[tuple[dict[str, Any], dict[str, Any], Path]],
) -> dict[str, Any]:
    bindings = [
        {
            "role": record.get("role"),
            "path": str(Path(path).resolve()),
            "sha256": _sha256_file(path),
        }
        for _receipt, record, path in completed
    ]
    safe = all(
        record.get("cleanup_process_count") == 0
        and record.get("residual_owned_process_count") == 0
        and record.get("emergency_actions", []) == []
        for _receipt, record, _path in completed
    )
    return {
        "safe": safe,
        "source": (
            "validated-completed-role-records" if completed else "no-role-launched"
        ),
        "completed_roles": bindings,
        "cleanup_process_count": 0 if safe else -1,
        "residual_owned_process_count": 0 if safe else -1,
        "emergency_actions": [],
        "active_pids_after_cleanup": [] if safe else None,
    }


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


def _identity_boundary_hashes(identity: Mapping[str, Any]) -> dict[str, str]:
    """Return the individual receipts that must remain stable mid-campaign."""

    value = identity["identity"]
    return {
        "artifact_manifest_sha256": value["model"][
            "artifact_manifest_sha256"
        ],
        "build_provenance_sha256": value["build"]["provenance_sha256"],
        "matrix_sha256": _sha256_json(value["matrix"]),
        "prompt_sha256": value["prompt"]["sha256"],
        "runtime_property_sha256": _sha256_json(value["config"]),
    }


def _sequence_failure(
    message: str,
    *,
    role: str,
    record_path: Path | None,
    record: Mapping[str, Any] | None,
    fingerprint: str,
) -> MeasurementSequenceFailure:
    return MeasurementSequenceFailure(
        message,
        MeasurementFailureRecord(
            role=role,
            record_path=record_path,
            record=dict(record) if record is not None else None,
            fingerprint=fingerprint,
        ),
    )


def _adaptive_record(
    record: Mapping[str, Any],
    identity: Mapping[str, Any],
) -> dict[str, Any]:
    """Bind one immutable governed record to the campaign identity receipts."""

    value = dict(record)
    boundaries = _identity_boundary_hashes(identity)
    command = value.get("command")
    if (
        not isinstance(command, list)
        or not command
        or not all(isinstance(item, str) and item for item in command)
    ):
        raise ValueError("accepted runtime record has no canonical command payload")
    value["identity_hashes"] = {
        "artifact_manifest_sha256": boundaries["artifact_manifest_sha256"],
        "prompt_sha256": boundaries["prompt_sha256"],
        "matrix_sha256": boundaries["matrix_sha256"],
        "build_provenance_sha256": boundaries["build_provenance_sha256"],
        "command_sha256": _sha256_json(command),
        "evidence_sha256": identity["campaign_identity_sha256"],
    }
    value["runtime_property_sha256"] = boundaries["runtime_property_sha256"]
    return value


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
    monotonic_deadline: float | None = None,
    monotonic: Callable[[], float] = time.monotonic,
    launch_minimum_available_ram_mib: int = MIN_LAUNCH_AVAILABLE_RAM_MIB,
    emergency_minimum_available_ram_mib: int = MIN_EMERGENCY_AVAILABLE_RAM_MIB,
    campaign_job: KillOnCloseJob | None = None,
    run_measurement: Callable[..., dict[str, Any]] = run_single_measurement,
) -> dict[str, Any]:
    """Run or resume a complete five-role measurement campaign."""

    _validate_ram_thresholds(
        launch_minimum_available_ram_mib,
        emergency_minimum_available_ram_mib,
    )
    if monotonic_deadline is not None and (
        isinstance(monotonic_deadline, bool)
        or not isinstance(monotonic_deadline, (int, float))
        or not math.isfinite(monotonic_deadline)
    ):
        raise ValueError("monotonic deadline must be finite")
    if not callable(monotonic):
        raise TypeError("monotonic must be callable")
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
    matrix_case = identity["identity"]["matrix"]["case"]
    _campaign_identity(root, identity)

    completed: list[
        tuple[dict[str, Any], dict[str, Any], Path]
    ] = []
    for role in SEQUENCE_ROLES:
        refreshed_identity = build_campaign_identity(
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
        if (
            refreshed_identity["campaign_identity_sha256"] != identity_sha256
            or _identity_boundary_hashes(refreshed_identity)
            != _identity_boundary_hashes(identity)
        ):
            raise _sequence_failure(
                "campaign root belongs to a different canonical identity",
                role=role,
                record_path=None,
                record=None,
                fingerprint=refreshed_identity["campaign_identity_sha256"],
            )
        spec_value = _role_spec(template, role, identity_sha256)
        resumed = _resumable_attempt(
            campaign_root=root,
            role=role,
            role_spec=spec_value,
            identity_sha256=identity_sha256,
            matrix_case=matrix_case,
        )
        if resumed is not None:
            completed.append(resumed)
            continue

        attempt_number, attempt_dir = _next_attempt_directory(root, role)
        attempt_spec = attempt_dir / "spec.json"
        atomic_write_json(attempt_spec, spec_value)
        output_dir = attempt_dir / "run"
        role_timeout = float(timeout_seconds)
        if monotonic_deadline is not None:
            observed = monotonic()
            if (
                isinstance(observed, bool)
                or not isinstance(observed, (int, float))
                or not math.isfinite(observed)
            ):
                raise ValueError("monotonic clock returned an invalid value")
            role_timeout = min(
                role_timeout,
                float(monotonic_deadline) - float(observed),
            )
        if role_timeout <= 0:
            cleanup_proof = _completed_role_cleanup_proof(completed)
            receipt = _write_sequence_receipt(
                campaign_root=root,
                attempt_dir=attempt_dir,
                role=role,
                attempt_number=attempt_number,
                identity_sha256=identity_sha256,
                spec_value=spec_value,
                record_path=None,
                accepted=False,
                controller_error="row monotonic deadline expired before launch",
                cleanup_proof=cleanup_proof,
            )
            receipt_path = attempt_dir / "sequence-receipt.json"
            raise _sequence_failure(
                "row monotonic deadline expired before launch",
                role=role,
                record_path=receipt_path,
                record=receipt,
                fingerprint=identity_sha256,
            )
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
                    timeout_seconds=role_timeout,
                    launch_minimum_available_ram_mib=(
                        launch_minimum_available_ram_mib
                    ),
                    emergency_minimum_available_ram_mib=(
                        emergency_minimum_available_ram_mib
                    ),
                    campaign_job=campaign_job,
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
            raise _sequence_failure(
                f"{type(error).__name__}: {error}",
                role=role,
                record_path=record_path if record_path.is_file() else None,
                record=_persisted_record(record_path),
                fingerprint=identity_sha256,
            ) from error

        record_path = output_dir / "attempt.json"
        persisted = _persisted_record(record_path)
        matrix_error: str | None = None
        if persisted is not None:
            try:
                validate_runtime_record_against_matrix_case(
                    persisted,
                    matrix_case,
                )
            except ValueError as error:
                matrix_error = str(error)
        accepted = (
            persisted is not None
            and persisted == returned
            and persisted.get("role") == role
            and persisted.get("valid") is True
            and persisted.get("cleanup_process_count") == 0
            and _runtime_record_matches_spec(persisted, spec_value, role)
            and matrix_error is None
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
                else (
                    f"runtime activation contradicts matrix: {matrix_error}"
                    if matrix_error is not None
                    else "runtime record is absent, differs from the returned "
                    "record, or did not pass validation"
                )
            ),
        )
        if not accepted or persisted is None:
            raise _sequence_failure(
                f"{role} attempt did not pass validation",
                role=role,
                record_path=record_path if record_path.is_file() else None,
                record=persisted,
                fingerprint=identity_sha256,
            )
        completed.append((receipt, persisted, record_path))

    formal_completed = completed[2:]
    formal_samples: list[dict[str, Any]] = []
    adaptive_samples: list[dict[str, Any]] = []
    for _, record, source in formal_completed:
        try:
            formal_samples.append(measurement_sample(record, source))
            adaptive_samples.append(
                build_adaptive_runtime_sample(
                    _adaptive_record(record, identity),
                    source,
                )
            )
        except Exception as error:
            raise _sequence_failure(
                f"{record['role']} accepted formal record could not be summarized",
                role=record["role"],
                record_path=source,
                record=record,
                fingerprint=identity_sha256,
            ) from error
    try:
        metrics = summarize_samples(formal_samples)
        adaptive_summary = summarize_adaptive_runtime_samples(adaptive_samples)
    except Exception as error:
        _, record, source = formal_completed[-1]
        raise _sequence_failure(
            "accepted formal runtime aggregation failed",
            role=record["role"],
            record_path=source,
            record=record,
            fingerprint=identity_sha256,
        ) from error
    metrics.update(
        {
            "accepted": True,
            "cleanup_process_count": 0,
            "test_id": template["controlled_test_id"],
            "context_tokens": template["context"],
            "campaign_identity_sha256": identity_sha256,
            "runtime_config_sha256": _sha256_json(
                identity["identity"]["config"]
            ),
        }
    )
    metrics_path = root / "measurement-summary.json"
    atomic_write_json(metrics_path, metrics)
    adaptive_metrics_path = root / "adaptive-runtime-summary.json"
    atomic_write_json(
        adaptive_metrics_path,
        adaptive_summary,
    )
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
    monotonic_deadline: float | None = None,
    monotonic: Callable[[], float] = time.monotonic,
    launch_minimum_available_ram_mib: int = MIN_LAUNCH_AVAILABLE_RAM_MIB,
    emergency_minimum_available_ram_mib: int = MIN_EMERGENCY_AVAILABLE_RAM_MIB,
    campaign_job: KillOnCloseJob | None = None,
    run_measurement: Callable[..., dict[str, Any]] = run_single_measurement,
) -> dict[str, Any]:
    """Run or resume a locked complete five-role measurement campaign."""

    _validate_ram_thresholds(
        launch_minimum_available_ram_mib,
        emergency_minimum_available_ram_mib,
    )
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
            monotonic_deadline=monotonic_deadline,
            monotonic=monotonic,
            launch_minimum_available_ram_mib=launch_minimum_available_ram_mib,
            emergency_minimum_available_ram_mib=(
                emergency_minimum_available_ram_mib
            ),
            campaign_job=campaign_job,
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
    parser.add_argument(
        "--launch-minimum-available-ram-mib",
        type=int,
        default=MIN_LAUNCH_AVAILABLE_RAM_MIB,
    )
    parser.add_argument(
        "--emergency-minimum-available-ram-mib",
        type=int,
        default=MIN_EMERGENCY_AVAILABLE_RAM_MIB,
    )
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
        "launch_minimum_available_ram_mib": (
            args.launch_minimum_available_ram_mib
        ),
        "emergency_minimum_available_ram_mib": (
            args.emergency_minimum_available_ram_mib
        ),
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
