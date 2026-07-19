"""Audit generated WB-04 and persist the structural QA record."""

from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from scripts.testing.official_openvino.docx_audit import audit_docx
from scripts.testing.official_openvino.matrix import load_matrix


def main() -> None:
    docx = ROOT / "docs/testing/workbooks/generated/04_Official_OpenVINO_Controlled_Retest_Workbook_v1.docx"
    manifest = ROOT / "docs/testing/workbooks/Controlled-Workbook-Manifest.csv"
    matrix = load_matrix(ROOT / "experiments/manifests/official-openvino/retest-matrix.json")
    report = audit_docx(docx, manifest, {case.test_id for case in matrix})
    output = ROOT / "experiments/raw-results/official-openvino/2026-07-19/docx-structural-qa.json"
    output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, sort_keys=True))


if __name__ == "__main__":
    main()
