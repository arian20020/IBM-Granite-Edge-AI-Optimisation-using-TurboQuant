"""Validate manual adjudications and publish all-row AtomicBot quality evidence."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from scripts.testing.campaigns.atomicbot.matrix import load_matrix
from scripts.testing.campaigns.atomicbot.quality import score_response


def arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--matrix", type=Path, required=True)
    parser.add_argument("--raw-root", type=Path, required=True)
    parser.add_argument("--adjudications", type=Path, required=True)
    parser.add_argument("--register", type=Path, required=True)
    parser.add_argument("--summary", type=Path, required=True)
    return parser.parse_args()


def main() -> int:
    args = arguments()
    adjudications = json.loads(args.adjudications.read_text(encoding="utf-8"))
    cases = [case for case in load_matrix(args.matrix) if case.backend != "build"]
    existing: list[dict[str, str]] = []
    with args.register.open(newline="", encoding="utf-8-sig") as handle:
        reader = csv.DictReader(handle)
        fields = list(reader.fieldnames or ())
        existing = [row for row in reader if row.get("Route") != "atomicbot-turboquant"]

    published: list[dict[str, str]] = []
    summary_rows: list[dict[str, object]] = []
    used_keys: set[str] = set()
    for case in cases:
        scores = []
        prompt_summary = {}
        for number in range(1, 7):
            prompt_id = f"P{number}"
            raw_path = args.raw_root / case.test_id / f"{prompt_id}.json"
            if not raw_path.is_file():
                raise RuntimeError(f"missing raw result: {raw_path}")
            raw = json.loads(raw_path.read_text(encoding="utf-8"))
            if raw.get("status") not in {"complete", "timeout", "safety-blocked"}:
                raise RuntimeError(f"non-terminal raw result: {raw_path}: {raw.get('status')}")
            output = raw.get("output", "")
            digest = hashlib.sha256(output.encode()).hexdigest()
            if digest != raw.get("output_sha256"):
                raise RuntimeError(f"response hash mismatch: {raw_path}")
            key = f"{prompt_id}:{digest}"
            if key not in adjudications:
                raise RuntimeError(f"missing adjudication: {key} ({case.test_id})")
            adjudication = adjudications[key]
            used_keys.add(key)
            result = score_response(prompt_id, output, adjudication)
            scores.append(result.final_score)
            cap_reason = adjudication.get("critical_cap_reason", "No critical cap")
            response_path = args.raw_root / case.test_id / f"{prompt_id}-response.txt"
            row = {
                "Evaluation_ID": f"QE-{case.test_id}-{prompt_id}",
                "Route": "atomicbot-turboquant", "Workbook_ID": "WB-02",
                "Test_ID": case.test_id, "Run_ID": f"{case.test_id}-QUALITY-R001",
                "Configuration_ID": f"CONFIG-{case.test_id}",
                "Comparison_Role": f"Independent all-row screen: {case.format}",
                "Prompt_Set_ID": "GTQ-PROMPTS-v1", "Prompt_ID": prompt_id,
                "Rubric_ID": "GTQ-QUALITY-RUBRIC-v1",
                "Raw_Response_Path": response_path.as_posix(), "Response_SHA256": digest,
                "Deterministic_Check_Result": "Pass" if result.deterministic_pass else "Fail",
                "Format_Valid": adjudication["format_valid"],
                "Required_Facts_Retained": adjudication["required_facts_retained"],
                "Unsupported_Statements_Count": str(adjudication["unsupported_statements_count"]),
                "Repetition_Corruption_or_Truncation": adjudication["integrity_issue"],
                "Correctness_and_Grounding_0_to_10": str(result.dimensions["correctness_and_grounding"]),
                "Instruction_and_Format_0_to_10": str(result.dimensions["instruction_and_format_adherence"]),
                "Completeness_and_Fact_Retention_0_to_10": str(result.dimensions["completeness_and_fact_retention"]),
                "Relevance_Clarity_Coherence_0_to_10": str(result.dimensions["relevance_clarity_and_coherence"]),
                "Stability_and_Integrity_0_to_10": str(result.dimensions["stability_and_output_integrity"]),
                "Weighted_Score_0_to_10": f"{result.final_score:.2f}",
                "Critical_Cap_Applied": "Yes" if result.critical_caps else "No",
                "Critical_Cap_Reason": cap_reason,
                "Blind_Label": f"response-{digest[:12]}",
                "Pairwise_Order": "N/A - independently scored",
                "Judge_or_Reviewer": "Codex deterministic gates plus manual adjudication",
                "Manual_Adjudication_Required": "Yes",
                "Manual_Adjudication_Result": adjudication["manual_result"],
                "Result": "Pass" if result.deterministic_pass else "Fail",
                "Evidence_Commit": "Pending PR",
                "Notes": adjudication["notes"],
            }
            missing = [name for name in fields if not row.get(name)]
            if missing:
                raise RuntimeError(f"blank register fields for {case.test_id}/{prompt_id}: {missing}")
            published.append(row)
            prompt_summary[prompt_id] = {"score": result.final_score,
                                         "deterministic_pass": result.deterministic_pass,
                                         "response_sha256": digest}
        summary_rows.append({"test_id": case.test_id, "model_id": case.model_id,
                             "format": case.format, "backend": case.backend,
                             "quality_mean": round(sum(scores) / len(scores), 4),
                             "prompts": prompt_summary})

    unused = sorted(set(adjudications) - used_keys)
    if unused:
        raise RuntimeError(f"unused adjudications: {unused}")
    with args.register.open("w", newline="", encoding="utf-8-sig") as handle:
        writer = csv.DictWriter(handle, fieldnames=fields, lineterminator="\n")
        writer.writeheader(); writer.writerows(existing + published)
    args.summary.parent.mkdir(parents=True, exist_ok=True)
    args.summary.write_text(json.dumps({"schema_version": 1, "rows": summary_rows}, indent=2),
                            encoding="utf-8")
    print(f"published {len(published)} evaluations for {len(summary_rows)} rows")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
