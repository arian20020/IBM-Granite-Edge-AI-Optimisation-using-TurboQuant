from __future__ import annotations

import csv
import hashlib
import io
import json
import re
import subprocess
import tarfile
from collections import defaultdict
from pathlib import Path

import fitz
import openpyxl

from scripts.testing.reporting.parity import compare_markdown_docx
from scripts.testing.reporting.validate import GATE_ORDER
from scripts.testing.tools import cleanup_semantics


ROOT = Path(__file__).resolve().parents[4]
BASELINE_PATH = ROOT / "docs/testing/cleanup/baseline-semantic-snapshot.json"
PATH_MIGRATION = ROOT / "docs/testing/cleanup/PATH-MIGRATION.csv"
BASELINE = json.loads(BASELINE_PATH.read_text(encoding="utf-8"))
FINAL_SNAPSHOT = ROOT / "docs/testing/cleanup/final-semantic-snapshot.json"
CLEAN_ARCHIVE_RECEIPT = ROOT / "docs/testing/cleanup/clean-archive-validation.json"
IMPLEMENTATION_REMOVAL_RECEIPT = (
    ROOT / "docs/testing/cleanup/implementation-root-removal-receipt.json"
)
FINAL_VALIDATION = ROOT / "docs/testing/final-results/validation/validation.json"
RELEASE_ROOT = ROOT / "docs/testing/final-results"
PORTABLE_WORKBOOKS = (
    RELEASE_ROOT
    / "04-openvino-experimental-fork/reports/openvino-experimental-fork-results.xlsx",
    RELEASE_ROOT
    / "05-openvino-official-upstream/reports/openvino-official-upstream-results.xlsx",
)
WINDOWS_ABSOLUTE = re.compile(r"^[A-Za-z]:[\\/]")
FROZEN_TEXT_BYTES = {
    "docs/testing/cleanup/archive-plan.csv": (
        679_690,
        "9f35b438a11b36d64e959e062fd4791944bfbb2d7105f3c3fad7f393bbe42ef0",
    ),
    "docs/testing/cleanup/file-inventory.csv": (
        2_087_652,
        "dea150021b7b0a482966bba6694bb52b8ca3650d0c7561c80108ccd276535766",
    ),
    "docs/testing/Quality-Evaluation-Register.csv": (
        93_992,
        "4d8e327caf025090b61c917152ba3f656209376e2365b11c40eeaf0c3236797c",
    ),
    "experiments/granite_turboquant_intel/prompts/rendered-v2/P1.txt": (
        307,
        "16adbafc129f513b5e4ee5f6ad85afafb12dc74a7bb8db8ce27e33186beecc77",
    ),
    "experiments/granite_turboquant_intel/prompts/fixtures/P5-compact-context-v2.txt": (
        1_611,
        "ae3290b37cc1126f48301dc8f722775eeb279a90caabd3ea96622af7287e68a3",
    ),
    "experiments/manifests/official-openvino/retest-matrix.json": (
        44_488,
        "7db2636b403d285aa3886c9f23560a9e48bd43645e9aca35e0d1fc4f16eaea42",
    ),
    "scripts/testing/tests/unit/test_cleanup_semantics.py": (
        3_391,
        "b9fd5d478267f0e77d8e3c871daf792e0ab4346ffa0654531ee2da86d4940179",
    ),
}
FROZEN_PROMPT_DIRECTORIES = (
    "experiments/granite_turboquant_intel/prompts/fixtures",
    "experiments/granite_turboquant_intel/prompts/rendered",
    "experiments/granite_turboquant_intel/prompts/rendered-v2",
)


def _rows(path: Path) -> list[dict[str, str]]:
    with path.open(encoding="utf-8-sig", newline="") as handle:
        return [dict(row) for row in csv.DictReader(handle)]


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _canonical_hash(payload: dict[str, object], excluded: str) -> str:
    value = dict(payload)
    value.pop(excluded)
    encoded = (json.dumps(value, sort_keys=True, separators=(",", ":")) + "\n").encode(
        "utf-8"
    )
    return hashlib.sha256(encoded).hexdigest()


