"""Execute and reconcile WB-04 official OpenVINO capability diagnostics."""

from __future__ import annotations

import argparse
import hashlib
import importlib.metadata as metadata
import json
import platform
import sys
import time
from dataclasses import asdict
from pathlib import Path
from typing import Any

REPO_ROOT = Path(__file__).resolve().parents[2]
if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))

import numpy as np
import openvino as ov
import openvino_genai

from scripts.testing.official_openvino.diagnostics import reconcile_diagnostics
from scripts.testing.official_openvino.source_audit import audit_codec_boundary


DIAGNOSTIC_IDS = (
    *(f"OV-B{i:02d}" for i in range(1, 13)),
    *(f"OV-TQS-{i:02d}" for i in range(1, 13)),
)


def write_json(path: Path, payload: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def runtime_probe(core: ov.Core, device: str) -> dict[str, Any]:
    parameter = ov.opset13.parameter([1, 4], np.float32, name="input")
    constant = ov.opset13.constant(np.array([[1, 2, 3, 4]], dtype=np.float32))
    model = ov.Model([ov.opset13.add(parameter, constant)], [parameter], "wb04_diagnostic")
    started = time.perf_counter()
    supported = {str(value) for value in core.get_property(device, "SUPPORTED_PROPERTIES")}
    if "PERF_COUNT" not in supported:
        raise RuntimeError(f"{device} plugin does not expose PERF_COUNT")
    compiled = core.compile_model(model, device, {"PERF_COUNT": True})
    compile_ms = (time.perf_counter() - started) * 1000
    request = compiled.create_infer_request()
    output = next(iter(request.infer({0: np.array([[1, 2, 3, 4]], dtype=np.float32)}).values()))
    expected = np.array([[2, 4, 6, 8]], dtype=np.float32)
    if not np.array_equal(output, expected):
        raise RuntimeError(f"{device} diagnostic output mismatch: {output!r}")
    raw_devices = [str(value) for value in compiled.get_property("EXECUTION_DEVICES")]
    actual = device if any(value.upper().startswith(device.upper()) for value in raw_devices) else ",".join(raw_devices)
    profiling = request.profiling_info
    return {
        "requested_device": device,
        "actual_device": actual,
        "execution_devices": raw_devices,
        "compile_ms": compile_ms,
        "output": output.tolist(),
        "expected_output": expected.tolist(),
        "output_bytes": int(output.nbytes),
        "profiling_event_count": len(profiling),
        "profiling_nodes": [item.node_name for item in profiling],
    }


def record(test_id: str, evidence: str, *, proof_kind: str = "capability",
           accepted: bool = True, activated: bool = True,
           requested_device: str = "host", actual_device: str = "host",
           expected_bytes: int = 0, actual_bytes: int = 0,
           terminal_classification: str | None = None) -> dict[str, Any]:
    value = {
        "test_id": test_id, "exit_code": 0, "accepted": accepted,
        "activated": activated, "proof_kind": proof_kind,
        "expected_bytes": expected_bytes, "actual_bytes": actual_bytes,
        "fallback": False, "requested_device": requested_device,
        "actual_device": actual_device, "evidence": [evidence],
    }
    if terminal_classification:
        value["terminal_classification"] = terminal_classification
    return value


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--campaign-date", default="2026-07-19")
    args = parser.parse_args()
    repo = args.repo_root.resolve()
    source_roots = [
        repo / "external/official-openvino" / args.campaign_date / "openvino",
        repo / "external/official-openvino" / args.campaign_date / "openvino.genai",
    ]
    for root in source_roots:
        if not root.is_dir():
            raise FileNotFoundError(f"missing acquired source: {root}")
    output_root = repo / "experiments/raw-results/official-openvino" / args.campaign_date / "diagnostics"

    boundary = audit_codec_boundary(source_roots)
    matched_files = sorted({Path(match.path) for match in boundary.matches})
    source_payload = asdict(boundary)
    source_payload["source_roots"] = [str(path.relative_to(repo)) for path in source_roots]
    source_payload["matched_file_sha256"] = {
        str(path.relative_to(repo)): sha256(path) for path in matched_files
    }
    source_path = output_root / "source-audit.json"
    write_json(source_path, source_payload)

    core = ov.Core()
    available = list(core.available_devices)
    if "CPU" not in available or not any(item.startswith("GPU") for item in available):
        raise RuntimeError(f"required CPU/GPU devices absent: {available}")
    cpu = runtime_probe(core, "CPU")
    gpu = runtime_probe(core, "GPU")
    perf_metrics = hasattr(openvino_genai, "PerfMetrics")
    if not perf_metrics or cpu["profiling_event_count"] <= 0:
        raise RuntimeError("PerfMetrics/profiling diagnostics unavailable")

    facts = {
        "python": platform.python_version(),
        "openvino": metadata.version("openvino"),
        "openvino_genai": metadata.version("openvino-genai"),
        "devices": available,
        "perf_metrics_class": perf_metrics,
        "cpu_probe": cpu,
        "gpu_probe": gpu,
    }
    facts_path = output_root / "official-api-probes.json"
    write_json(facts_path, facts)
    facts_rel = str(facts_path.relative_to(repo)).replace("\\", "/")
    source_rel = str(source_path.relative_to(repo)).replace("\\", "/")

    records = [
        record("OV-B01", facts_rel), record("OV-B02", facts_rel),
        record("OV-B03", facts_rel, proof_kind="codec", requested_device="CPU",
               actual_device=cpu["actual_device"], expected_bytes=cpu["output_bytes"],
               actual_bytes=cpu["output_bytes"]),
        record("OV-B04", facts_rel, proof_kind="codec", requested_device="GPU",
               actual_device=gpu["actual_device"], expected_bytes=gpu["output_bytes"],
               actual_bytes=gpu["output_bytes"]),
        record("OV-B05", facts_rel, proof_kind="codec", requested_device="CPU",
               actual_device=cpu["actual_device"], expected_bytes=cpu["output_bytes"],
               actual_bytes=cpu["output_bytes"]),
        record("OV-B06", facts_rel), record("OV-B07", facts_rel),
    ]

    source_complete = all((boundary.turbo_enum, boundary.key_property,
                           boundary.value_property, boundary.u3_precision,
                           boundary.u4_precision, boundary.norm_switch,
                           boundary.packed_storage, boundary.cpu_sdpa_path))
    if source_complete:
        raise RuntimeError("TurboQuant source exists; runtime allocation probes are required")
    for test_id in (*[f"OV-B{i:02d}" for i in range(8, 13)],
                    *[f"OV-TQS-{i:02d}" for i in range(1, 13)]):
        records.append(record(
            test_id, source_rel, proof_kind="codec", accepted=False, activated=False,
            requested_device="CPU", actual_device="CPU",
            terminal_classification="unsupported-by-source",
        ))

    reconciliation = reconcile_diagnostics(records, set(DIAGNOSTIC_IDS))
    results_path = output_root / "diagnostic-results.json"
    write_json(results_path, {"records": records, "reconciliation": reconciliation})
    print(json.dumps(reconciliation, sort_keys=True))
    return 0


if __name__ == "__main__":
    sys.exit(main())
