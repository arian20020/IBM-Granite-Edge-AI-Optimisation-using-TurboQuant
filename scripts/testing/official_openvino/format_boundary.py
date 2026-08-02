"""Typed, fixed-order input contract for the OpenVINO format boundary retest."""

from __future__ import annotations

import hashlib
import json
import os
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Callable, Literal, Mapping

from scripts.testing.official_openvino.adaptive_campaign_spec import (
    _build_root,
    _directory_sha256,
)
from scripts.testing.official_openvino.artifact_inventory import sha256_file
from scripts.testing.official_openvino.runtime_measurement import (
    build_runtime_property_spec,
)
from scripts.testing.official_openvino.workload import build_context_workload


_ROOT = Path(__file__).resolve().parents[3]
_CPU_ORDER = (
    ("u4", "TBQ3"), ("u4", "TBQ4"), ("u4", "STANDARD"),
    ("u8", "TBQ3"), ("u8", "TBQ4"), ("u8", "STANDARD"),
    ("f16", "TBQ3"), ("f16", "TBQ4"), ("f16", "STANDARD"),
)
_FORMAL_METRICS = (
    "available_ram_min_mb", "cpu_percent", "decode_tps",
    "generation_duration_ms", "gpu_memory_peak_mb", "gpu_percent", "kv_mb",
    "load_ms", "peak_private_mb", "peak_working_set_mb", "prompt_tps",
    "tpot_ms", "ttft_ms",
)
_SHA256_HEX = frozenset("0123456789abcdef")


@dataclass(frozen=True)
class ProjectionFile:
    path: Path
    sha256: str


@dataclass(frozen=True)
class ExecutableBoundaryInput:
    case_internal_id: str
    runtime_spec: ProjectionFile


@dataclass(frozen=True)
class TerminalBoundaryInput:
    case_internal_id: str
    descriptor: ProjectionFile


@dataclass(frozen=True)
class BoundaryEvidenceProjection:
    repository_root: Path
    campaign_root: Path
    build_root: Path
    boundary_manifest: ProjectionFile
    comparison_matrix: ProjectionFile
    runtime_specs: tuple[ExecutableBoundaryInput, ...]
    terminal_prerequisites: tuple[TerminalBoundaryInput, ...]
    projection_index: ProjectionFile


@dataclass(frozen=True)
class BoundaryCase:
    internal_id: str
    label: str
    lane: Literal["cpu", "gpu-control"]
    order: int
    weight_precision: Literal["u4", "u8", "f16"]
    key_algorithm: Literal["STANDARD", "TBQ3", "TBQ4"]
    value_algorithm: Literal["STANDARD", "TBQ3", "TBQ4"]
    key_precision: Literal["f16", "u4", "u3"]
    value_precision: Literal["f16", "u4", "u3"]
    device: Literal["CPU", "GPU"]
    context: int
    artifact_manifest_path: Path | None


@dataclass(frozen=True)
class BoundaryManifest:
    cpu_cases: tuple[BoundaryCase, ...]
    gpu_cases: tuple[BoundaryCase, ...]
    source_path: Path
    sha256: str


@dataclass(frozen=True)
class BoundaryCampaignConfig:
    """Immutable controller settings for one serial format-boundary campaign."""

    repository_root: Path
    campaign_root: Path
    manifest_path: Path
    prompt_set_path: Path
    rubric_path: Path
    runtime_timeout_seconds: float = 180.0
    quality_timeout_seconds: float = 90.0
    row_timeout_seconds: float = 720.0
    launch_minimum_available_ram_mib: int = 4096
    emergency_minimum_available_ram_mib: int = 3072
    max_clean_retries: int = 1
    resume: bool = False


class RowFailure(RuntimeError):
    """A governed row failure whose retry safety has already been decided."""

    def __init__(self, reason_code: str, *, hard: bool, receipt: Path | None = None):
        super().__init__(reason_code)
        self.reason_code = reason_code
        self.hard = hard
        self.receipt = receipt


_MIB = 1024**2
_ENVELOPE_TEXT = "outside this laptop's configured safe RAM/time envelope"


def _canonical_bytes(value: Mapping[str, Any]) -> bytes:
    return (json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=True,
                       allow_nan=False) + "\n").encode("utf-8")


def _atomic_write_json(path: Path, value: Mapping[str, Any]) -> None:
    """Commit one state/receipt document without exposing a partial JSON file."""

    target = Path(path)
    target.parent.mkdir(parents=True, exist_ok=True)
    temporary = target.with_name(f".{target.name}.{os.getpid()}.tmp")
    try:
        with temporary.open("xb") as handle:
            handle.write(_canonical_bytes(value))
            handle.flush()
            os.fsync(handle.fileno())
        temporary.replace(target)
    finally:
        if temporary.exists():
            temporary.unlink()