def test_implementation_root_removal_receipt_covers_exact_archived_raw_set() -> None:
    inventory_rows = sorted(
        (
            row
            for row in _rows(ROOT / "docs/testing/cleanup/file-inventory.csv")
            if row["tracked_status"] == "tracked"
            and row["action"] == "archive_external"
            and row["path"].startswith("experiments/raw-results/")
        ),
        key=lambda row: row["path"],
    )
    assert len(inventory_rows) == 693
    assert sum(int(row["size_bytes"]) for row in inventory_rows) == 125_662

    receipt = json.loads(IMPLEMENTATION_REMOVAL_RECEIPT.read_text(encoding="utf-8"))
    assert receipt["schema"] == "testing-cleanup-implementation-root-removal/v1"
    assert receipt["valid"] is True
    assert receipt["input_commit"] == (
        "9678e9c175dba87e37711424004598f2e528f68a"
    )
    assert receipt["selection"] == {
        "action": "archive_external",
        "path_prefix": "experiments/raw-results/",
        "tracked_status": "tracked",
        "path_count": 693,
        "size_bytes": 125_662,
        "path_list_sha256": (
            "d41e443798a39c8bf5cdbf17279fe3641237c3f410bcb2c1dfe8b32b3a891e0c"
        ),
        "breakdown": {
            "atomicbot-turboquant": 517,
            "openvino-turboquant": 173,
            "openvino-official-upstream": 2,
            "raw-root-gitkeep": 1,
        },
    }
    assert receipt["verification"] == {
        "inventory_worktree_external_exact_count": 693,
        "parent_git_blob_exact_count": 565,
        "parent_git_blob_eol_normalized_count": 128,
        "parent_git_blob_other_mismatch_count": 0,
        "published_citation_intersection_count": 0,
        "retained_source_intersection_count": 0,
        "retained_destination_intersection_count": 0,
        "path_migration_intersection_count": 0,
        "canonical_retained_path_count": 1400,
        "canonical_retained_missing_count": 0,
        "canonical_retained_hash_mismatch_count": 0,
        "removed_path_count": 693,
        "remaining_selected_path_count": 0,
    }

    expected = {
        row["path"]: (int(row["size_bytes"]), row["sha256"])
        for row in inventory_rows
    }
    entries = receipt["entries"]
    assert len(entries) == 693
    assert [entry["path"] for entry in entries] == sorted(expected)
    assert {
        entry["path"]: (entry["size_bytes"], entry["sha256"])
        for entry in entries
    } == expected
    assert sum(entry["git_normalized_blob"] for entry in entries) == 128
    assert all(entry["state"] == "removed_from_implementation_root" for entry in entries)
    assert all(entry["inventory_worktree_external_exact"] is True for entry in entries)
    assert all(
        entry["parent_git_blob_relation"]
        == (
            "eol_only_git_normalized"
            if entry["git_normalized_blob"]
            else "byte_exact"
        )
        for entry in entries
    )
    assert all(not (ROOT / entry["path"]).exists() for entry in entries)
    tracked_raw = set(
        subprocess.check_output(
            ["git", "ls-files", "experiments/raw-results"],
            cwd=ROOT,
            text=True,
        ).splitlines()
    )
    assert tracked_raw.isdisjoint(expected)
    assert receipt["receipt_payload_sha256"] == _canonical_hash(
        receipt, "receipt_payload_sha256"
    )


def test_frozen_text_bytes_survive_the_git_tree_and_plain_archive() -> None:
    prompt_paths = tuple(
        path.relative_to(ROOT).as_posix()
        for directory in FROZEN_PROMPT_DIRECTORIES
        for path in sorted((ROOT / directory).iterdir())
        if path.is_file()
    )
    paths = tuple(dict.fromkeys((*FROZEN_TEXT_BYTES, *prompt_paths)))
    attributes = subprocess.check_output(
        ["git", "check-attr", "--cached", "text", "--", *paths],
        cwd=ROOT,
        text=True,
    ).splitlines()
    assert len(attributes) == len(paths)
    assert all(line.endswith(": text: unset") for line in attributes)

    tree = subprocess.check_output(
        ["git", "write-tree"], cwd=ROOT, text=True
    ).strip()
    archive_bytes = subprocess.check_output(
        ["git", "archive", "--format=tar", tree, "--", *paths], cwd=ROOT
    )
    with tarfile.open(fileobj=io.BytesIO(archive_bytes), mode="r:") as archive:
        for relative in paths:
            working_bytes = (ROOT / relative).read_bytes()
            index_bytes = subprocess.check_output(
                ["git", "show", f":{relative}"], cwd=ROOT
            )
            archived = archive.extractfile(relative)
            assert archived is not None
            member_bytes = archived.read()
            assert working_bytes == index_bytes == member_bytes
            if relative in FROZEN_TEXT_BYTES:
                expected_size, expected_hash = FROZEN_TEXT_BYTES[relative]
                assert len(member_bytes) == expected_size
                assert hashlib.sha256(member_bytes).hexdigest() == expected_hash


