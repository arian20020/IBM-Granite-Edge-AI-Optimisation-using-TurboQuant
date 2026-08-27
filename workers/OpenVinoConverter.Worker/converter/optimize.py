"""Fresh-package OpenVINO FP16/INT8/INT4 weight optimization."""

from __future__ import annotations

import shutil
from pathlib import Path


def optimize_model(source_path: str, destination_path: str, precision: str) -> None:
    import nncf
    import openvino as ov

    source = Path(source_path)
    destination = Path(destination_path)
    if (
        not source.is_dir()
        or not destination.is_dir()
        or any(destination.iterdir())
        or precision not in {"fp16", "int8", "int4"}
    ):
        raise RuntimeError("conversion_preflight_failed")

    model_path = source / "openvino_model.xml"
    weights_path = source / "openvino_model.bin"
    if not model_path.is_file() or not weights_path.is_file():
        raise RuntimeError("conversion_preflight_failed")

    for item in source.iterdir():
        if not item.is_file() or item.name in {
            "openvino_model.xml",
            "openvino_model.bin",
            "granite-openvino-provenance.json",
            "granite-openvino-optimization.json",
        }:
            continue
        shutil.copy2(item, destination / item.name)

    model = ov.Core().read_model(model_path)
    if precision == "int8":
        model = nncf.compress_weights(
            model,
            mode=nncf.CompressWeightsMode.INT8_ASYM,
            group_size=-1,
            ratio=1.0,
        )
    elif precision == "int4":
        model = nncf.compress_weights(
            model,
            mode=nncf.CompressWeightsMode.INT4_ASYM,
            group_size=-1,
            ratio=1.0,
            all_layers=True,
        )
    ov.save_model(
        model,
        destination / "openvino_model.xml",
        compress_to_fp16=precision == "fp16",
    )

    required = (
        "openvino_model.xml",
        "openvino_model.bin",
        "openvino_tokenizer.xml",
        "openvino_tokenizer.bin",
        "openvino_detokenizer.xml",
        "openvino_detokenizer.bin",
        "config.json",
        "generation_config.json",
        "tokenizer_config.json",
    )
    if any(not (destination / name).is_file() for name in required):
        raise RuntimeError("conversion_output_invalid")