def _read_json_object(path: Path) -> dict[str, Any]:
    try:
        value = json.loads(Path(path).read_bytes())
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as error:
        raise ValueError(f"boundary controller record is unreadable: {path}") from error
    if not isinstance(value, dict):
        raise ValueError(f"boundary controller record is not an object: {path}")
    return value


def _file_sha256(path: Path | None) -> str | None:
    if path is None or not Path(path).is_file():
        return None
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def _empty_lane() -> dict[str, Any]:
    return {
        "status": "running", "reason_code": None, "accepted_count": 0,
        "attempt_count": 0, "skipped_count": 0, "rows": {},
    }


def _initial_state(manifest: BoundaryManifest) -> dict[str, Any]:
    return {
        "schema": "official-openvino-format-boundary-state/v1",
        "manifest_path": str(manifest.source_path), "manifest_sha256": manifest.sha256,
        "cpu_lane": _empty_lane(), "gpu_lane": _empty_lane(),
    }


def _lane_name(case: BoundaryCase) -> str:
    return "cpu_lane" if case.lane == "cpu" else "gpu_lane"


def _lane_directory(case: BoundaryCase) -> str:
    return "cpu" if case.lane == "cpu" else "gpu-control"


def _runtime_is_accepted(value: Any) -> bool:
    """Check the sequence-level proof before allowing isolated quality work."""

    if not isinstance(value, Mapping):
        return False
    return (
        value.get("accepted") is not False
        and value.get("accepted_sample_count") == 3
        and value.get("pilot_passed") is True
        and value.get("warmup_excluded") is True
        and value.get("cleanup_process_count") == 0
        and isinstance(value.get("measurement_summary_path"), str)
        and isinstance(value.get("measurement_summary_sha256"), str)
        and len(value["measurement_summary_sha256"]) == 64
    )


def _quality_is_accepted(value: Any) -> bool:
    if not isinstance(value, Mapping):
        return False
    if not (
        value.get("status") == "passed"
        and value.get("completed_prompt_ids") == ["P1", "P2", "P3", "P4", "P5", "P6"]
        and value.get("prompt_receipt_count") == 6
    ):
        return False
    receipts = value.get("prompt_receipts")
    if receipts is not None:
        return (
            isinstance(receipts, list)
            and len(receipts) == 6
            and all(isinstance(receipt, Mapping) and receipt.get("status") == "passed"
                    and receipt.get("cleanup_process_count") == 0 for receipt in receipts)
        )
    return value.get("cleanup_process_count") == 0


def _accepted_receipt(case: BoundaryCase, runtime: Mapping[str, Any],
                      quality: Mapping[str, Any]) -> dict[str, Any]:
    return {
        "schema": "official-openvino-format-boundary-accepted-row/v1",
        "case": {"internal_id": case.internal_id, "label": case.label,
                 "lane": case.lane, "order": case.order},
        "runtime": dict(runtime), "quality": dict(quality),
    }


def _validate_accepted_receipt(path: Path, case: BoundaryCase) -> dict[str, Any]:
    receipt = _read_json_object(path)
    identity = receipt.get("case")
    if not isinstance(identity, Mapping) or identity.get("internal_id") != case.internal_id:
        raise ValueError("accepted-row receipt belongs to a different boundary case")
    if not _runtime_is_accepted(receipt.get("runtime")):
        raise ValueError("accepted-row receipt has incomplete runtime proof")
    if not _quality_is_accepted(receipt.get("quality")):
        raise ValueError("accepted-row receipt has incomplete quality proof")
    return receipt


def _terminal_receipt(case: BoundaryCase, failure: RowFailure, *, elapsed_seconds: float,
                      available_ram: int | None, manifest_sha256: str) -> dict[str, Any]:
    raw_path = failure.receipt.resolve() if failure.receipt is not None else None
    return {
        "schema": "official-openvino-format-boundary-terminal-boundary/v1",
        "case": {"internal_id": case.internal_id, "label": case.label,
                 "lane": case.lane, "order": case.order},
        "role": "measurement", "fingerprint": manifest_sha256,
        "available_ram_bytes": available_ram,
        "configured_minimum_available_ram_mib": 4096,
        "elapsed_seconds": elapsed_seconds, "reason_code": failure.reason_code,
        "raw_record_path": str(raw_path) if raw_path is not None else None,
        "raw_record_sha256": _file_sha256(raw_path), "cleanup_process_count": 0,
        "envelope": _ENVELOPE_TEXT if "ram" in failure.reason_code or "timeout" in failure.reason_code else None,
    }