def test_compact_final_semantic_snapshot_equals_frozen_task_one_baseline() -> None:
    final = cleanup_semantics.build_final_semantic_snapshot(
        ROOT,
        baseline_path=BASELINE_PATH,
        path_migration=PATH_MIGRATION,
    )

    assert final == BASELINE


def test_cleanup_semantics_cli_writes_the_exact_reconciled_snapshot(
    tmp_path: Path,
) -> None:
    output = tmp_path / "final-semantic-snapshot.json"

    result = cleanup_semantics.main(
        (
            "--repo-root",
            str(ROOT),
            "--output",
            str(output),
            "--baseline",
            str(BASELINE_PATH),
            "--path-migration",
            str(PATH_MIGRATION),
            "--expect-release-baseline",
        )
    )

    assert result == 0
    assert output.read_bytes() == BASELINE_PATH.read_bytes()


def test_published_final_snapshot_is_byte_identical_to_the_frozen_baseline() -> None:
    assert FINAL_SNAPSHOT.read_bytes() == BASELINE_PATH.read_bytes()
    final = json.loads(FINAL_SNAPSHOT.read_text(encoding="utf-8"))
    assert final["outcomes"]["total"] == 169
    assert final["outcomes"]["status_counts"] == {
        "artifact-unavailable": 54,
        "blocked": 28,
        "failed": 6,
        "passed": 81,
    }
    assert final["evidence"]["unique_path_count"] == 1846
    assert final["evidence"]["relationship_count"] == 1846
    assert final["evidence"]["record_count"] == 1857
    assert final["evidence"]["unique_evidence_id_count"] == 1857
    assert len(final["evidence"]["relationships"]) == 1857
    assert final["reports"]["pair_count"] == 6
    assert final["pdfs"]["file_count"] == 6
    assert final["pdfs"]["page_count"] == 202


def test_archive_and_removal_receipts_bind_every_selected_path_and_byte() -> None:
    cleanup = ROOT / "docs/testing/cleanup"
    summary = json.loads((cleanup / "archive-summary.json").read_text(encoding="utf-8"))
    receipt_path = Path(summary["receipt_path"])
    archive = json.loads(receipt_path.read_text(encoding="utf-8"))
    removal = json.loads((cleanup / "removal-receipt.json").read_text(encoding="utf-8"))
    inventory = _rows(cleanup / "file-inventory.csv")
    inventory_by_key = {
        (row["source_root_id"], row["path"]): row for row in inventory
    }

    assert summary == {
        "archive_manifest_sha256": "863a13513f760ac70545b67fa4e70e017fe5e0eb89de4313f7c8a68debb54ed7",
        "archive_root": str(receipt_path.parent),
        "entry_count": 1128,
        "plan_sha256": "9f35b438a11b36d64e959e062fd4791944bfbb2d7105f3c3fad7f393bbe42ef0",
        "receipt_path": str(receipt_path),
        "receipt_sha256": _sha256(receipt_path),
        "schema": "testing-history-archive-summary/v1",
        "total_bytes": 2038466,
        "unverified_count": 0,
    }
    assert archive["entry_count"] == len(archive["entries"]) == 1128
    assert archive["total_bytes"] == 2038466
    assert archive["unverified_count"] == 0
    assert archive["archive_manifest_sha256"] == summary["archive_manifest_sha256"]
    assert all(entry["journal"] == ["planned", "copied", "verified"] for entry in archive["entries"])
    for entry in archive["entries"]:
        target = receipt_path.parent / entry["archive_path"]
        assert target.is_file(), target
        assert target.stat().st_size == entry["size_bytes"]
        assert _sha256(target) == entry["sha256"]

    assert removal["schema"] == "testing-cleanup-removal-receipt/v1"
    assert removal["status"] == "removed"
    assert removal["planned_count"] == removal["removed_count"] == len(removal["entries"]) == 1529
    assert removal["archive_entry_count"] == 1128
    assert removal["archive_receipt_sha256"] == summary["receipt_sha256"]
    assert removal["archive_manifest_sha256"] == summary["archive_manifest_sha256"]
    assert removal["action_counts"] == {
        "archive_external": {"planned": 1128, "removed": 1128},
        "remove_regenerable": {"planned": 401, "removed": 401},
    }
    for entry in removal["entries"]:
        inventory_row = inventory_by_key[(entry["source_root_id"], entry["original_path"])]
        assert inventory_row["action"] == entry["action"]
        assert inventory_row["sha256"] == entry["sha256"]
        assert int(inventory_row["size_bytes"]) == entry["size_bytes"]
        assert entry["state"] == "removed"
        assert not (Path(entry["source_root"]) / entry["original_path"]).exists()
        if entry["action"] == "archive_external":
            archived = Path(entry["archive_destination"])
            assert archived.is_file()
            assert archived.stat().st_size == entry["size_bytes"]
            assert _sha256(archived) == entry["sha256"]
        else:
            assert entry["archive_destination"] is None
            assert entry["regenerable_rule"] == (
                "python bytecode cache: scripts/testing/**/__pycache__/*.pyc"
            )


