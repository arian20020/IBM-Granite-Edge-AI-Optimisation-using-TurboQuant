"""Safely execute or terminally classify WB-04 OpenVINO conversions."""

from __future__ import annotations

import argparse
import importlib.metadata as metadata
import json
import sys
from dataclasses import asdict
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))

import psutil
from huggingface_hub import model_info

from scripts.testing.official_openvino.conversion import ConversionSpec, validate_conversion


MODELS = {
    "granite-3b": ("ibm-granite/granite-4.1-3b", "c0650403e44e78ec0262dab1c90914c65b196c4e"),
    "granite-8b": ("ibm-granite/granite-4.1-8b", "1504002f650e656a0a3789d99574df12e3e94ed0"),
}
CASES = (
    ("OV-C01", "granite-3b", "f16"), ("OV-C02", "granite-3b", "u8"),
    ("OV-C03", "granite-3b", "u4"), ("OV-C04", "granite-8b", "f16"),
    ("OV-C05", "granite-8b", "u8"), ("OV-C06", "granite-8b", "u4"),
)
FLOOR_BYTES = 2 * 1024**3
WORKSPACE_BYTES = 1 * 1024**3


def write_json(path: Path, payload: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def repository_evidence(model_id: str, revision: str) -> dict[str, object]:
    info = model_info(model_id, revision=revision, files_metadata=True)
    files = []
    tensor_bytes = 0
    for sibling in info.siblings:
        size = int(sibling.size or 0)
        lfs = sibling.lfs or {}
        digest = lfs.get("sha256") if isinstance(lfs, dict) else getattr(lfs, "sha256", None)
        files.append({"path": sibling.rfilename, "size": size, "lfs_sha256": digest})
        if sibling.rfilename.endswith((".safetensors", ".bin")):
            tensor_bytes += size
    if info.sha != revision or tensor_bytes <= 0:
        raise RuntimeError(f"incomplete immutable model metadata for {model_id}")
    return {"model_id": model_id, "revision": revision,
            "tensor_bytes": tensor_bytes, "files": files}


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--campaign-date", default="2026-07-19")
    args = parser.parse_args()
    output = REPO_ROOT / "experiments/raw-results/official-openvino" / args.campaign_date / "conversion"
    model_evidence = {name: repository_evidence(*identity) for name, identity in MODELS.items()}
    write_json(output / "source-models.json", model_evidence)

    records = []
    for test_id, model_name, precision in CASES:
        source_model, revision = MODELS[model_name]
        available = int(psutil.virtual_memory().available)
        tensor_bytes = int(model_evidence[model_name]["tensor_bytes"])
        estimated_peak = tensor_bytes + WORKSPACE_BYTES
        command = [
            ["hf", "download", source_model, "--revision", revision],
            ["optimum-cli", "export", "openvino", "--model", "<immutable-snapshot-path>",
             "--task", "text-generation-with-past", "--weight-format",
             {"f16": "fp16", "u8": "int8", "u4": "int4"}[precision]],
        ]
        if available >= estimated_peak + FLOOR_BYTES:
            raise RuntimeError(
                f"{test_id} is safe to execute ({available} bytes available); "
                "conversion execution must be enabled rather than terminally classified"
            )
        evidence = {
            "available_ram_bytes": available,
            "required_floor_bytes": FLOOR_BYTES,
            "source_tensor_bytes": tensor_bytes,
            "workspace_allowance_bytes": WORKSPACE_BYTES,
            "estimated_peak_bytes": estimated_peak,
            "required_available_bytes": estimated_peak + FLOOR_BYTES,
            "measurement": "psutil.virtual_memory().available immediately before row",
            "planned_command": command,
        }
        spec = ConversionSpec(
            test_id=test_id, source_model=source_model, source_revision=revision,
            precision=precision, command=[], tool_versions={}, files={}, load_probe={},
            terminal_classification="memory-gate-not-run", terminal_evidence=evidence,
        )
        validation = validate_conversion(spec)
        record = asdict(spec)
        record["validation"] = validation
        record["installed_tool_versions"] = {
            "optimum-intel": metadata.version("optimum-intel"),
            "openvino": metadata.version("openvino"),
        }
        records.append(record)
        write_json(output / test_id / "manifest.json", record)

    write_json(output / "conversion-results.json", {
        "records": records, "row_count": len(records),
        "load_proven_count": 0, "terminal_count": len(records), "failed_count": 0,
    })
    print(json.dumps({"rows": len(records), "terminal": len(records), "failed": 0}))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