def _normalise_failure(error: Exception) -> RowFailure:
    if isinstance(error, RowFailure):
        return error
    sequence_failure = getattr(error, "failure", None)
    record_path = getattr(sequence_failure, "record_path", None)
    record = getattr(sequence_failure, "record", None)
    if isinstance(record, Mapping):
        reason = record.get("failure_code") or record.get("reason_code")
        if isinstance(reason, str) and reason:
            return RowFailure(
                reason,
                hard=reason in {"emergency_minimum_available_ram", "cleanup_failure"},
                receipt=Path(record_path) if record_path is not None else None,
            )
    return RowFailure("measurement_error", hard=False)


def _resume_state(config: BoundaryCampaignConfig, manifest: BoundaryManifest) -> dict[str, Any]:
    state_path = Path(config.campaign_root) / "campaign-state.json"
    state = _read_json_object(state_path)
    if state.get("schema") != "official-openvino-format-boundary-state/v1":
        raise ValueError("boundary campaign state schema is invalid")
    if state.get("manifest_sha256") != manifest.sha256:
        raise ValueError("boundary campaign manifest differs from persisted state")
    for case in (*manifest.cpu_cases, *manifest.gpu_cases):
        receipt = Path(config.campaign_root) / _lane_directory(case) / case.internal_id / "accepted-row.json"
        if receipt.exists():
            _validate_accepted_receipt(receipt, case)
    return state


def run_boundary_campaign(
    config: BoundaryCampaignConfig,
    *,
    run_measurement: Callable[..., Mapping[str, Any]],
    run_quality: Callable[..., Mapping[str, Any]],
    available_ram: Callable[[], int | None],
) -> dict[str, Any]:
    """Execute the low-to-high CPU ladder and its independent GPU control serially."""

    if not isinstance(config, BoundaryCampaignConfig):
        raise TypeError("config must be a BoundaryCampaignConfig")
    if config.max_clean_retries != 1:
        raise ValueError("format-boundary campaigns allow exactly one clean retry")
    manifest = load_boundary_manifest(config.manifest_path)
    root = Path(config.campaign_root).resolve()
    state_path = root / "campaign-state.json"
    if root.exists() and not config.resume:
        if any(root.iterdir()):
            raise ValueError("boundary campaign output exists; use resume=True")
    if config.resume:
        state = _resume_state(config, manifest)
    else:
        root.mkdir(parents=True, exist_ok=True)
        state = _initial_state(manifest)
        _atomic_write_json(state_path, state)

    for cases in (manifest.cpu_cases, manifest.gpu_cases):
        for index, case in enumerate(cases):
            lane_key = _lane_name(case)
            lane = state[lane_key]
            row_root = root / _lane_directory(case) / case.internal_id
            accepted_path = row_root / "accepted-row.json"
            if accepted_path.exists():
                _validate_accepted_receipt(accepted_path, case)
                lane["rows"][case.internal_id] = "accepted"
                continue
            if lane["status"] == "stopped":
                continue
            started = time.monotonic()
            observed_ram = None
            failure = None
            runtime: Mapping[str, Any] | None = None
            quality: Mapping[str, Any] | None = None
            for attempt in range(config.max_clean_retries + 1):
                observed_ram = available_ram()
                lane["attempt_count"] += 1
                if observed_ram is not None and observed_ram < config.emergency_minimum_available_ram_mib * _MIB:
                    failure = RowFailure("emergency_minimum_available_ram", hard=True)
                    break
                if observed_ram is not None and observed_ram < config.launch_minimum_available_ram_mib * _MIB:
                    failure = RowFailure("minimum_available_ram", hard=False)
                    if attempt >= config.max_clean_retries:
                        break
                    continue
                try:
                    _atomic_write_json(state_path, state)
                    runtime = run_measurement(
                        case, config=config, campaign_root=row_root / "runtime",
                        timeout_seconds=config.runtime_timeout_seconds,
                        launch_minimum_available_ram_mib=config.launch_minimum_available_ram_mib,
                        emergency_minimum_available_ram_mib=config.emergency_minimum_available_ram_mib,
                    )
                    if not _runtime_is_accepted(runtime):
                        raise RowFailure("incomplete_runtime_metrics", hard=False)
                    quality = run_quality(
                        case, runtime, config=config, campaign_root=row_root / "quality",
                        timeout_seconds=config.quality_timeout_seconds, resume=False,
                    )
                    if not _quality_is_accepted(quality):
                        raise RowFailure("incomplete_quality_metrics", hard=False)
                    failure = None
                    break
                except Exception as error:  # executor boundary: persist a governed stop.
                    failure = _normalise_failure(error)
                    if failure.hard or attempt >= config.max_clean_retries:
                        break
            if failure is None and runtime is not None and quality is not None:
                receipt = _accepted_receipt(case, runtime, quality)
                _atomic_write_json(accepted_path, receipt)
                lane["accepted_count"] += 1
                lane["rows"][case.internal_id] = "accepted"
                _atomic_write_json(state_path, state)
                continue
            assert failure is not None
            lane["status"] = "stopped"
            lane["reason_code"] = failure.reason_code
            lane["rows"][case.internal_id] = "terminal-boundary"
            terminal = _terminal_receipt(
                case, failure, elapsed_seconds=time.monotonic() - started,
                available_ram=observed_ram, manifest_sha256=manifest.sha256,
            )
            _atomic_write_json(root / _lane_directory(case) / "terminal-boundary.json", terminal)
            for later in cases[index + 1:]:
                lane["rows"][later.internal_id] = "not-attempted-after-boundary"
                lane["skipped_count"] += 1
                _atomic_write_json(
                    root / _lane_directory(later) / later.internal_id / "skipped-row.json",
                    {"schema": "official-openvino-format-boundary-skipped-row/v1",
                     "case": {"internal_id": later.internal_id, "label": later.label,
                              "lane": later.lane, "order": later.order},
                     "status": "not-attempted-after-boundary"},
                )
            _atomic_write_json(state_path, state)
            break
        else:
            lane_key = _lane_name(cases[0])
            if state[lane_key]["status"] == "running":
                state[lane_key]["status"] = "complete"
                _atomic_write_json(state_path, state)
        # The GPU row is a control for a reached CPU boundary, not a recovery
        # route after the CPU ladder has already crossed its safe envelope.
        if cases is manifest.cpu_cases and state["cpu_lane"]["status"] == "stopped":
            return state
    return state


