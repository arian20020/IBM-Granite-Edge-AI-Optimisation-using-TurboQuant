"""Audit generated WB-04 and persist the structural QA record."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import sys
import tempfile
from pathlib import Path
from typing import Sequence

ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from scripts.testing.official_openvino.docx_audit import audit_docx
from scripts.testing.official_openvino.matrix import load_matrix


DEFAULT_DOCX = ROOT / "docs/testing/workbooks/generated/04_Official_OpenVINO_Controlled_Retest_Workbook_v1.docx"
DEFAULT_MANIFEST = ROOT / "docs/testing/workbooks/Controlled-Workbook-Manifest.csv"
DEFAULT_MATRIX = ROOT / "experiments/manifests/official-openvino/retest-matrix.json"
# Retained for compatibility; releases should provide a dated 2026-07-30 output.
DEFAULT_OUTPUT = ROOT / "experiments/raw-results/official-openvino/2026-07-19/docx-structural-qa.json"
RELEASE_OUTPUT = ROOT / "experiments/raw-results/openvino-turboquant/2026-07-30/release-evidence/docx-structural-qa.json"
RELEASE_VISIBLE_REVISION = "1.8"
RELEASE_TABLE_COUNT = 10
FROZEN_MATRIX_SHA256 = "7db2636b403d285aa3886c9f23560a9e48bd43645e9aca35e0d1fc4f16eaea42"


def write_report_atomically(output: Path, report: dict) -> None:
    """Publish one JSON report only after its complete canonical content is ready."""
    output = output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary_name = tempfile.mkstemp(
        dir=output.parent,
        prefix=f".{output.name}.",
        suffix=".tmp",
    )
    temporary = Path(temporary_name)
    try:
        with os.fdopen(descriptor, "w", encoding="utf-8", newline="\n") as handle:
            handle.write(json.dumps(report, indent=2, sort_keys=True) + "\n")
            handle.flush()
            os.fsync(handle.fileno())
        temporary.replace(output)
    except BaseException:
        temporary.unlink(missing_ok=True)
        raise


def enforce_expected_shape(
    report: dict,
    *,
    expected_visible_revision: str | None,
    expected_table_count: int | None,
) -> None:
    """Apply optional release-level checks without changing the frozen audit API."""
    if (
        expected_visible_revision is not None
        and report["visible_revision"] != expected_visible_revision
    ):
        raise ValueError(
            "visible revision mismatch: "
            f"expected {expected_visible_revision}, got {report['visible_revision']}"
        )
    if expected_table_count is not None and report["table_count"] != expected_table_count:
        raise ValueError(
            "table count mismatch: "
            f"expected {expected_table_count}, got {report['table_count']}"
        )


def parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--docx", type=Path)
    parser.add_argument("--manifest", type=Path)
    parser.add_argument("--matrix", type=Path)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--expected-visible-revision")
    parser.add_argument("--expected-table-count", type=int)
    parser.add_argument(
        "--release",
        action="store_true",
        help=(
            "Enforce the controlled WB-04 v1.8 / 10-table release inputs and "
            "2026-07-30 output path."
        ),
    )
    return parser.parse_args(argv)


def _same_path(actual: Path, expected: Path) -> bool:
    return actual.resolve() == expected.resolve()


def _sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def resolve_audit_configuration(args: argparse.Namespace) -> tuple[Path, Path, Path, Path, str | None, int | None]:
    """Resolve compatible defaults, or the immutable controlled release profile."""
    docx = args.docx or DEFAULT_DOCX
    manifest = args.manifest or DEFAULT_MANIFEST
    matrix = args.matrix or DEFAULT_MATRIX
    output = args.output or DEFAULT_OUTPUT
    revision = args.expected_visible_revision
    table_count = args.expected_table_count
    if not args.release:
        return docx, manifest, matrix, output, revision, table_count
    for name, supplied, expected in (
        ("docx", args.docx, DEFAULT_DOCX),
        ("manifest", args.manifest, DEFAULT_MANIFEST),
        ("matrix", args.matrix, DEFAULT_MATRIX),
        ("output", args.output, RELEASE_OUTPUT),
    ):
        if supplied is not None and not _same_path(supplied, expected):
            raise ValueError(f"release mode refuses noncanonical --{name}")
    if revision is not None and revision != RELEASE_VISIBLE_REVISION:
        raise ValueError("release mode requires visible revision 1.8")
    if table_count is not None and table_count != RELEASE_TABLE_COUNT:
        raise ValueError("release mode requires table count 10")
    if _sha256(matrix) != FROZEN_MATRIX_SHA256:
        raise ValueError("release mode frozen matrix SHA-256 mismatch")
    return (
        DEFAULT_DOCX,
        DEFAULT_MANIFEST,
        DEFAULT_MATRIX,
        RELEASE_OUTPUT,
        RELEASE_VISIBLE_REVISION,
        RELEASE_TABLE_COUNT,
    )


def main(argv: Sequence[str] | None = None) -> int:
    args = parse_args(argv)
    try:
        docx, manifest, matrix_path, output, revision, table_count = resolve_audit_configuration(args)
        matrix = load_matrix(matrix_path)
        report = audit_docx(docx, manifest, {case.test_id for case in matrix})
        enforce_expected_shape(
            report,
            expected_visible_revision=revision,
            expected_table_count=table_count,
        )
        write_report_atomically(output, report)
    except (OSError, ValueError) as exc:
        print(f"WB-04 DOCX audit refused: {exc}", file=sys.stderr)
        return 1
    print(json.dumps(report, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
