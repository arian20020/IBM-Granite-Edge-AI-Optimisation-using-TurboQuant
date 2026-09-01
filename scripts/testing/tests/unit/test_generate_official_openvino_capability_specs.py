import hashlib
import json
import tempfile
from pathlib import Path


def _matrix(path: Path) -> None:
    cases = []
    for test_id, key, value, key_precision, value_precision in (
        ("OV-TQS-01", "TBQ4", "TBQ4", "u4", "u4"),
        ("OV-TQS-02", "TBQ3", "TBQ3", "u3", "u3"),
        ("OV-TQS-03", "TBQ4", "TBQ3", "u4", "u3"),
        ("OV-TQS-04", "TBQ3", "TBQ4", "u3", "u4"),
    ):
        cases.append(
            {
                "test_id": test_id,
                "phase": "capability",
                "contexts": [256],
                "requested_device": "CPU",
                "runtime_key_algorithm": key,
                "runtime_value_algorithm": value,
                "key_cache_precision": key_precision,
                "value_cache_precision": value_precision,
                "norm_correction": True,
                "expected_outcome": "pass",
            }
        )
    path.write_text(json.dumps({"cases": cases}), encoding="utf-8")


def test_generates_four_exact_context_row_bound_specs():
    from scripts.testing.tools.generate_official_openvino_capability_specs import (
        generate_capability_specs,
    )

    with tempfile.TemporaryDirectory() as directory:
        root = Path(directory)
        matrix = root / "matrix.json"
        model = root / "model"
        output = root / "specs"
        cache = root / "cache"
        model.mkdir()
        cache.mkdir()
        _matrix(matrix)

        result = generate_capability_specs(
            matrix_path=matrix,
            model_path=model,
            cache_root=cache,
            output_root=output,
        )

        assert set(result) == {
            "OV-TQS-01",
            "OV-TQS-02",
            "OV-TQS-03",
            "OV-TQS-04",
        }
        for test_id, path_text in result.items():
            path = Path(path_text)
            spec = json.loads(path.read_text(encoding="utf-8"))
            assert path == output / test_id / "context-256" / "spec.json"
            assert spec["schema"] == "official-openvino-wb04-worker-spec/v1"
            assert spec["controlled_test_id"] == test_id
            assert spec["context"] == 256
            assert spec["expected_input_tokens"] == 256
            assert spec["prompt"] == " test" * 256
            assert spec["prompt_sha256"] == hashlib.sha256(
                spec["prompt"].encode("utf-8")
            ).hexdigest()
            assert spec["device"] == "CPU"
            assert spec["max_new_tokens"] == 4
            assert spec["role"] == "pilot"
            assert spec["properties"]["TURBOQUANT_NORM_CORRECTION"] is True
            assert Path(spec["properties"]["CACHE_DIR"]).parent == cache.resolve()
            assert test_id.casefold().replace("-", "_") in Path(
                spec["properties"]["CACHE_DIR"]
            ).name