def _artifact_path(value: Any) -> Path | None:
    if value is None:
        return None
    if not isinstance(value, str) or not value:
        raise ValueError("artifact manifest path must be a non-blank string or null")
    candidate = Path(value)
    resolved = candidate.resolve() if candidate.is_absolute() else (_ROOT / candidate).resolve()
    try:
        resolved.relative_to(_ROOT.resolve())
    except ValueError as exc:
        raise ValueError("artifact manifest path escapes repository root") from exc
    return resolved


def _case(raw: Any) -> BoundaryCase:
    if not isinstance(raw, dict):
        raise ValueError("boundary case must be an object")
    try:
        case = BoundaryCase(
            internal_id=raw["internal_id"], label=raw["label"], lane=raw["lane"],
            order=raw["order"], weight_precision=raw["weight_precision"],
            key_algorithm=raw["key_algorithm"], value_algorithm=raw["value_algorithm"],
            key_precision=raw["key_precision"], value_precision=raw["value_precision"],
            device=raw["device"], context=raw["context"],
            artifact_manifest_path=_artifact_path(raw.get("artifact_manifest_path")),
        )
    except KeyError as exc:
        raise ValueError(f"boundary case is missing {exc.args[0]}") from exc
    if not isinstance(case.internal_id, str) or not case.internal_id:
        raise ValueError("boundary internal id must be non-blank")
    if not isinstance(case.label, str) or not case.label:
        raise ValueError("boundary label must be non-blank")
    if case.lane not in {"cpu", "gpu-control"} or case.device not in {"CPU", "GPU"}:
        raise ValueError("boundary lane or device is invalid")
    if case.weight_precision not in {"u4", "u8", "f16"}:
        raise ValueError("boundary weight precision is invalid")
    if case.key_algorithm not in {"STANDARD", "TBQ3", "TBQ4"} or case.value_algorithm != case.key_algorithm:
        raise ValueError("boundary cache algorithms must be matched supported values")
    if case.key_precision not in {"f16", "u4", "u3"} or case.value_precision != case.key_precision:
        raise ValueError("boundary cache precisions must be matched supported values")
    if type(case.order) is not int or case.order < 1 or case.context != 512:
        raise ValueError("boundary order or context is invalid")
    if (case.lane == "cpu") != (case.device == "CPU"):
        raise ValueError("boundary lane/device pairing is invalid")
    if case.device == "GPU" and case.key_algorithm != "STANDARD":
        raise ValueError("GPU TurboQuant cases are prohibited")
    if case.key_algorithm == "STANDARD" and case.key_precision != "f16":
        raise ValueError("STANDARD cache requires F16 precisions")
    if case.key_algorithm == "TBQ3" and case.key_precision != "u3":
        raise ValueError("TBQ3 cache requires U3 precisions")
    if case.key_algorithm == "TBQ4" and case.key_precision != "u4":
        raise ValueError("TBQ4 cache requires U4 precisions")
    return case