def test_before_after_inventory_counts_and_bytes_reconcile_exactly() -> None:
    inventory = _rows(ROOT / "docs/testing/cleanup/file-inventory.csv")
    removal = json.loads(
        (ROOT / "docs/testing/cleanup/removal-receipt.json").read_text(encoding="utf-8")
    )
    inventory_by_key = {
        (row["source_root_id"], row["path"]): row for row in inventory
    }
    before: dict[str, list[int]] = defaultdict(lambda: [0, 0])
    removed: dict[str, list[int]] = defaultdict(lambda: [0, 0])
    for row in inventory:
        before[row["tracked_status"]][0] += 1
        before[row["tracked_status"]][1] += int(row["size_bytes"])
    for entry in removal["entries"]:
        row = inventory_by_key[(entry["source_root_id"], entry["original_path"])]
        removed[row["tracked_status"]][0] += 1
        removed[row["tracked_status"]][1] += entry["size_bytes"]
    after = {
        status: [
            before[status][0] - removed[status][0],
            before[status][1] - removed[status][1],
        ]
        for status in before
    }

    assert dict(before) == {
        "tracked": [3404, 94055555],
        "untracked": [1045, 10949857],
        "ignored": [608, 1872737998],
    }
    assert dict(removed) == {
        "tracked": [697, 379434],
        "untracked": [348, 599920],
        "ignored": [484, 17286055],
    }
    assert after == {
        "tracked": [2707, 93676121],
        "untracked": [697, 10349937],
        "ignored": [124, 1855451943],
    }
    assert [len(inventory), sum(int(row["size_bytes"]) for row in inventory)] == [
        5057,
        1977743410,
    ]
    assert [len(removal["entries"]), sum(row["size_bytes"] for row in removal["entries"])] == [
        1529,
        18265409,
    ]
    assert [sum(value[0] for value in after.values()), sum(value[1] for value in after.values())] == [
        3528,
        1959478001,
    ]


def test_all_migration_indexes_are_complete_and_release_text_has_no_stale_path() -> None:
    path_rows = _rows(PATH_MIGRATION)
    test_rows = _rows(ROOT / "docs/testing/cleanup/test-path-migration.csv")
    code_rows = _rows(ROOT / "archive/testing-code/MIGRATION.csv")
    evidence_rows = _rows(ROOT / "experiments/raw-results/evidence-manifest.csv")

    assert len(path_rows) == len({row["old_path"] for row in path_rows}) == 1572
    assert len(test_rows) == len({row["destination_path"] for row in test_rows}) == 94
    assert len(code_rows) == len({row["old_path"] for row in code_rows}) == 74
    assert len(evidence_rows) == len({row["source_path"] for row in evidence_rows}) == 1400

    tracked_tests = set(
        subprocess.run(
            ["git", "ls-files", "scripts/testing/tests/**/test_*.py"],
            cwd=ROOT,
            check=True,
            capture_output=True,
            text=True,
        ).stdout.splitlines()
    )
    assert {row["destination_path"] for row in test_rows} == tracked_tests
    assert all((ROOT / row["destination_path"]).is_file() for row in test_rows)
    assert all((ROOT / row["retained_path"]).is_file() for row in evidence_rows)
    for row in code_rows:
        assert not (ROOT / row["old_path"]).exists() or row["old_path"] == row["final_path"]
        target = row["final_path"] or row["archive_path"]
        assert target and (ROOT / target).is_file(), row["old_path"]

    published_rows = [
        row
        for row in path_rows
        if row["old_path"].startswith("docs/testing/final-results/")
    ]
    for row in published_rows:
        assert not (ROOT / row["old_path"]).exists(), row["old_path"]
        assert (ROOT / row["new_path"]).exists(), row["new_path"]
    text_files = [
        path
        for path in RELEASE_ROOT.rglob("*")
        if path.is_file()
        and path.suffix.casefold() in {".csv", ".json", ".md", ".txt"}
    ]
    texts = [path.read_text(encoding="utf-8-sig") for path in text_files]
    for row in path_rows:
        full = row["old_path"]
        forbidden = {full}
        if full.startswith("docs/testing/final-results/"):
            forbidden.add(full.removeprefix("docs/testing/final-results/"))
        assert all(
            token not in text for text in texts for token in forbidden
        ), full


