from __future__ import annotations

import csv
import hashlib
import json
from pathlib import Path

from scripts.testing.tools.cleanup_semantics import build_semantic_snapshot


def _csv(path: Path, fieldnames: tuple[str, ...], rows: tuple[dict[str, object], ...]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)


def test_semantic_snapshot_is_deterministic_and_records_scientific_values(
    tmp_path: Path,
):
    root = tmp_path
    route = root / "docs/testing/final-results/01-route"
    evidence = root / "experiments/raw-results/value.txt"
    evidence.parent.mkdir(parents=True)
    evidence.write_text("observed\n", encoding="utf-8", newline="\n")
    digest = hashlib.sha256(evidence.read_bytes()).hexdigest()

    _csv(
        route / "results/attempts.csv",
        ("route_id", "campaign_id", "test_case_id", "attempt_id", "status"),
        (
            {
                "route_id": "route",
                "campaign_id": "campaign",
                "test_case_id": "CASE-1",
                "attempt_id": "ATTEMPT-1",
                "status": "passed",
            },
            {
                "route_id": "route",
                "campaign_id": "campaign",
                "test_case_id": "CASE-2",
                "attempt_id": "ATTEMPT-2",
                "status": "blocked",
            },
        ),
    )
    _csv(
        route / "results/measurements.csv",
        ("attempt_id", "metric", "value", "unit"),
        ({"attempt_id": "ATTEMPT-1", "metric": "tps", "value": "4.25", "unit": "token/s"},),
    )
    _csv(
        route / "quality/scores.csv",
        ("test_case_id", "score", "method"),
        ({"test_case_id": "CASE-1", "score": "8.75", "method": "frozen-v1"},),
    )
    _csv(
        route / "evidence/evidence-index.csv",
        ("evidence_id", "relative_path", "sha256", "size_bytes"),
        ({"evidence_id": "EV-1", "relative_path": "experiments/raw-results/value.txt", "sha256": digest, "size_bytes": evidence.stat().st_size},),
    )
    comparison = root / "docs/testing/final-results/06-cross-route-comparison"
    _csv(
        comparison / "results/comparability-matrix.csv",
        ("left_route_id", "right_route_id", "metric", "classification"),
        ({"left_route_id": "route-a", "right_route_id": "route-b", "metric": "quality", "classification": "descriptive_only"},),
    )

    first = build_semantic_snapshot(root)
    second = build_semantic_snapshot(root)
    assert first == second
    assert first["outcomes"]["total"] == 2
    assert first["outcomes"]["status_counts"] == {"blocked": 1, "passed": 1}
    assert first["evidence"]["relationship_count"] == 1
    assert first["evidence"]["unique_path_count"] == 1
    assert first["scientific_tables"]["measurements"][0]["value"] == "4.25"
    assert first["scientific_tables"]["quality"][0]["score"] == "8.75"
    assert first["comparability"]["rows"][0]["classification"] == "descriptive_only"
    payload = dict(first)
    snapshot_sha256 = payload.pop("snapshot_sha256")
    canonical = json.dumps(payload, sort_keys=True, separators=(",", ":")) + "\n"
    assert snapshot_sha256 == hashlib.sha256(canonical.encode("utf-8")).hexdigest()