def load_boundary_manifest(path: Path) -> BoundaryManifest:
    raw = Path(path).read_bytes()
    try:
        document = json.loads(raw)
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise ValueError("boundary manifest is invalid JSON") from exc
    if not isinstance(document, dict) or document.get("schema") != "official-openvino-format-boundary-matrix/v1":
        raise ValueError("boundary manifest schema is invalid")
    rows = document.get("cases")
    if not isinstance(rows, list):
        raise ValueError("boundary manifest cases must be a list")
    cases = tuple(_case(row) for row in rows)
    if len({case.internal_id for case in cases}) != len(cases):
        raise ValueError("duplicate boundary internal ids")
    cpu_cases = tuple(case for case in cases if case.lane == "cpu")
    gpu_cases = tuple(case for case in cases if case.lane == "gpu-control")
    for lane_cases in (cpu_cases, gpu_cases):
        if len({case.order for case in lane_cases}) != len(lane_cases):
            raise ValueError("duplicate boundary order")
    if tuple((case.weight_precision, case.key_algorithm) for case in sorted(cpu_cases, key=lambda item: item.order)) != _CPU_ORDER:
        raise ValueError("CPU boundary order differs from the global constraint")
    if len(gpu_cases) != 1 or gpu_cases[0].weight_precision != "u4":
        raise ValueError("boundary manifest requires one U4 GPU control")
    expected_terminal_ids = {
        "cpu-f16-tbq3", "cpu-f16-tbq4", "cpu-f16-standard",
    }
    actual_terminal_ids = {
        case.internal_id for case in cases if case.artifact_manifest_path is None
    }
    if actual_terminal_ids != expected_terminal_ids or any(
        case.weight_precision != "f16"
        for case in cases
        if case.internal_id in actual_terminal_ids
    ):
        raise ValueError(
            "only the three F16 boundary identities may have null artifacts"
        )
    return BoundaryManifest(
        cpu_cases=tuple(sorted(cpu_cases, key=lambda item: item.order)),
        gpu_cases=tuple(sorted(gpu_cases, key=lambda item: item.order)),
        source_path=Path(path).resolve(), sha256=hashlib.sha256(raw).hexdigest(),
    )


def _projection_file(path: Path) -> ProjectionFile:
    source = Path(path).resolve()
    return ProjectionFile(path=source, sha256=hashlib.sha256(source.read_bytes()).hexdigest())


def _write_or_validate_json(path: Path, value: Mapping[str, Any]) -> ProjectionFile:
    target = Path(path).resolve()
    expected = _canonical_bytes(value)
    if target.exists():
        if not target.is_file() or target.read_bytes() != expected:
            raise ValueError(f"immutable projection drift detected: {target}")
    else:
        _atomic_write_json(target, value)
    if target.read_bytes() != expected:
        raise ValueError(f"immutable projection drift detected: {target}")
    return ProjectionFile(path=target, sha256=hashlib.sha256(expected).hexdigest())


def _binding(file: ProjectionFile) -> dict[str, str]:
    return {"path": str(file.path), "sha256": file.sha256}


def _require_sha256(value: Any, field: str) -> str:
    if (
        not isinstance(value, str)
        or len(value) != 64
        or any(character not in _SHA256_HEX for character in value)
    ):
        raise ValueError(f"{field} must be a SHA-256 hex digest")
    return value


def _model_file_bindings(manifest: Mapping[str, Any], model_root: Path) -> list[dict[str, Any]]:
    rows = manifest.get("files")
    if not isinstance(rows, list):
        raise ValueError("artifact manifest files must be an array")
    selected: list[dict[str, Any]] = []
    for name in ("openvino_model.bin", "openvino_model.xml"):
        matches = [row for row in rows if isinstance(row, Mapping) and row.get("path") == name]
        if len(matches) != 1:
            raise ValueError(f"artifact manifest must bind exactly one {name}")
        row = matches[0]
        size = row.get("size_bytes")
        model_file = (model_root / name).resolve()
        if (
            isinstance(size, bool)
            or not isinstance(size, int)
            or size <= 0
            or not model_file.is_file()
            or model_file.stat().st_size != size
        ):
            raise ValueError(f"artifact model file is missing or has size drift: {name}")
        expected_sha256 = _require_sha256(row.get("sha256"), f"{name} hash")
        if sha256_file(model_file) != expected_sha256:
            raise ValueError(f"artifact model file hash drift: {name}")
        selected.append(
            {"path": name, "size_bytes": size, "sha256": expected_sha256}
        )
    return selected


