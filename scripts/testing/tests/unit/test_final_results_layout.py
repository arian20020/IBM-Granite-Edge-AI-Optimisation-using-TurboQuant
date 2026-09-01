from __future__ import annotations

import json
import sys
from pathlib import Path

import pytest


sys.path.insert(0, str(Path(__file__).resolve().parents[4]))

from scripts.testing.reporting.csvio import write_json
from scripts.testing.reporting.layout import (
    consolidate_validation,
    plan_route_migration,
    render_validation_markdown,
    validate_layout,
)


def _write_text(path: Path, text: str) -> Path:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8", newline="\n")
    return path


def _write_bytes(path: Path, payload: bytes) -> Path:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(payload)
    return path


def _build_old_route(tmp_path: Path) -> Path:
    route = tmp_path / "01-upstream-llama-cpp"
    write_json(route / "route-manifest.json", {"route_id": "upstream-llama-cpp"})
    _write_text(route / "README.md", "# Route\n")
    _write_text(
        route / "workbook/source/upstream-llama-cpp-final-report.md",
        "# Report\n",
    )
    _write_bytes(
        route / "workbook/generated/upstream-llama-cpp-final-report.docx",
        b"DOCX",
    )
    _write_bytes(
        route / "workbook/generated/upstream-llama-cpp-final-report.pdf",
        b"%PDF-1.7\n",
    )
    _write_bytes(
        route / "workbook/generated/upstream-llama-cpp-portable-results.xlsx",
        b"XLSX",
    )
    _write_text(route / "results/attempts.csv", "attempt_id\nattempt-1\n")
    _write_text(route / "results/measurements.csv", "measurement_id\nmeasure-1\n")
    _write_text(route / "results/summary-results.csv", "summary_id\nsummary-1\n")
    _write_text(
        route / "results/availability-matrix.csv",
        "test_case_id,status\ncase-1,passed\n",
    )
    _write_text(route / "quality/scores.csv", "quality_id\nquality-1\n")
    _write_text(
        route / "quality/prompt-suite.csv",
        "prompt_id\nprompt-1\n",
    )
    _write_text(route / "failures/failure-register.csv", "failure_id\n")
    _write_text(route / "protocol/deviations.csv", "deviation_id\n")
    _write_text(route / "protocol/test-plan.md", "# Plan\n")
    _write_text(route / "evidence/evidence-index.csv", "evidence_id\nproof-1\n")
    _write_text(route / "evidence/claim-evidence-map.csv", "claim_id\nclaim-1\n")
    _write_text(
        route / "evidence/manifest-sha256.txt",
        "abc123  evidence/evidence-index.csv\n",
    )
    _write_text(route / "reproduction/README.md", "# Reproduce\n")
    _write_text(route / "reproduction/commands.md", "python -m scripts.testing.cli.run_llama\n")
    _write_text(route / "reproduction/dependencies.md", "- python\n")
    _write_text(route / "system/hardware.json", '{"device":"cpu"}\n')
    _write_text(route / "system/software.json", '{"runtime":"python"}\n')
    _write_text(route / "system/repository.json", '{"revision":"abc"}\n')
    _write_text(route / "system/environment.txt", "PATH=/tmp\n")
    write_json(
        route / "validation/coverage-validation.json",
        {"valid": True, "limitations": ["coverage caveat"]},
    )
    write_json(
        route / "validation/data-validation.json",
        {"valid": False, "findings": ["data mismatch"], "limitations": ["manual review"]},
    )
    write_json(
        route / "validation/integrity-validation.json",
        {"valid": True, "findings": []},
    )
    write_json(
        route / "validation/relationship-validation.json",
        {"valid": True},
    )
    write_json(
        route / "validation/visual-validation.json",
        {"valid": True},
    )
    write_json(
        route / "validation/workbook-parity.json",
        {"matches": True},
    )
    _write_text(
        route / "validation/validation-report.md",
        "Overall result: **Failed**\n",
    )
    return route


def _materialize_new_layout(tmp_path: Path, source_route: Path) -> Path:
    destination = tmp_path / "migrated"
    destination.mkdir(parents=True, exist_ok=True)
    for move in plan_route_migration(source_route):
        payload = (source_route / move.old_path).read_bytes()
        target = destination / move.new_path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(payload)
    validation = consolidate_validation(source_route)
    write_json(destination / "validation/validation.json", validation)
    _write_text(
        destination / "validation/validation.md",
        render_validation_markdown(validation),
    )
    return destination


