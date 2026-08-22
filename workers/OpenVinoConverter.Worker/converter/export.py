"""Narrow dense-Granite FP16 export using the pinned Optimum Intel closure."""

from __future__ import annotations

from pathlib import Path


def export_model(source_path: str, destination_path: str) -> None:
    # Imports are intentionally delayed until after process isolation is verified.
    from optimum.exporters.openvino import main_export
    from optimum.intel.openvino.configuration import OVConfig

    source = Path(source_path)
    destination = Path(destination_path)
    if not source.is_dir() or destination.exists() or not destination.parent.is_dir():
        raise RuntimeError("conversion_preflight_failed")

    weight_format = "fp16"
    main_export(
        model_name_or_path=source,
        output=destination,
        task="text-generation-with-past",
        framework="pt",
        library_name="transformers",
        local_files_only=True,
        trust_remote_code=False,
        convert_tokenizer=True,
        ov_config=OVConfig(dtype=weight_format),
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