def _build_execution_binding(
    build_root: Path,
    *,
    repository_root: Path,
    build_identity: Mapping[str, Any],
) -> dict[str, Any]:
    build = _build_root(build_root)
    package = build / "openvino_genai"
    modules = sorted(package.glob("py_openvino_genai*.pyd"))
    initializer = package / "__init__.py"
    runtime_dll = package / "openvino_genai.dll"
    provenance_value = build_identity.get("path")
    if not isinstance(provenance_value, str) or not provenance_value:
        raise ValueError("build identity provenance path is missing")
    provenance = (repository_root / provenance_value).resolve()
    try:
        provenance.relative_to(repository_root)
    except ValueError as error:
        raise ValueError("build provenance escapes repository root") from error
    if not provenance.is_file():
        raise ValueError(f"build provenance is missing: {provenance}")
    provenance_sha256 = sha256_file(provenance)
    if provenance_sha256 != _require_sha256(
        build_identity.get("sha256"), "build provenance hash"
    ):
        raise ValueError("build provenance hash differs from frozen identity")
    provenance_payload = _read_json_object(provenance)
    if provenance_payload.get("status") != "passed":
        raise ValueError("build provenance does not report passed status")

    def executable_file(path: Path) -> dict[str, str]:
        return {"path": str(path.resolve()), "sha256": sha256_file(path)}

    return {
        "root": str(build),
        "root_sha256": _directory_sha256(build),
        "package_initializer": executable_file(initializer),
        "python_module": executable_file(modules[0]),
        "runtime_dll": executable_file(runtime_dll),
        "provenance": {"path": str(provenance), "sha256": provenance_sha256},
    }


def _artifact_binding(
    case: BoundaryCase,
    *,
    repository_root: Path,
    authoritative_cases: tuple[Any, ...],
) -> dict[str, Any]:
    if case.artifact_manifest_path is None:
        raise ValueError("executable boundary case lacks an artifact manifest")
    candidates = [
        row for row in authoritative_cases
        if row.weight_precision == case.weight_precision
        and row.artifact_status == "available"
    ]
    identities = {
        (
            row.artifact_id,
            str(Path(row.artifact_manifest_path).resolve()),
            row.artifact_manifest_sha256,
        )
        for row in candidates
    }
    if len(identities) != 1:
        raise ValueError(
            f"authoritative artifact identity is ambiguous for {case.weight_precision}"
        )
    artifact_id, expected_path, expected_sha256 = identities.pop()
    manifest_path = case.artifact_manifest_path.resolve()
    if manifest_path != Path(expected_path) or not manifest_path.is_file():
        raise ValueError(f"{case.internal_id} artifact manifest differs from proven identity")
    manifest_sha256 = hashlib.sha256(manifest_path.read_bytes()).hexdigest()
    if manifest_sha256 != expected_sha256:
        raise ValueError(f"{case.internal_id} artifact manifest hash differs from proven identity")
    manifest = _read_json_object(manifest_path)
    if manifest.get("artifact_id") != artifact_id:
        raise ValueError(f"{case.internal_id} artifact id differs from proven identity")
    model_root_raw = manifest.get("artifact_root")
    if not isinstance(model_root_raw, str) or not model_root_raw.strip():
        raise ValueError("artifact manifest model root is missing")
    model_root = Path(model_root_raw).resolve()
    try:
        model_root.relative_to(repository_root)
    except ValueError as error:
        raise ValueError("artifact model root escapes repository root") from error
    if not model_root.is_dir():
        raise ValueError(f"artifact model root is missing: {model_root}")
    return {
        "artifact_id": artifact_id,
        "artifact_manifest_path": str(manifest_path),
        "artifact_manifest_sha256": manifest_sha256,
        "artifact_inventory_sha256": _require_sha256(
            manifest.get("inventory_sha256"), "artifact inventory hash"
        ),
        "model_path": str(model_root),
        "model_files": _model_file_bindings(manifest, model_root),
    }