def test_six_parity_pairs_202_renderable_pdf_pages_and_two_portable_workbooks() -> None:
    page_counts = []
    route_directories = sorted(RELEASE_ROOT.glob("0[1-6]-*"))
    assert len(route_directories) == 6
    for route in route_directories:
        markdown = next((route / "reports").glob("*-report.md"))
        docx = next((route / "reports").glob("*-report.docx"))
        pdf_path = next((route / "reports").glob("*-report.pdf"))
        assert compare_markdown_docx(markdown, docx)["matches"] is True
        with fitz.open(pdf_path) as pdf:
            page_counts.append(pdf.page_count)
            for page in pdf:
                assert page.get_text("text").strip()
                pixmap = page.get_pixmap(matrix=fitz.Matrix(0.25, 0.25), alpha=False)
                assert pixmap.width > 0 and pixmap.height > 0
    assert page_counts == [47, 22, 7, 54, 52, 20]
    assert sum(page_counts) == 202

    assert len(PORTABLE_WORKBOOKS) == 2
    for path in PORTABLE_WORKBOOKS:
        absolute_cells = []
        workbook = openpyxl.load_workbook(
            path,
            read_only=True,
            data_only=False,
            keep_links=True,
        )
        try:
            for sheet in workbook.worksheets:
                for row in sheet.iter_rows():
                    for cell in row:
                        value = cell.value
                        if isinstance(value, str) and WINDOWS_ABSOLUTE.match(value):
                            absolute_cells.append(f"{sheet.title}!{cell.coordinate}")
        finally:
            workbook.close()
        assert absolute_cells == []


def test_final_validation_receipt_records_every_cleanup_invariant() -> None:
    validation = json.loads(FINAL_VALIDATION.read_text(encoding="utf-8"))

    assert validation["schema"] == "testing-cleanup-final-validation/v1"
    assert validation["valid"] is True
    assert validation["release_version"] == "unified-final-results-2026-09-01-v2"
    assert validation["cleanup_version"] == "testing-results-cleanup-2026-09-02-v1"
    assert set(validation["checks"]) == {
        "archive_receipt",
        "clean_git_archive",
        "comparison_decisions",
        "evidence_relationships",
        "implementation_root_removal",
        "inventory_reduction",
        "migration_indexes",
        "outcomes",
        "pdf_reports",
        "portable_workbooks",
        "removal_receipt",
        "report_parity",
        "semantic_equality",
        "stale_paths",
    }
    assert all(check["valid"] is True for check in validation["checks"].values())
    semantic = validation["checks"]["semantic_equality"]
    assert semantic["byte_equal"] is True
    assert semantic["baseline_file_sha256"] == semantic["final_file_sha256"]
    assert semantic["snapshot_sha256"] == BASELINE["snapshot_sha256"]
    assert validation["checks"]["outcomes"]["total"] == 169
    implementation = validation["checks"]["implementation_root_removal"]
    assert implementation["removed_path_count"] == 693
    assert implementation["authoritative_size_bytes"] == 125_662
    assert implementation["parent_git_blob_exact_count"] == 565
    assert implementation["parent_git_blob_eol_normalized_count"] == 128
    assert implementation["remaining_selected_path_count"] == 0
    relationships = validation["checks"]["evidence_relationships"]
    assert relationships["record_count"] == 1857
    assert relationships["relationship_record_count"] == 1857
    assert relationships["snapshot_relationship_count"] == 1846
    assert relationships["snapshot_relationship_count_semantics"] == (
        "unique cited path cardinality (legacy frozen-snapshot field name)"
    )
    assert relationships["unique_path_count"] == 1846
    assert validation["checks"]["report_parity"]["pair_count"] == 6
    assert validation["checks"]["pdf_reports"]["page_count"] == 202
    assert validation["checks"]["portable_workbooks"]["absolute_path_count"] == 0
    assert validation["checks"]["clean_git_archive"]["receipt"] == (
        "docs/testing/cleanup/clean-archive-validation.json"
    )


