"""Typed, fixed-order input contract for the OpenVINO format boundary retest."""

from __future__ import annotations

import hashlib
import json
import os
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Callable, Literal, Mapping

from scripts.testing.official_openvino.runtime_measurement import (
    build_runtime_property_spec,
)


_ROOT = Path(__file__).resolve().parents[3]
_CPU_ORDER = (
    ("u4", "TBQ3"), ("u4", "TBQ4"), ("u4", "STANDARD"),
    ("u8", "TBQ3"), ("u8", "TBQ4"), ("u8", "STANDARD"),
    ("f16", "TBQ3"), ("f16", "TBQ4"), ("f16", "STANDARD"),
)


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
    return BoundaryManifest(
        cpu_cases=tuple(sorted(cpu_cases, key=lambda item: item.order)),
        gpu_cases=tuple(sorted(gpu_cases, key=lambda item: item.order)),
        source_path=Path(path).resolve(), sha256=hashlib.sha256(raw).hexdigest(),
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