def _projected_matrix_case(
    case: BoundaryCase,
    *,
    artifact: Mapping[str, Any] | None,
    terminal: ProjectionFile | None,
) -> dict[str, Any]:
    turboquant = case.key_algorithm != "STANDARD"
    return {
        "test_id": case.internal_id,
        "phase": "formal",
        "description": case.label,
        "model": "granite-3b",
        "weight_precision": case.weight_precision,
        "k_algorithm": case.key_algorithm.lower(),
        "v_algorithm": case.value_algorithm.lower(),
        "k_precision": case.key_precision,
        "v_precision": case.value_precision,
        "device": case.device.lower(),
        "contexts": [512],
        "guard": "ram-2048-mib",
        "quality_required": True,
        "required_metrics": list(_FORMAL_METRICS),
        "key_cache_precision": case.key_precision,
        "value_cache_precision": case.value_precision,
        "requested_device": case.device,
        "runtime_key_algorithm": case.key_algorithm,
        "runtime_value_algorithm": case.value_algorithm,
        "norm_correction": turboquant,
        "attention_path": (
            "stateful_sdpa_reference_codec" if turboquant else "stateful_sdpa_standard"
        ),
        "execution_route": "patched-stateful" if turboquant else "stateful-standard",
        "expected_outcome": "pass",
        "suitable_host_required": False,
        "numeric_generation_metrics_expected": True,
        "artifact_id": artifact["artifact_id"] if artifact is not None else None,
        "artifact_manifest_path": (
            artifact["artifact_manifest_path"] if artifact is not None else None
        ),
        "artifact_manifest_sha256": (
            artifact["artifact_manifest_sha256"] if artifact is not None else None
        ),
        "artifact_status": "available" if artifact is not None else "artifact-unavailable",
        "artifact_terminal_path": str(terminal.path) if terminal is not None else None,
        "artifact_terminal_sha256": terminal.sha256 if terminal is not None else None,
    }