def test_ro_crate_models_cleanup_finalization_before_manifest_generation() -> None:
    crate = json.loads(
        (RELEASE_ROOT / "ro-crate-metadata.json").read_text(encoding="utf-8")
    )
    entities = {entity["@id"]: entity for entity in crate["@graph"]}
    finalization = entities["#testing-results-cleanup-finalization"]
    manifest_generation = entities["#release-manifest-generation"]

    finalization_inputs = {item["@id"] for item in finalization["object"]}
    finalization_outputs = {item["@id"] for item in finalization["result"]}
    manifest_inputs = {item["@id"] for item in manifest_generation["object"]}
    manifest_outputs = {item["@id"] for item in manifest_generation["result"]}

    assert "manifest-sha256.txt" not in finalization_inputs
    assert finalization_inputs == {
        "validation/release-readiness.json",
        "#implementation-root-removal-receipt",
    }
    receipt = entities["#implementation-root-removal-receipt"]
    assert receipt["@type"] == "CreativeWork"
    assert receipt["identifier"] == (
        "docs/testing/cleanup/implementation-root-removal-receipt.json"
    )
    assert receipt["sha256"] == hashlib.sha256(
        IMPLEMENTATION_REMOVAL_RECEIPT.read_bytes()
    ).hexdigest()
    assert finalization_outputs == {
        "validation/validation.json",
        "validation/validation.md",
    }
    assert finalization_outputs <= manifest_inputs
    assert manifest_outputs == {"manifest-sha256.txt"}


def test_clean_archive_receipt_is_self_hashed_and_records_all_thirteen_gates() -> None:
    receipt = json.loads(CLEAN_ARCHIVE_RECEIPT.read_text(encoding="utf-8"))

    assert receipt["schema"] == "testing-cleanup-clean-archive-validation/v1"
    assert receipt["valid"] is True
    assert receipt["base_commit"] == "26d08591837ae8c6238686797b7e735e8738cb0d"
    assert receipt["review_fix_input_commit"] == (
        "2bcb25388e58ca1cb2d55afe6239ce9e7706ca74"
    )
    assert re.fullmatch(r"[0-9a-f]{40}", receipt["validated_tree"])
    assert re.fullmatch(r"[0-9a-f]{40}", receipt["release_subtree"])
    assert receipt["excluded_paths"] == [
        "docs/testing/cleanup/clean-archive-validation.json"
    ]
    assert receipt["archive"]["format"] == "tar"
    assert receipt["archive"]["size_bytes"] > 0
    assert re.fullmatch(r"[0-9a-f]{64}", receipt["archive"]["sha256"])
    assert receipt["archive"]["short_path_validation"] is True
    assert receipt["archive"]["evidence_hydration_performed"] is False
    assert receipt["archive"]["published_evidence_source_count"] == 1846
    assert receipt["archive"]["published_evidence_relationship_record_count"] == 1857
    assert receipt["archive"]["snapshot_relationship_count"] == 1846
    assert receipt["archive"]["missing_published_evidence_source_count"] == 0
    assert receipt["validation"]["command"] == [
        "py",
        "-3",
        "-B",
        "-m",
        "scripts.testing.cli.validate_results",
        "--route",
        "all",
        "--output-root",
        "docs/testing/final-results",
    ]
    assert receipt["validation"]["exit_code"] == 0
    assert receipt["validation"]["gate_count"] == 13
    assert [gate["name"] for gate in receipt["validation"]["gates"]] == list(
        GATE_ORDER
    )
    assert all(
        gate["valid"] is True
        and gate["result_code"] == 0
        and gate["blocking_finding_count"] == 0
        for gate in receipt["validation"]["gates"]
    )
    assert receipt["receipt_payload_sha256"] == _canonical_hash(
        receipt, "receipt_payload_sha256"
    )
