"""Idempotently append WB-03 P1-P6 adjudications to the central quality register."""

from __future__ import annotations

import argparse
import csv
import json
from pathlib import Path


DIMENSION_COLUMNS = (
    ("correctness_and_grounding", "Correctness_and_Grounding_0_to_10"),
    ("instruction_and_format_adherence", "Instruction_and_Format_0_to_10"),
    ("completeness_and_fact_retention", "Completeness_and_Fact_Retention_0_to_10"),
    ("relevance_clarity_and_coherence", "Relevance_Clarity_Coherence_0_to_10"),
    ("stability_and_output_integrity", "Stability_and_Integrity_0_to_10"),
)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--adjudication", type=Path, required=True)
    parser.add_argument("--register", type=Path, required=True)
    parser.add_argument("--quality-root", type=Path, required=True)
    args = parser.parse_args()
    results = json.loads(args.adjudication.read_text(encoding="utf-8-sig"))
    with args.register.open(encoding="utf-8-sig", newline="") as stream:
        reader = csv.DictReader(stream)
        fields = reader.fieldnames
        # Recovery updates are intentionally narrow: preserve all existing WB-03
        # rows and idempotently replace only AH-09 P1-P6.
        rows = [row for row in reader if not (
            row.get("Workbook_ID") == "WB-03" and row.get("Test_ID") == "AH-09")]
    if not fields:
        raise ValueError("quality register header is missing")
    for test_id, result in sorted(results.items()):
        if test_id != "AH-09":
            continue
        for prompt_id, item in sorted(result["prompts"].items()):
            raw, adjudication = item["raw"], item["adjudication"]
            caps = adjudication.get("critical_caps", [])
            record = {field: "" for field in fields}
            record.update({
                "Evaluation_ID": f"QE-{test_id}-{prompt_id}",
                "Route": "animehacker-tq3-0", "Workbook_ID": "WB-03",
                "Test_ID": test_id, "Run_ID": f"{test_id}-QUALITY-R001",
                "Configuration_ID": f"CONFIG-{test_id}",
                "Comparison_Role": f"Independent all-row screen: {raw['cache']}",
                "Prompt_Set_ID": "GTQ-PROMPTS-v1", "Prompt_ID": prompt_id,
                "Rubric_ID": "GTQ-QUALITY-RUBRIC-v1",
                "Raw_Response_Path": str(args.quality_root / test_id / f"{prompt_id}-response.txt").replace("\\", "/"),
                "Response_SHA256": raw["output_sha256"],
                "Deterministic_Check_Result": "Pass" if adjudication["deterministic_pass"] else "Fail",
                "Format_Valid": adjudication["format_valid"],
                "Required_Facts_Retained": adjudication["required_facts_retained"],
                "Unsupported_Statements_Count": adjudication["unsupported_statements_count"],
                "Repetition_Corruption_or_Truncation": adjudication["integrity_issue"],
                "Weighted_Score_0_to_10": item["final_score"],
                "Critical_Cap_Applied": "Yes" if caps else "No",
                "Critical_Cap_Reason": adjudication["critical_cap_reason"],
                "Blind_Label": f"response-{raw['output_sha256'][:12]}",
                "Pairwise_Order": "Independently scored", "Judge_or_Reviewer": "project tooling deterministic gates plus manual adjudication",
                "Manual_Adjudication_Required": "Yes",
                "Manual_Adjudication_Result": adjudication["manual_result"],
                "Result": "Pass" if adjudication["deterministic_pass"] else "Fail",
                "Evidence_Commit": "Pending PR", "Notes": adjudication["notes"],
            })
            for dimension, column in DIMENSION_COLUMNS:
                record[column] = adjudication["dimensions"][dimension]
            rows.append(record)
    with args.register.open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=fields, lineterminator="\n")
        writer.writeheader(); writer.writerows(rows)
    print(f"wrote {sum(1 for row in rows if row.get('Workbook_ID') == 'WB-03')} WB-03 evaluations")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