def project_boundary_evidence_inputs(
    *,
    repository_root: Path,
    campaign_root: Path,
    build_root: Path,
    manifest_path: Path,
    comparison_matrix_path: Path,
) -> BoundaryEvidenceProjection:
    """Project immutable matrix/spec/prerequisite inputs without model execution."""

    from scripts.testing.official_openvino.matrix import (
        load_matrix,
        load_matrix_metadata,
    )

    repository = Path(repository_root).resolve()
    campaign = Path(campaign_root).resolve()
    if repository != _ROOT.resolve() or not repository.is_dir():
        raise ValueError("repository_root must be this repository")
    manifest = load_boundary_manifest(Path(manifest_path))
    comparison_source = Path(comparison_matrix_path).resolve()
    for source, field in (
        (manifest.source_path, "boundary manifest"),
        (comparison_source, "authoritative comparison matrix"),
    ):
        try:
            source.relative_to(repository)
        except ValueError as error:
            raise ValueError(f"{field} escapes repository root") from error
        if not source.is_file():
            raise ValueError(f"{field} is missing: {source}")

    identities = load_matrix_metadata(comparison_source)
    build_binding = _build_execution_binding(
        Path(build_root),
        repository_root=repository,
        build_identity=identities["build_identity"],
    )
    build = Path(build_binding["root"])
    authoritative_cases = tuple(load_matrix(comparison_source))
    boundary_file = ProjectionFile(manifest.source_path, manifest.sha256)
    authoritative_file = _projection_file(comparison_source)
    output_root = campaign / "execution-inputs"
    cases = (*manifest.cpu_cases, *manifest.gpu_cases)
    artifacts: dict[str, dict[str, Any]] = {}
    verified_artifacts: dict[tuple[str, Path], dict[str, Any]] = {}
    terminals: list[TerminalBoundaryInput] = []
    terminal_files: dict[str, ProjectionFile] = {}

    for case in cases:
        if case.artifact_manifest_path is not None:
            artifact_key = (
                case.weight_precision,
                case.artifact_manifest_path.resolve(),
            )
            if artifact_key not in verified_artifacts:
                verified_artifacts[artifact_key] = _artifact_binding(
                    case,
                    repository_root=repository,
                    authoritative_cases=authoritative_cases,
                )
            artifacts[case.internal_id] = verified_artifacts[artifact_key]
            continue
        descriptor = {
            "schema": "official-openvino-format-boundary-terminal-prerequisite/v1",
            "role": "terminal-prerequisite",
            "reason": "artifact-unavailable",
            "case": {
                "internal_id": case.internal_id,
                "label": case.label,
                "lane": case.lane,
                "order": case.order,
                "weight_precision": case.weight_precision,
                "key_algorithm": case.key_algorithm,
                "value_algorithm": case.value_algorithm,
            },
            "boundary_manifest": _binding(boundary_file),
            "source_identity": identities["source_identity"],
            "build_identity": identities["build_identity"],
        }
        descriptor_file = _write_or_validate_json(
            output_root / "terminal-prerequisites" / f"{case.internal_id}.json",
            descriptor,
        )
        terminal_files[case.internal_id] = descriptor_file
        terminals.append(TerminalBoundaryInput(case.internal_id, descriptor_file))

    matrix_payload = {
        "source_identity": identities["source_identity"],
        "build_identity": identities["build_identity"],
        "cases": [
            _projected_matrix_case(
                case,
                artifact=artifacts.get(case.internal_id),
                terminal=terminal_files.get(case.internal_id),
            )
            for case in cases
        ],
    }
    matrix_file = _write_or_validate_json(
        output_root / "comparison-matrix.json", matrix_payload
    )
    # Validate through the actual governed loader before exposing any specs.
    load_matrix(matrix_file.path)

    executable: list[ExecutableBoundaryInput] = []
    runtime_index: list[dict[str, Any]] = []
    workload = {**build_context_workload(512), "actual_input_tokens": 512}
    for case in cases:
        artifact = artifacts.get(case.internal_id)
        if artifact is None:
            continue
        runtime = build_boundary_worker_spec(
            case,
            role="pilot",
            cache_dir=campaign / "cache" / case.internal_id / "512",
        )
        spec_payload = {
            "schema": "official-openvino-adaptive-comparison-runtime-spec/v1",
            "controlled_test_id": case.internal_id,
            "artifact_id": artifact["artifact_id"],
            "artifact_manifest_path": artifact["artifact_manifest_path"],
            "artifact_manifest_sha256": artifact["artifact_manifest_sha256"],
            "model_path": artifact["model_path"],
            "device": runtime["device"],
            "context_tokens": 512,
            "workload": workload,
            "properties": runtime["properties"],
            "max_new_tokens": 4,
            "ignore_eos": True,
            "seed": 42,
            "apply_chat_template": False,
        }
        spec_file = _write_or_validate_json(
            output_root / "runtime-specs" / case.internal_id / "512" / "runtime-spec.json",
            spec_payload,
        )
        executable.append(ExecutableBoundaryInput(case.internal_id, spec_file))
        runtime_index.append(
            {
                "case_internal_id": case.internal_id,
                "runtime_spec": _binding(spec_file),
                "artifact_binding": artifact,
            }
        )

    terminal_index = [
        {"case_internal_id": item.case_internal_id,
         "descriptor": _binding(item.descriptor)}
        for item in terminals
    ]
    index_payload = {
        "schema": "official-openvino-format-boundary-projection-index/v1",
        "roots": {
            "repository": str(repository),
            "campaign": str(campaign),
            "build": str(build),
        },
        "boundary_manifest": _binding(boundary_file),
        "authoritative_comparison_matrix": _binding(authoritative_file),
        "projected_comparison_matrix": _binding(matrix_file),
        "source_identity": identities["source_identity"],
        "build_identity": identities["build_identity"],
        "build": build_binding,
        "runtime_specs": runtime_index,
        "terminal_prerequisites": terminal_index,
    }
    index_file = _write_or_validate_json(output_root / "projection-index.json", index_payload)
    return BoundaryEvidenceProjection(
        repository_root=repository,
        campaign_root=campaign,
        build_root=build,
        boundary_manifest=boundary_file,
        comparison_matrix=matrix_file,
        runtime_specs=tuple(executable),
        terminal_prerequisites=tuple(terminals),
        projection_index=index_file,
    )


def build_boundary_worker_spec(
    case: BoundaryCase, *, role: str, cache_dir: Path,
) -> dict[str, Any]:
    if role not in {"pilot", "sample", "warmup"}:
        raise ValueError("boundary worker role is invalid")
    if not isinstance(cache_dir, Path):
        raise ValueError("cache_dir must be a Path")
    if case.device == "CPU":
        runtime = build_runtime_property_spec(
            device=case.device, key_algorithm=case.key_algorithm,
            value_algorithm=case.value_algorithm, key_cache_precision=case.key_precision,
            value_cache_precision=case.value_precision,
            norm_correction=case.key_algorithm != "STANDARD", cache_dir=str(cache_dir),
        )
    else:
        runtime = {
            "device": "GPU",
            "properties": {
                "ATTENTION_BACKEND": "SDPA", "CACHE_DIR": str(cache_dir),
                "PERFORMANCE_HINT": "LATENCY", "KEY_CACHE_PRECISION": "f16",
                "VALUE_CACHE_PRECISION": "f16",
            },
        }
    return {"role": role, "internal_id": case.internal_id, "label": case.label,
            "context": case.context, **runtime}
