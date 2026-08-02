"""Allow-listed immutable prompt-contract identities for OpenVINO quality."""

from __future__ import annotations

import hashlib
import json
from collections.abc import Mapping
from dataclasses import dataclass
from pathlib import Path


_ROOT = Path(__file__).resolve().parents[3]


@dataclass(frozen=True)
class QualityContract:
    prompt_set_id: str
    prompt_set_sha256: str
    rendered_root: Path
    maximum_input_tokens: int | None


QUALITY_CONTRACTS: Mapping[str, QualityContract] = {
    "GTQ-PROMPTS-v1": QualityContract(
        prompt_set_id="GTQ-PROMPTS-v1",
        prompt_set_sha256="9ba512818e81e0ba8da3ddc89cf040dd3b779d1edc41db23d3a896d778de807f",
        rendered_root=_ROOT / "experiments/granite_turboquant_intel/prompts/rendered",
        maximum_input_tokens=None,
    ),
    "GTQ-PROMPTS-v2": QualityContract(
        prompt_set_id="GTQ-PROMPTS-v2",
        prompt_set_sha256="78ce1a8876c2b559f8008ec760cadc09c5a7ba37ea543bedb69e1aa48ae7bf18",
        rendered_root=_ROOT / "experiments/granite_turboquant_intel/prompts/rendered-v2",
        maximum_input_tokens=512,
    ),
}


def load_quality_contract(prompt_set_path: Path) -> QualityContract:
    raw = Path(prompt_set_path).read_bytes()
    try:
        document = json.loads(raw)
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise ValueError("prompt contract is invalid JSON") from exc
    if not isinstance(document, dict):
        raise ValueError("prompt contract must be an object")
    contract = QUALITY_CONTRACTS.get(document.get("prompt_set_id"))
    if contract is None:
        raise ValueError("prompt contract is not allow-listed")
    if hashlib.sha256(raw).hexdigest() != contract.prompt_set_sha256:
        raise ValueError("prompt contract hash is not allow-listed")
    return contract
