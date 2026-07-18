"""Evidence-based cache and backend activation classification."""

from __future__ import annotations

import re


def _option(command: list[str], name: str) -> str | None:
    try:
        return command[command.index(name) + 1]
    except (ValueError, IndexError):
        return None


def classify_activation(command: list[str], log: str, *, expected_cache: str,
                        backend: str) -> dict[str, object]:
    exact_cache = (_option(command, "-ctk") == expected_cache and
                   _option(command, "-ctv") == expected_cache)
    kv_match = re.search(
        r"llama_kv_cache:\s*(?P<device>CPU|SYCL\d+)\s+KV buffer size\s*=\s*"
        r"(?P<size>\d+(?:\.\d+)?)\s*MiB", log, re.IGNORECASE)
    if not exact_cache:
        return {"activated": False, "reason": "cache flags do not match the controlled row"}
    if not kv_match:
        return {"activated": False, "reason": "runtime KV allocation evidence is missing"}

    actual_device = kv_match.group("device").upper()
    result: dict[str, object] = {
        "activated": True,
        "actual_device": actual_device,
        "kv_mb": float(kv_match.group("size")),
        "proof_kind": ("runtime-plus-source-linked" if expected_cache == "tq3_0"
                       else "runtime-allocation"),
        "reason": "exact cache flags and runtime KV allocation agree",
    }
    if backend == "cpu":
        if _option(command, "-ngl") != "0" or actual_device != "CPU":
            result.update(activated=False, reason="CPU placement evidence does not match request")
    elif backend == "sycl-partial":
        has_device = re.search(r"using device SYCL\d+", log, re.IGNORECASE)
        offload = re.search(r"offloaded\s+(?P<count>\d+)/(?P<total>\d+)\s+layers", log, re.IGNORECASE)
        tensor_override = _option(command, "-ot")
        tensor_placement = bool(tensor_override and "SYCL0" in tensor_override and
                                re.search(r"SYCL0\s+model buffer size", log, re.IGNORECASE))
        layer_placement = _option(command, "-ngl") == "1" and bool(offload)
        if not has_device or not (layer_placement or tensor_placement):
            result.update(activated=False, reason="SYCL device or layer-offload proof is missing")
        elif layer_placement:
            result["offloaded_layers"] = int(offload.group("count"))
            result["total_layers"] = int(offload.group("total"))
        else:
            result["tensor_override"] = tensor_override
    return result
