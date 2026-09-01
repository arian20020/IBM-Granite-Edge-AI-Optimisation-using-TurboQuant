"""Build deterministic, source-bound release evidence for controlled WB-04.

This module writes release evidence only.  It deliberately has no code path
that opens or modifies the workbook, revision registers, or generated DOCX.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import tempfile
from pathlib import Path
from typing import Any, Iterable, Mapping, Sequence


STATIC_SECTION_NUMBERS = (1, 2, 3, 4)
_SECTION_HEADING = re.compile(r"^# ([0-9]+)\. .+$", re.MULTILINE)
MATRIX_SHA256 = "7db2636b403d285aa3886c9f23560a9e48bd43645e9aca35e0d1fc4f16eaea42"
MATRIX_RELATIVE_PATH = "experiments/manifests/official-openvino/retest-matrix.json"
CAMPAIGN_DATE = "2026-07-30"
WORKBOOK_VERSION = "1.8"
REVISION_ID = "WR-036"
RELEASE_INPUT_SCHEMA = "official-openvino-wb04-release-input/v2"
STATIC_SECTION_EVIDENCE_SCHEMA = (
    "official-openvino-wb04-static-section-evidence/v2"
)

MEASURED_SOURCES = {
    ("OV-TQ-13", 512): (
        "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "runtime/OV-TQ-13/context-512/measurement-summary.json",
        "5fc814f16d749e863607db5f519ee564b6459f132a0b25c2a6aab800b8c7c550",
    ),
    ("OV-TQ-14", 512): (
        "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "runtime-frozen-85ed31e/OV-TQ-14/context-512/measurement-summary.json",
        "3aea0c66a05faa64283cec1e59c16a9669fcbcb2c8520bfb3f5fadae0e0e5747",
    ),
    ("OV-TQ-14", 2048): (
        "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "runtime/OV-TQ-14/context-2048/measurement-summary.json",
        "2b3fbde825b91e4768325be0ff58254110de4c5e36981cf9d570806a11e13478",
    ),
}

EXPECTED_REJECTION_KEYS = {
    ("OV-04", 4096),
    ("OV-05", 4096),
    ("OV-TQ-01", 4096),
    ("OV-TQ-02", 4096),
    ("OV-TQ-18", 1024),
    ("OV-TQ-19", 256),
    ("OV-TQ-20", 256),
}

DIRECT_TERMINAL_KEYS = {
    ("OV-01", 1024),
    ("OV-02", 2048),
    ("OV-03", 4096),
    ("OV-06", 4096),
    ("OV-07", 2048),
    ("OV-08", 4096),
    ("OV-09", 4096),
    ("OV-10", 4096),
    ("OV-TQ-16", 4096),
    ("OV-TQ-17", 4096),
}

RESOURCE_ANCHOR_KEY = ("OV-TQ-13", 2048)
RESOURCE_ENVELOPE_KEYS = {
    *((f"OV-TQ-{number:02d}", 4096) for number in range(3, 13)),
    ("OV-TQ-13", 4096),
    ("OV-TQ-13", 8192),
    ("OV-TQ-14", 4096),
    ("OV-TQ-14", 8192),
    ("OV-TQ-15", 4096),
}

OV03_ATTEMPTS = (
    (
        "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "runtime-frozen-85ed31e/OV-03/context-4096/attempts/pilot/"
        "attempt-001/run/attempt.json",
        "8bbe8116f764784feb88f30cfbf1120354e33c42250df30d6025d5f32f30c6eb",
    ),
    (
        "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "runtime-frozen-85ed31e/OV-03/context-4096/attempts/pilot/"
        "attempt-002/run/attempt.json",
        "f278d5e4823cd981b3ddab4007814fb0a9c641373c0b759a6f5670d6849da146",
    ),
    (
        "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "runtime-frozen-85ed31e/OV-03/context-4096/attempts/pilot/"
        "attempt-003/run/attempt.json",
        "c13329c21bbc644a35d74ee425a09f7c50166ef8b81948c987a6a28e65d62efb",
    ),
)

TQ13_2048_ATTEMPTS = (
    (
        "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "runtime/OV-TQ-13/context-2048/attempts/pilot/"
        "attempt-001/run/attempt.json",
        "6c898bfed2a7a146cd628d397638d1e1d2653ec01d99942f5a3bcba6c38f504a",
    ),
    (
        "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "runtime/OV-TQ-13/context-2048/attempts/pilot/"
        "attempt-002/run/attempt.json",
        "549dbce9735b5392f37439a75f9f376555b72a2a82adb258e2bc7bf8174a69c4",
    ),
    (
        "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "runtime/OV-TQ-13/context-2048/attempts/pilot/"
        "attempt-003/run/attempt.json",
        "6b936ea1892dca7beefee7d2803832221737b2dabc40f1d5fcdb4bb745a5e421",
    ),
)

HOST_RESOURCE_PROVENANCE = {
    (
        "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "runtime-frozen-85ed31e/OV-03/context-4096/"
        "campaign-identity.json"
    ): "91e629cdcff00ee7e9fa22b81006702e0483821a09420b6b58bb6efc4e1fa315",
    (
        "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "runtime-frozen-85ed31e/OV-03/context-4096/attempts/pilot/"
        "attempt-001/spec.json"
    ): "aa0290ad70477fd0ef964dd376b35bddbd1136bdf552f73ae0ef924a9f600ae3",
    (
        "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "runtime-frozen-85ed31e/OV-03/context-4096/attempts/pilot/"
        "attempt-001/sequence-receipt.json"
    ): "d23cfdaf3d86677cb257ac0faae782ad2fecea3a273ab91e98f9be49347afb95",
    (
        "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "runtime-frozen-85ed31e/OV-03/context-4096/attempts/pilot/"
        "attempt-002/spec.json"
    ): "aa0290ad70477fd0ef964dd376b35bddbd1136bdf552f73ae0ef924a9f600ae3",
    (
        "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "runtime-frozen-85ed31e/OV-03/context-4096/attempts/pilot/"
        "attempt-002/sequence-receipt.json"
    ): "d4c3cf52ce0dcbb68887050287a0db73295d38102dd1efa83f30ab81d3c12481",
    (
        "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "runtime-frozen-85ed31e/OV-03/context-4096/attempts/pilot/"
        "attempt-003/spec.json"
    ): "aa0290ad70477fd0ef964dd376b35bddbd1136bdf552f73ae0ef924a9f600ae3",
    (
        "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "runtime-frozen-85ed31e/OV-03/context-4096/attempts/pilot/"
        "attempt-003/sequence-receipt.json"
    ): "08c7342e743b5d110c699aa9d1e111619530a2d090f7616240515ba13de260c8",
    (
        "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "runtime/OV-TQ-13/context-2048/campaign-identity.json"
    ): "2d56b86910988d71adfc80231ffa5bf94f94bc31c886f00c277ea2537901ba2c",
    (
        "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "runtime/OV-TQ-13/context-2048/attempts/pilot/"
        "attempt-001/spec.json"
    ): "173ea563519338dc7c5812550a62ba970c1473e18498707c0cf8856533b5a7b6",
    (
        "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "runtime/OV-TQ-13/context-2048/attempts/pilot/"
        "attempt-001/sequence-receipt.json"
    ): "03cd4e52ce77e0a20b2af6a47109f6389b5cb5a1ed399b3c93534adb72a1600a",
    (
        "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "runtime/OV-TQ-13/context-2048/attempts/pilot/"
        "attempt-002/spec.json"
    ): "173ea563519338dc7c5812550a62ba970c1473e18498707c0cf8856533b5a7b6",
    (
        "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "runtime/OV-TQ-13/context-2048/attempts/pilot/"
        "attempt-002/sequence-receipt.json"
    ): "1b80861062c22deb4fe893dff68d911a8879ff8ad962cd9febb861fc8feda153",
    (
        "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "runtime/OV-TQ-13/context-2048/attempts/pilot/"
        "attempt-003/spec.json"
    ): "173ea563519338dc7c5812550a62ba970c1473e18498707c0cf8856533b5a7b6",
    (
        "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "runtime/OV-TQ-13/context-2048/attempts/pilot/"
        "attempt-003/sequence-receipt.json"
    ): "f2dcb58eafe5bddaf043d7ef694934a60441f392b63d129622cb4ee5e4b025b0",
}

OV06_ATTEMPTS = (
    (
        "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "diagnostics/ov-06-gpu-calibration-attempt-001/run/attempt.json",
        "dfa6f7ff6fc937a05a95b397755e9d818cb1762e2ee4af4df211a9ddbca65541",
    ),
    (
        "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "diagnostics/ov-06-gpu-calibration-attempt-002/run/attempt.json",
        "84c77a2f7eae277bc62d5731cdb2e320697d328c3ebdccbf40a8389f25f51677",
    ),
    (
        "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "diagnostics/ov-06-gpu-calibration-attempt-003/run/attempt.json",
        "73404a70f1dece6e8b0e93b145d6381791ef4f861b8a88c96c15bc01b75f9e49",
    ),
)

QUALITY_GUARDS = (
    (
        "experiments/raw-results/openvino-turboquant/2026-07-31/"
        "quality-bound-input-attempt-001/OV-TQ-14/context-512/governed/"
        "guard-evidence.json",
        "96fde946340c50296dcad2de378212cf743f6ee5de84b975dbf8b20a104d91b1",
        "experiments/raw-results/openvino-turboquant/2026-07-31/"
        "quality-bound-input-attempt-001/OV-TQ-14/context-512/governed/"
        "worker-spec.json",
        "eba70f4801453a864e35404e75e2a51256b27beae48d611d4e1e3773ca092fb5",
    ),
    (
        "experiments/raw-results/openvino-turboquant/2026-07-31/"
        "quality-bound-input-attempt-002/OV-TQ-14/context-512/governed/"
        "guard-evidence.json",
        "d0d1623ae293a238c156d2e741d8b3be392daf0558e6e4b08df9db74e3958317",
        "experiments/raw-results/openvino-turboquant/2026-07-31/"
        "quality-bound-input-attempt-002/OV-TQ-14/context-512/governed/"
        "worker-spec.json",
        "eba70f4801453a864e35404e75e2a51256b27beae48d611d4e1e3773ca092fb5",
    ),
)

HOST_MANIFEST = (
    "experiments/granite_turboquant_intel/manifests/environments/"
    "ENV-20260730-INTEL-LAPTOP-01-WB04/machine-manifest.json",
    "23b4fb6e849ae6cf7538d0d3ae17d5859cb88a664a2f83135105146b50488b17",
)

EXPECTED_REJECTION_AGGREGATES = {
    "scalar": (
        "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "expected-rejections/scalar-semantic-rejections.json",
        "f0b9e4fda6469276f7e6d2834987aa7749d1e181c4746a87b3122d5549dbba5a",
    ),
    "property": (
        "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "expected-rejections/property-expected-rejections-current.json",
        "ccb6460aac37e6444054d55cf571ecdd39313d45ae5a72270d3a7a972b7314a6",
    ),
}

ARTIFACT_MANIFESTS = {
    (
        "experiments/raw-results/openvino-turboquant/2026-07-30/artifacts/"
        "granite-4.1-3b-u4-ea0231a/artifact-manifest.json"
    ): "48f78f6e7b8ec6a30ac40e2899939405634995dc97d8a1bb3d1d7582554980ba",
    (
        "experiments/raw-results/openvino-turboquant/2026-07-30/artifacts/"
        "granite-4.1-3b-u8-f858c9e/artifact-manifest.json"
    ): "e23963bc56af1e4fc4a082c8643e22e645bd279a172d6a01674de8fa01ff0804",
}

TERMINAL_OUTCOME = {
    "host-resource-blocked": "not-produced-by-resource-blocked",
    "device-resource-blocked": "not-produced-by-resource-blocked",
    "host-resource-blocked-not-launched": "not-produced-by-resource-envelope",
    "suitable-host-required-not-launched": (
        "not-produced-by-suitable-host-requirement"
    ),
    "controlled-terminal-not-launched": "not-produced-by-controlled-terminal",
}

QUALITY_RESOURCE_BLOCKED_STATUS = "quality-host-resource-blocked"
QUALITY_RESOURCE_ENVELOPE_STATUS = "quality-host-resource-blocked-not-launched"
QUALITY_RESOURCE_BLOCKED_OUTCOME = "not-produced-by-quality-resource-blocked"
QUALITY_RESOURCE_ENVELOPE_OUTCOME = "not-produced-by-quality-resource-envelope"


def _sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def _sha256_file(path: Path) -> str:
    return _sha256_bytes(path.read_bytes())


def _canonical_sha256(value: Mapping[str, Any]) -> str:
    encoded = (
        json.dumps(
            value,
            ensure_ascii=False,
            allow_nan=False,
            sort_keys=True,
            separators=(",", ":"),
        )
        + "\n"
    ).encode("utf-8")
    return _sha256_bytes(encoded)


def _evidence_canonical_sha256(value: Mapping[str, Any]) -> str:
    """Match the canonical newline-terminated expected-rejection artifacts."""

    return _canonical_sha256(value)


def _json_bytes(value: Any) -> bytes:
    return (
        json.dumps(
            value,
            ensure_ascii=False,
            allow_nan=False,
            indent=2,
            sort_keys=True,
        )
        + "\n"
    ).encode("utf-8")


def _read_json(path: Path) -> Any:
    try:
        return json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        raise ValueError(f"invalid JSON evidence: {path}") from exc


def _within_repo(repo_root: Path, path: Path, field: str) -> Path:
    resolved = path.resolve()
    try:
        resolved.relative_to(repo_root.resolve())
    except ValueError as exc:
        raise ValueError(f"{field} must remain inside the repository") from exc
    return resolved


def _relative(repo_root: Path, path: Path) -> str:
    return _within_repo(repo_root, path, "evidence path").relative_to(
        repo_root.resolve()
    ).as_posix()


def _repository_local_evidence_path(repo_root: Path, value: str) -> Path:
    root = repo_root.resolve()
    resolved = Path(value).resolve()
    try:
        resolved.relative_to(root)
        return resolved
    except ValueError:
        parts = resolved.parts
        lowered = [part.casefold() for part in parts]
        matches = [
            index
            for index in range(len(parts) - 1)
            if lowered[index : index + 2] == ["experiments", "raw-results"]
        ]
        if len(matches) != 1:
            raise ValueError("historical evidence path is not repository-relative")
        candidate = (root / Path(*parts[matches[0] :])).resolve()
        _within_repo(root, candidate, "historical evidence path")
        return candidate


def _source_ref(
    repo_root: Path,
    relative_path: str,
    expected_sha256: str | None = None,
) -> dict[str, str]:
    path = _within_repo(repo_root, repo_root / relative_path, "source evidence")
    if not path.is_file():
        raise ValueError(f"missing source evidence: {relative_path}")
    actual = _sha256_file(path)
    if expected_sha256 is not None and actual != expected_sha256:
        raise ValueError(f"source evidence SHA-256 mismatch: {relative_path}")
    return {"path": relative_path, "sha256": actual}


def _generated_ref(
    repo_root: Path, path: Path, payload: Mapping[str, Any]
) -> dict[str, str]:
    return {
        "path": _relative(repo_root, path),
        "sha256": _sha256_bytes(_json_bytes(payload)),
    }


def _atomic_write(path: Path, data: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary_name = tempfile.mkstemp(
        prefix=f".{path.name}.", suffix=".tmp", dir=path.parent
    )
    temporary = Path(temporary_name)
    try:
        with os.fdopen(descriptor, "wb") as stream:
            stream.write(data)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
    finally:
        if temporary.exists():
            temporary.unlink()


def _runtime_cases(matrix: Mapping[str, Any]) -> dict[tuple[str, int], dict[str, Any]]:
    cases = matrix.get("cases")
    if not isinstance(cases, list) or len(cases) != 60:
        raise ValueError("frozen matrix must contain exactly 60 cases")
    result: dict[tuple[str, int], dict[str, Any]] = {}
    for case in cases:
        if not isinstance(case, dict) or case.get("phase") not in {
            "baseline",
            "formal",
        }:
            continue
        contexts = case.get("contexts")
        if not isinstance(contexts, list) or not contexts:
            raise ValueError("runtime matrix case has no contexts")
        for context in contexts:
            key = (case.get("test_id"), context)
            if (
                not isinstance(key[0], str)
                or type(context) is not int
                or key in result
            ):
                raise ValueError("runtime matrix contains an invalid composite key")
            result[key] = case
    if len(result) != 36:
        raise ValueError("frozen matrix must expand to exactly 36 runtime rows")
    return result


def _quality_keys(
    runtime_cases: Mapping[tuple[str, int], Mapping[str, Any]]
) -> set[tuple[str, int]]:
    result = {
        key
        for key, case in runtime_cases.items()
        if case.get("model") in {"granite-3b", "granite-8b"}
    }
    if len(result) != 33:
        raise ValueError("frozen matrix must expand to exactly 33 quality rows")
    return result


def _validate_governed_low_memory(
    repo_root: Path,
    sources: Sequence[tuple[str, str]],
    *,
    label: str,
) -> list[dict[str, str]]:
    references: list[dict[str, str]] = []
    campaign_identity_paths: set[str] = set()
    for relative_path, expected_sha256 in sources:
        reference = _source_ref(repo_root, relative_path, expected_sha256)
        attempt = _read_json(repo_root / relative_path)
        minimum = (
            attempt.get("available_ram_bytes", {}).get("minimum")
            if isinstance(attempt.get("available_ram_bytes"), dict)
            else None
        )
        if (
            attempt.get("schema") != "official-openvino-wb04-governed-run/v1"
            or attempt.get("role") != "pilot"
            or attempt.get("valid") is not False
            or attempt.get("low_memory_stop") is not True
            or attempt.get("timed_out") is not False
            or attempt.get("cleanup_process_count") != 0
            or type(minimum) is not int
            or minimum > 2048 * 1024 * 1024
        ):
            raise ValueError(f"{label} does not prove a governed RAM-floor stop")
        references.append(reference)
        attempt_path = Path(relative_path)
        attempt_root = attempt_path.parent.parent
        for sibling_name in ("spec.json", "sequence-receipt.json"):
            sibling_path = (attempt_root / sibling_name).as_posix()
            expected_sibling_sha256 = HOST_RESOURCE_PROVENANCE.get(
                sibling_path
            )
            if expected_sibling_sha256 is None:
                raise ValueError(
                    f"{label} {sibling_name} has no reviewed provenance hash"
                )
            references.append(
                _source_ref(
                    repo_root, sibling_path, expected_sibling_sha256
                )
            )
        campaign_identity_paths.add(
            (attempt_path.parents[4] / "campaign-identity.json").as_posix()
        )
    if len(campaign_identity_paths) != 1:
        raise ValueError(f"{label} attempts do not share one campaign identity")
    identity_path = next(iter(campaign_identity_paths))
    expected_identity_sha256 = HOST_RESOURCE_PROVENANCE.get(identity_path)
    if expected_identity_sha256 is None:
        raise ValueError(f"{label} campaign identity has no reviewed hash")
    references.append(
        _source_ref(repo_root, identity_path, expected_identity_sha256)
    )
    if len({item["sha256"] for item in references}) != len(references):
        attempt_hashes = [
            item["sha256"]
            for item in references
            if item["path"].endswith("/attempt.json")
        ]
        if len(set(attempt_hashes)) != len(attempt_hashes):
            raise ValueError(f"{label} attempts are not distinct")
    return references


def _validate_measurement(
    repo_root: Path, key: tuple[str, int], source: tuple[str, str]
) -> tuple[dict[str, str], dict[str, Any]]:
    reference = _source_ref(repo_root, source[0], source[1])
    summary = _read_json(repo_root / source[0])
    if (
        summary.get("schema") != "official-openvino-wb04-measurement-summary/v1"
        or summary.get("test_id") != key[0]
        or summary.get("context_tokens") != key[1]
        or summary.get("status") != "measured"
        or summary.get("accepted") is not True
        or summary.get("sample_count") != 3
        or summary.get("cleanup_process_count") != 0
        or not isinstance(summary.get("sources"), list)
        or len(summary["sources"]) != 3
    ):
        raise ValueError(f"selected summary is not an accepted 3-sample row: {key}")
    return reference, summary


def _terminal_payload(
    key: tuple[str, int],
    *,
    status: str,
    reason: str,
    source_evidence: Sequence[Mapping[str, str]],
) -> dict[str, Any]:
    outcome = TERMINAL_OUTCOME[status]
    return {
        "schema": "official-openvino-wb04-terminal-classification/v1",
        "test_id": key[0],
        "context_tokens": key[1],
        "status": status,
        "reason": reason,
        "sample_count": 0,
        "cleanup_process_count": 0,
        "metric_outcome": outcome,
        "source_evidence": list(source_evidence),
    }


def _terminal_record(
    key: tuple[str, int],
    *,
    status: str,
    reason: str,
    evidence: Mapping[str, str],
) -> dict[str, Any]:
    return {
        "test_id": key[0],
        "context_tokens": key[1],
        "accepted": False,
        "status": status,
        "reason": reason,
        "sample_count": 0,
        "cleanup_process_count": 0,
        "metric_outcome": TERMINAL_OUTCOME[status],
        "evidence_path": evidence["path"],
        "evidence_sha256": evidence["sha256"],
    }


def _quality_payload(
    key: tuple[str, int],
    *,
    status: str,
    reason: str,
    score_outcome: str,
    source_evidence: Sequence[Mapping[str, str]],
) -> dict[str, Any]:
    return {
        "schema": "official-openvino-wb04-quality-terminal-classification/v1",
        "test_id": key[0],
        "context_tokens": key[1],
        "status": status,
        "reason": reason,
        "prompt_count": 0,
        "score_outcome": score_outcome,
        "cleanup_process_count": 0,
        "source_evidence": list(source_evidence),
    }


def _quality_record(
    key: tuple[str, int],
    *,
    status: str,
    reason: str,
    score_outcome: str,
    evidence: Mapping[str, str],
) -> dict[str, Any]:
    return {
        "test_id": key[0],
        "context_tokens": key[1],
        "status": status,
        "reason": reason,
        "prompt_count": 0,
        "score_outcome": score_outcome,
        "evidence_path": evidence["path"],
        "evidence_sha256": evidence["sha256"],
    }


def _build_inventory(
    repo_root: Path,
    output_root: Path,
) -> tuple[Path, dict[str, Any]]:
    artifact_root = (
        repo_root
        / "experiments/raw-results/openvino-turboquant/2026-07-30/artifacts"
    )
    spec_root = (
        repo_root
        / "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "campaign-specs-dbbb784"
    )
    inventory_roots = sorted(
        (_relative(repo_root, artifact_root), _relative(repo_root, spec_root))
    )
    discovered_artifacts = {
        _relative(repo_root, path)
        for path in artifact_root.glob("*/artifact-manifest.json")
    }
    if discovered_artifacts != set(ARTIFACT_MANIFESTS):
        raise ValueError("reviewed artifact-manifest inventory has changed")
    artifact_entries: list[dict[str, Any]] = []
    for relative_path in sorted(discovered_artifacts):
        reference = _source_ref(
            repo_root, relative_path, ARTIFACT_MANIFESTS[relative_path]
        )
        artifact = _read_json(repo_root / relative_path)
        artifact_id = artifact.get("artifact_id")
        precision = (
            "u4"
            if artifact_id == "granite-4.1-3b-u4-openvino-ea0231a"
            else "u8"
            if artifact_id == "granite-4.1-3b-u8-openvino-f858c9e"
            else None
        )
        if precision is None or artifact.get("status") != "load-proven":
            raise ValueError("artifact inventory includes an unreviewed artifact")
        artifact_entries.append(
            {
                **reference,
                "model": "granite-3b",
                "weight_precision": precision,
            }
        )

    expected_spec_keys = {
        ("OV-03", 4096),
        ("OV-06", 4096),
        *((f"OV-TQ-{number:02d}", 4096) for number in range(3, 13)),
        *((("OV-TQ-13", context)) for context in (512, 2048, 4096, 8192)),
        *((("OV-TQ-14", context)) for context in (512, 2048, 4096, 8192)),
        ("OV-TQ-15", 4096),
    }
    spec_entries: list[dict[str, Any]] = []
    discovered_spec_keys: set[tuple[str, int]] = set()
    for path in sorted(spec_root.glob("**/spec.json")):
        relative_path = _relative(repo_root, path)
        spec = _read_json(path)
        key = (spec.get("controlled_test_id"), spec.get("context"))
        if (
            spec.get("schema") != "official-openvino-wb04-worker-spec/v1"
            or not isinstance(key[0], str)
            or type(key[1]) is not int
            or key in discovered_spec_keys
        ):
            raise ValueError("campaign spec inventory contains an invalid entry")
        discovered_spec_keys.add(key)
        spec_entries.append(
            {
                "path": relative_path,
                "sha256": _sha256_file(path),
                "test_id": key[0],
                "context_tokens": key[1],
            }
        )
    if discovered_spec_keys != expected_spec_keys:
        raise ValueError("reviewed campaign-spec inventory has changed")

    conclusions = [
        {
            "test_id": "OV-01",
            "context_tokens": 1024,
            "status": "controlled-terminal-not-launched",
            "reason_code": "diagnostic-spec-absent",
            "matching_artifact_paths": [],
            "matching_spec_paths": [],
        },
        {
            "test_id": "OV-02",
            "context_tokens": 2048,
            "status": "controlled-terminal-not-launched",
            "reason_code": "fp16-artifact-and-spec-absent",
            "matching_artifact_paths": [],
            "matching_spec_paths": [],
        },
    ]
    payload: dict[str, Any] = {
        "schema": "official-openvino-wb04-artifact-spec-inventory/v1",
        "matrix_sha256": MATRIX_SHA256,
        "inventory_roots": inventory_roots,
        "artifact_entries": sorted(
            artifact_entries, key=lambda item: item["path"]
        ),
        "spec_entries": sorted(spec_entries, key=lambda item: item["path"]),
        "conclusions": sorted(
            conclusions,
            key=lambda item: (item["test_id"], item["context_tokens"]),
        ),
    }
    payload["inventory_sha256"] = _canonical_sha256(payload)
    return output_root / "artifact-spec-inventory.json", payload


def _validate_ov06_sources(repo_root: Path) -> list[dict[str, str]]:
    references: list[dict[str, str]] = []
    low_memory_flags: list[bool] = []
    for relative_path, expected_sha256 in OV06_ATTEMPTS:
        reference = _source_ref(repo_root, relative_path, expected_sha256)
        attempt = _read_json(repo_root / relative_path)
        if (
            attempt.get("schema") != "official-openvino-wb04-governed-run/v1"
            or attempt.get("role") != "pilot"
            or attempt.get("valid") is not False
            or attempt.get("timed_out") is not False
            or attempt.get("cleanup_process_count") != 0
        ):
            raise ValueError("OV-06 calibration source is not a clean pilot stop")
        low_memory_flags.append(attempt.get("low_memory_stop"))
        references.append(reference)
    if low_memory_flags != [False, True, True]:
        raise ValueError("OV-06 calibration sequence no longer matches review")
    return references


def _validate_quality_guards(repo_root: Path) -> list[dict[str, str]]:
    references: list[dict[str, str]] = []
    run_ids: set[str] = set()
    for (
        relative_path,
        expected_sha256,
        spec_relative_path,
        expected_spec_sha256,
    ) in QUALITY_GUARDS:
        guard_reference = _source_ref(
            repo_root, relative_path, expected_sha256
        )
        spec_reference = _source_ref(
            repo_root, spec_relative_path, expected_spec_sha256
        )
        guard_path = (repo_root / relative_path).resolve()
        spec_path = (repo_root / spec_relative_path).resolve()
        guard = _read_json(guard_path)
        observed = guard.get("observed_available_ram_bytes")
        job = guard.get("job_object")
        command = guard.get("command")
        bound_inputs = guard.get("bound_inputs")
        bound_input = bound_inputs[0] if isinstance(bound_inputs, list) and len(bound_inputs) == 1 else None
        bound_input_matches = (
            isinstance(bound_input, dict)
            and bound_input.get("name") == "quality_worker_spec"
            and bound_input.get("sha256") == expected_spec_sha256
            and isinstance(bound_input.get("path"), str)
            and _repository_local_evidence_path(repo_root, bound_input["path"])
            == spec_path
        )
        if (
            guard.get("schema") != "official-openvino-owned-process-guard/v1"
            or guard.get("valid") is not False
            or guard.get("low_memory_stop") is not True
            or guard.get("timed_out") is not False
            or guard.get("exit_code") == 0
            or guard.get("cleanup_process_count") != 0
            or guard.get("configured_minimum_available_ram_bytes")
            != 2048 * 1024 * 1024
            or guard.get("termination_reason") != "minimum_available_ram"
            or type(guard.get("memory_sample_count")) is not int
            or guard["memory_sample_count"] <= 0
            or not isinstance(observed, dict)
            or type(observed.get("minimum")) is not int
            or observed["minimum"] > 2048 * 1024 * 1024
            or not isinstance(job, dict)
            or job.get("setup_ok") is not True
            or job.get("query_ok") is not True
            or job.get("survivor_pids_after_cleanup") != []
            or not isinstance(guard.get("run_id"), str)
            or not bound_input_matches
            or not isinstance(command, list)
            or command.count("--spec") != 1
        ):
            raise ValueError("quality guard does not prove a clean RAM-floor stop")
        spec_index = command.index("--spec") + 1
        if (
            spec_index >= len(command)
            or not isinstance(command[spec_index], str)
            or _repository_local_evidence_path(repo_root, command[spec_index])
            != spec_path
            or spec_path != guard_path.parent / "worker-spec.json"
        ):
            raise ValueError(
                "quality guard does not bind its exact sibling worker spec"
            )
        run_ids.add(guard["run_id"])
        references.extend((guard_reference, spec_reference))
    if len(run_ids) != 2:
        raise ValueError("quality guard executions are not distinct")
    return references


def _expected_rejection_records(
    repo_root: Path,
    output_root: Path,
    generated: dict[Path, dict[str, Any]],
    runtime_cases: Mapping[tuple[str, int], Mapping[str, Any]],
) -> list[dict[str, Any]]:
    records: list[dict[str, Any]] = []
    scalar_keys = {
        ("OV-04", 4096),
        ("OV-05", 4096),
        ("OV-TQ-01", 4096),
        ("OV-TQ-02", 4096),
    }
    aggregates: dict[str, tuple[dict[str, str], dict[str, Any]]] = {}
    for kind, source in EXPECTED_REJECTION_AGGREGATES.items():
        reference = _source_ref(repo_root, source[0], source[1])
        aggregate = _read_json(repo_root / source[0])
        if (
            aggregate.get("cleanup_process_count") != 0
            or not isinstance(aggregate.get("probes"), list)
        ):
            raise ValueError(f"{kind} expected-rejection aggregate is invalid")
        aggregates[kind] = (reference, aggregate)
    for key in sorted(EXPECTED_REJECTION_KEYS):
        case = runtime_cases[key]
        kind = "scalar" if key in scalar_keys else "property"
        reference, aggregate = aggregates[kind]
        probes = [
            probe
            for probe in aggregate["probes"]
            if isinstance(probe, dict)
            and probe.get("controlled_test_id") == key[0]
        ]
        if len(probes) != 1:
            raise ValueError(f"expected rejection does not bind exactly one probe: {key}")
        probe = probes[0]
        unsigned_probe = {
            field: value
            for field, value in probe.items()
            if field != "probe_sha256"
        }
        if (
            probe.get("probe_sha256")
            != _evidence_canonical_sha256(unsigned_probe)
            or probe.get("status") != "passed: expected-rejection"
            or probe.get("expected_outcome") != "expected-rejection"
            or probe.get("cleanup_process_count") != 0
            or case.get("expected_outcome") != "expected-rejection"
        ):
            raise ValueError(f"expected-rejection probe is invalid: {key}")
        generation_not_launched = kind == "property"
        if generation_not_launched:
            if (
                probe.get("generation_not_launched") is not True
                or aggregate.get("generation_not_launched") is not True
            ):
                raise ValueError(f"property rejection launched generation: {key}")
        elif (
            probe.get("generation_launched") is not True
            or probe.get("numeric_generation_metrics_accepted") is not False
            or probe.get("metric_outcome")
            != "not-produced-by-expected-rejection"
        ):
            raise ValueError(f"scalar semantic rejection is invalid: {key}")
        classification = {
            "schema": (
                "official-openvino-wb04-expected-rejection-classification/v1"
            ),
            "test_id": key[0],
            "context_tokens": key[1],
            "status": "passed: expected-rejection",
            "metric_outcome": "not-produced-by-expected-rejection",
            "generation_not_launched": generation_not_launched,
            "cleanup_process_count": 0,
            "source_aggregate_path": reference["path"],
            "source_aggregate_sha256": reference["sha256"],
            "probe_sha256": probe["probe_sha256"],
        }
        path = (
            output_root
            / "expected-rejections"
            / f"{key[0]}-context-{key[1]}.json"
        )
        generated[path] = classification
        evidence = _generated_ref(repo_root, path, classification)
        if kind == "scalar":
            reason = (
                "Bounded STANDARD generation completed, but concrete K/V "
                "state was f32/f32 rather than the requested scalar cache "
                "precision; no numeric benchmark result is admissible."
            )
        else:
            reason = (
                "The frozen unsupported property boundary rejected before "
                "generation exactly as declared by the matrix."
            )
        records.append(
            {
                "test_id": key[0],
                "context_tokens": key[1],
                "accepted": False,
                "status": "passed: expected-rejection",
                "reason": reason,
                "sample_count": 0,
                "cleanup_process_count": 0,
                "metric_outcome": "not-produced-by-expected-rejection",
                "evidence_path": evidence["path"],
                "evidence_sha256": evidence["sha256"],
                "generation_not_launched": generation_not_launched,
            }
        )
    return records


def build_release_evidence(
    *,
    repo_root: Path,
    output_root: Path,
    release_input_path: Path,
    static_draft_path: Path,
) -> dict[str, Any]:
    """Validate sources, then atomically emit the complete WB-04 release set."""

    repo_root = Path(repo_root).resolve()
    output_root = _within_repo(repo_root, Path(output_root), "output root")
    release_input_path = _within_repo(
        repo_root, Path(release_input_path), "release input"
    )
    static_draft_path = _within_repo(
        repo_root, Path(static_draft_path), "static draft"
    )
    matrix_ref = _source_ref(repo_root, MATRIX_RELATIVE_PATH, MATRIX_SHA256)
    matrix = _read_json(repo_root / MATRIX_RELATIVE_PATH)
    runtime_cases = _runtime_cases(matrix)
    quality_keys = _quality_keys(runtime_cases)
    mapped_runtime = (
        set(MEASURED_SOURCES)
        | EXPECTED_REJECTION_KEYS
        | DIRECT_TERMINAL_KEYS
        | {RESOURCE_ANCHOR_KEY}
        | RESOURCE_ENVELOPE_KEYS
    )
    if mapped_runtime != set(runtime_cases):
        raise ValueError("release mapping does not exactly cover 36 runtime rows")
    if any(
        runtime_cases[key].get("expected_outcome") != "expected-rejection"
        for key in EXPECTED_REJECTION_KEYS
    ):
        raise ValueError("expected-rejection mapping contradicts the matrix")

    generated: dict[Path, dict[str, Any]] = {}
    measured_refs: dict[tuple[str, int], dict[str, str]] = {}
    measured_payloads: dict[tuple[str, int], dict[str, Any]] = {}
    for key, source in MEASURED_SOURCES.items():
        reference, summary = _validate_measurement(repo_root, key, source)
        measured_refs[key] = reference
        measured_payloads[key] = summary

    inventory_path, inventory = _build_inventory(repo_root, output_root)
    generated[inventory_path] = inventory
    inventory_ref = _generated_ref(repo_root, inventory_path, inventory)

    runtime_records: dict[tuple[str, int], dict[str, Any]] = {}
    runtime_classification_refs: dict[tuple[str, int], dict[str, str]] = {}

    def add_terminal(
        key: tuple[str, int],
        *,
        status: str,
        reason: str,
        sources: Sequence[Mapping[str, str]],
    ) -> dict[str, Any]:
        classification = _terminal_payload(
            key, status=status, reason=reason, source_evidence=sources
        )
        path = output_root / "runtime" / f"{key[0]}-context-{key[1]}.json"
        generated[path] = classification
        reference = _generated_ref(repo_root, path, classification)
        record = _terminal_record(
            key, status=status, reason=reason, evidence=reference
        )
        runtime_records[key] = record
        runtime_classification_refs[key] = reference
        return record

    add_terminal(
        ("OV-01", 1024),
        status="controlled-terminal-not-launched",
        reason=(
            "The sealed current inventory contains no OV-01 diagnostic "
            "campaign spec, so the row remains a controlled terminal and "
            "cannot supply performance metrics."
        ),
        sources=[inventory_ref],
    )
    add_terminal(
        ("OV-02", 2048),
        status="controlled-terminal-not-launched",
        reason=(
            "The sealed current inventory contains neither an OV-02 campaign "
            "spec nor a validated Granite 3B FP16 artifact, so generation "
            "was not launched."
        ),
        sources=[inventory_ref],
    )
    add_terminal(
        ("OV-03", 4096),
        status="host-resource-blocked",
        reason=(
            "Three governed U8 CPU STANDARD pilots crossed the fixed "
            "2,048 MiB available-RAM floor with cleanup_process_count=0; "
            "no warm-up or measured sample was launched."
        ),
        sources=_validate_governed_low_memory(
            repo_root, OV03_ATTEMPTS, label="OV-03"
        ),
    )
    add_terminal(
        ("OV-06", 4096),
        status="device-resource-blocked",
        reason=(
            "GPU calibration reached compilation after property correction, "
            "then two governed attempts crossed the fixed 2,048 MiB RAM "
            "floor; no formal GPU sample was accepted."
        ),
        sources=_validate_ov06_sources(repo_root),
    )

    host_ref = _source_ref(repo_root, HOST_MANIFEST[0], HOST_MANIFEST[1])
    host = _read_json(repo_root / HOST_MANIFEST[0])
    installed_ram = (
        host.get("memory", {}).get("installed_ram_bytes")
        if isinstance(host.get("memory"), dict)
        else None
    )
    if (
        type(installed_ram) is not int
        or installed_ram >= 24 * 1024 * 1024 * 1024
        or host.get("review_required") != []
    ):
        raise ValueError("host manifest does not prove the suitable-host boundary")
    for key in sorted(DIRECT_TERMINAL_KEYS - {
        ("OV-01", 1024),
        ("OV-02", 2048),
        ("OV-03", 4096),
        ("OV-06", 4096),
    }):
        case = runtime_cases[key]
        if case.get("suitable_host_required") is not True:
            raise ValueError(f"suitable-host mapping contradicts matrix: {key}")
        add_terminal(
            key,
            status="suitable-host-required-not-launched",
            reason=(
                "The frozen matrix requires at least 24 GiB available RAM "
                f"for {key[0]}, while the reviewed host has "
                f"{installed_ram} installed physical bytes; local generation "
                "was not launched."
            ),
            sources=[host_ref, matrix_ref],
        )

    anchor_sources = _validate_governed_low_memory(
        repo_root, TQ13_2048_ATTEMPTS, label="OV-TQ-13/context-2048"
    )
    anchor_attempts = [
        source
        for source in anchor_sources
        if source["path"].endswith("/attempt.json")
    ]
    anchor_record = add_terminal(
        RESOURCE_ANCHOR_KEY,
        status="host-resource-blocked",
        reason=(
            "Three governed OV-TQ-13 context-2048 pilots crossed the fixed "
            "2,048 MiB available-RAM floor with zero cleanup survivors."
        ),
        sources=anchor_sources,
    )
    anchor_classification_ref = runtime_classification_refs[RESOURCE_ANCHOR_KEY]
    for key in sorted(RESOURCE_ENVELOPE_KEYS):
        add_terminal(
            key,
            status="host-resource-blocked-not-launched",
            reason=(
                "This explicitly named U8 CPU row has cache-bit×context "
                "demand no smaller than the governed OV-TQ-13/context-2048 "
                "RAM-floor blocker, so it was not launched locally."
            ),
            sources=[anchor_classification_ref],
        )

    accepted_key = ("OV-TQ-14", 2048)
    accepted_minimum = measured_payloads[accepted_key].get(
        "available_ram_min_mb", {}
    ).get("min")
    if not isinstance(accepted_minimum, (int, float)) or accepted_minimum <= 2048:
        raise ValueError("resource-envelope accepted anchor is invalid")
    decision: dict[str, Any] = {
        "schema": "official-openvino-wb04-resource-envelope-decision/v1",
        "status": "accepted",
        "matrix_sha256": MATRIX_SHA256,
        "minimum_available_ram_mib": 2048,
        "accepted_anchor": {
            "test_id": accepted_key[0],
            "context_tokens": accepted_key[1],
            "minimum_available_ram_mib": accepted_minimum,
            "measurement_summary_path": measured_refs[accepted_key]["path"],
            "measurement_summary_sha256": measured_refs[accepted_key]["sha256"],
        },
        "blocked_anchor": {
            "test_id": RESOURCE_ANCHOR_KEY[0],
            "context_tokens": RESOURCE_ANCHOR_KEY[1],
            "terminal_record": anchor_record,
            "attempt_count": 3,
            "attempts": anchor_attempts,
        },
        "classified_rows": [
            runtime_records[key] for key in sorted(RESOURCE_ENVELOPE_KEYS)
        ],
        "reason": (
            "The accepted OV-TQ-14/context-2048 lower-demand row stayed "
            "above the fixed floor, while three comparable "
            "OV-TQ-13/context-2048 pilots crossed it; classification is "
            "limited to the 15 explicitly named equal-or-higher-demand "
            "U8 CPU rows."
        ),
    }
    decision["decision_sha256"] = _canonical_sha256(decision)
    decision_path = output_root / "runtime-resource-envelope-decision.json"
    generated[decision_path] = decision

    expected_records = _expected_rejection_records(
        repo_root, output_root, generated, runtime_cases
    )

    quality_records: list[dict[str, Any]] = []
    direct_quality_key = ("OV-TQ-14", 512)
    direct_quality_reason = (
        "Two governed quality launches for measured OV-TQ-14/context-512 "
        "crossed the fixed 2,048 MiB available-RAM floor before any complete "
        "P1-P6 capture or score was produced."
    )
    direct_quality_payload = _quality_payload(
        direct_quality_key,
        status=QUALITY_RESOURCE_BLOCKED_STATUS,
        reason=direct_quality_reason,
        score_outcome=QUALITY_RESOURCE_BLOCKED_OUTCOME,
        source_evidence=[
            measured_refs[direct_quality_key],
            *_validate_quality_guards(repo_root),
        ],
    )
    direct_quality_path = (
        output_root
        / "quality"
        / f"{direct_quality_key[0]}-context-{direct_quality_key[1]}.json"
    )
    generated[direct_quality_path] = direct_quality_payload
    direct_quality_ref = _generated_ref(
        repo_root, direct_quality_path, direct_quality_payload
    )
    quality_records.append(
        _quality_record(
            direct_quality_key,
            status=QUALITY_RESOURCE_BLOCKED_STATUS,
            reason=direct_quality_reason,
            score_outcome=QUALITY_RESOURCE_BLOCKED_OUTCOME,
            evidence=direct_quality_ref,
        )
    )

    for key in (("OV-TQ-13", 512), ("OV-TQ-14", 2048)):
        reason = (
            "Quality was not launched because this measured U8 CPU row has "
            "cache-bit×context demand no smaller than the governed "
            "OV-TQ-14/context-512 quality RAM-floor blocker; no P1-P6 score "
            "exists."
        )
        payload = _quality_payload(
            key,
            status=QUALITY_RESOURCE_ENVELOPE_STATUS,
            reason=reason,
            score_outcome=QUALITY_RESOURCE_ENVELOPE_OUTCOME,
            source_evidence=[measured_refs[key], direct_quality_ref],
        )
        path = output_root / "quality" / f"{key[0]}-context-{key[1]}.json"
        generated[path] = payload
        quality_records.append(
            _quality_record(
                key,
                status=QUALITY_RESOURCE_ENVELOPE_STATUS,
                reason=reason,
                score_outcome=QUALITY_RESOURCE_ENVELOPE_OUTCOME,
                evidence=_generated_ref(repo_root, path, payload),
            )
        )

    expected_quality_keys = EXPECTED_REJECTION_KEYS & quality_keys
    terminal_quality_keys = quality_keys - expected_quality_keys - set(MEASURED_SOURCES)
    if len(expected_quality_keys) != 5 or len(terminal_quality_keys) != 25:
        raise ValueError("quality mapping does not match the 33-row scope")
    for key in sorted(terminal_quality_keys):
        runtime_record = runtime_records[key]
        status = runtime_record["status"]
        score_outcome = runtime_record["metric_outcome"]
        reason = (
            "No quality campaign was launched because the hash-bound runtime "
            f"row ended as {status}; no P1-P6 responses or score were "
            "produced."
        )
        payload = _quality_payload(
            key,
            status=status,
            reason=reason,
            score_outcome=score_outcome,
            source_evidence=[runtime_classification_refs[key]],
        )
        path = output_root / "quality" / f"{key[0]}-context-{key[1]}.json"
        generated[path] = payload
        quality_records.append(
            _quality_record(
                key,
                status=status,
                reason=reason,
                score_outcome=score_outcome,
                evidence=_generated_ref(repo_root, path, payload),
            )
        )
    if len(quality_records) != 28:
        raise ValueError("release must contain exactly 28 quality terminals")

    if not static_draft_path.is_file():
        raise ValueError("static-section draft is missing")
    draft_text = static_draft_path.read_text(encoding="utf-8-sig")
    section_bodies = parse_static_sections(draft_text)
    draft_ref = {
        "path": _relative(repo_root, static_draft_path),
        "sha256": _sha256_file(static_draft_path),
    }
    static_records: dict[str, Any] = {}
    for number in STATIC_SECTION_NUMBERS:
        body = section_bodies[number]
        payload = {
            "schema": STATIC_SECTION_EVIDENCE_SCHEMA,
            "section_number": number,
            "body": body,
            "source_evidence": [draft_ref],
        }
        path = output_root / "static-sections" / f"section-{number:02d}.json"
        generated[path] = payload
        static_records[str(number)] = {
            "body": body,
            "evidence": _generated_ref(repo_root, path, payload),
        }

    direct_records = [
        runtime_records[key] for key in sorted(DIRECT_TERMINAL_KEYS)
    ]
    release = {
        "schema": RELEASE_INPUT_SCHEMA,
        "campaign_date": CAMPAIGN_DATE,
        "workbook_version": WORKBOOK_VERSION,
        "revision_id": REVISION_ID,
        "matrix_path": matrix_ref["path"],
        "matrix_sha256": matrix_ref["sha256"],
        "selected_measurement_summaries": [
            measured_refs[key]["path"] for key in MEASURED_SOURCES
        ],
        "terminal_records": direct_records,
        "resource_envelope_decisions": [decision],
        "expected_rejection_records": expected_records,
        "quality_records": [],
        "quality_terminal_records": sorted(
            quality_records,
            key=lambda item: (item["test_id"], item["context_tokens"]),
        ),
        "static_section_bodies": static_records,
    }
    release_bytes = _json_bytes(release)

    # Everything above is validated and materialised before the first write.
    # The release input is replaced last, so readers never observe a manifest
    # that points at artifacts from an incomplete build.
    for path in sorted(generated, key=lambda item: item.as_posix()):
        _atomic_write(path, _json_bytes(generated[path]))
    _atomic_write(release_input_path, release_bytes)
    return {
        "generated_file_count": len(generated) + 1,
        "runtime_row_count": len(mapped_runtime),
        "quality_row_count": len(quality_keys),
        "release_input_sha256": _sha256_bytes(release_bytes),
    }


def _parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    repository = Path(__file__).resolve().parents[3]
    campaign = (
        repository
        / "experiments/raw-results/openvino-turboquant/2026-07-30"
    )
    parser = argparse.ArgumentParser(
        description=(
            "Build deterministic, fail-closed WB-04 release evidence without "
            "modifying the workbook, registers, or DOCX."
        )
    )
    parser.add_argument("--repo-root", type=Path, default=repository)
    parser.add_argument(
        "--output-root", type=Path, default=campaign / "release-evidence"
    )
    parser.add_argument(
        "--release-input",
        type=Path,
        default=campaign / "reconciliation-input.json",
    )
    parser.add_argument(
        "--static-draft",
        type=Path,
        default=(
            repository
            / ".superpowers/sdd/2026-07-19-openvino-turboquant-recovery/"
            "wb04-static-sections-draft.md"
        ),
    )
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    args = _parse_args(argv)
    report = build_release_evidence(
        repo_root=args.repo_root,
        output_root=args.output_root,
        release_input_path=args.release_input,
        static_draft_path=args.static_draft,
    )
    print(json.dumps(report, sort_keys=True))
    return 0


def parse_static_sections(text: str) -> dict[int, str]:
    """Return the controlled static-section bodies from a completed draft."""

    matches = list(_SECTION_HEADING.finditer(text))
    sections: dict[int, str] = {}
    for index, match in enumerate(matches):
        number = int(match.group(1))
        if number not in STATIC_SECTION_NUMBERS:
            continue
        if number in sections:
            raise ValueError(f"duplicate section {number}")
        end = matches[index + 1].start() if index + 1 < len(matches) else len(text)
        body = text[match.end() : end].strip()
        if not body:
            raise ValueError(f"section {number} body is empty")
        sections[number] = body
    if set(sections) != set(STATIC_SECTION_NUMBERS):
        raise ValueError("static draft must provide sections 1-4")
    return sections


if __name__ == "__main__":
    raise SystemExit(main())