def test_route_migration_plan_is_deterministic_collision_free_and_six_part(tmp_path):
    route = _build_old_route(tmp_path)

    moves = plan_route_migration(route)

    assert moves == tuple(sorted(moves, key=lambda move: move.old_path.as_posix()))
    assert len({move.new_path for move in moves}) == len(moves)
    assert {move.new_path.parts[0] for move in moves} == {
        "README.md",
        "reports",
        "data",
        "evidence",
        "reproduction",
    }
    mapping = {
        move.old_path.as_posix(): move.new_path.as_posix()
        for move in moves
    }
    assert mapping["route-manifest.json"] == "data/route.json"
    assert (
        mapping["workbook/source/upstream-llama-cpp-final-report.md"]
        == "reports/upstream-llama-cpp-report.md"
    )
    assert (
        mapping["workbook/generated/upstream-llama-cpp-final-report.docx"]
        == "reports/upstream-llama-cpp-report.docx"
    )
    assert (
        mapping["workbook/generated/upstream-llama-cpp-final-report.pdf"]
        == "reports/upstream-llama-cpp-report.pdf"
    )
    assert (
        mapping["workbook/generated/upstream-llama-cpp-portable-results.xlsx"]
        == "reports/upstream-llama-cpp-results.xlsx"
    )
    assert mapping["results/attempts.csv"] == "data/attempts.csv"
    assert mapping["results/measurements.csv"] == "data/measurements.csv"
    assert mapping["results/summary-results.csv"] == "data/summaries.csv"
    assert mapping["quality/scores.csv"] == "data/quality.csv"
    assert mapping["failures/failure-register.csv"] == "data/failures.csv"
    assert mapping["protocol/deviations.csv"] == "data/deviations.csv"
    assert mapping["system/hardware.json"] == "reproduction/system/hardware.json"


def test_validation_consolidation_preserves_receipts_and_computes_overall_status(tmp_path):
    route = _build_old_route(tmp_path)

    consolidated = consolidate_validation(route)

    assert consolidated["route_id"] == "upstream-llama-cpp"
    assert consolidated["valid"] is False
    assert tuple(consolidated["checks"]) == (
        "coverage",
        "data",
        "integrity",
        "relationship",
        "visual",
        "workbook_parity",
    )
    assert consolidated["checks"]["data"]["valid"] is False
    assert any(item["message"] == "data mismatch" for item in consolidated["findings"])
    assert any(item["message"] == "manual review" for item in consolidated["limitations"])


def test_validation_consolidation_accepts_status_only_receipts(tmp_path):
    route = _build_old_route(tmp_path)
    write_json(
        route / "validation/integrity-validation.json",
        {"status": "valid", "manifest": "evidence/manifest-sha256.txt"},
    )

    consolidated = consolidate_validation(route)

    assert consolidated["checks"]["integrity"]["valid"] is True


def test_validate_layout_accepts_materialized_new_route_and_rejects_stale_paths(tmp_path):
    route = _build_old_route(tmp_path)
    migrated = _materialize_new_layout(tmp_path, route)

    assert validate_layout(migrated) == ()

    write_json(
        migrated / "data/route.json",
        {"route_id": "upstream-llama-cpp", "legacy_path": "workbook/source/old.md"},
    )
    issues = validate_layout(migrated)

    assert any("stale_removed_path" in issue for issue in issues)


def test_route_migration_rejects_duplicate_destinations(tmp_path):
    route = _build_old_route(tmp_path)
    _write_text(route / "workbook/source/rogue-final-report.md", "# Rogue\n")

    with pytest.raises(ValueError, match="duplicate destination"):
        plan_route_migration(route)


def test_route_migration_rejects_missing_required_source(tmp_path):
    route = _build_old_route(tmp_path)
    (route / "results/measurements.csv").unlink()

    with pytest.raises(ValueError, match="missing required source"):
        plan_route_migration(route)


def test_route_migration_rejects_unknown_role(tmp_path):
    route = _build_old_route(tmp_path)
    _write_text(route / "mystery/rogue.bin", "rogue\n")

    with pytest.raises(ValueError, match="unknown role"):
        plan_route_migration(route)


def test_validate_layout_rejects_manifest_self_inclusion(tmp_path):
    route = _build_old_route(tmp_path)
    migrated = _materialize_new_layout(tmp_path, route)
    _write_text(
        migrated / "evidence/manifest-sha256.txt",
        "abc123  evidence/manifest-sha256.txt\n",
    )

    issues = validate_layout(migrated)

    assert any("manifest_self_inclusion" in issue for issue in issues)


def test_validation_consolidation_rejects_report_disagreement(tmp_path):
    route = _build_old_route(tmp_path)
    write_json(route / "validation/data-validation.json", {"valid": True})
    _write_text(route / "validation/validation-report.md", "Overall result: **Failed**\n")

    with pytest.raises(ValueError, match="validation disagreement"):
        consolidate_validation(route)
