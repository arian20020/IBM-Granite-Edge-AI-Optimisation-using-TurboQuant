"""Generate the deterministic two-route Workbook 05 memory-frontier execution index."""

from __future__ import annotations

import argparse
import csv
from pathlib import Path
from typing import Iterable


FIELDNAMES = (
    "Execution_Record_ID",
    "Test_ID",
    "Workbook_ID",
    "Route_ID",
    "Phase_ID",
    "Category",
    "Test_Title",
    "Expected_K_Record_Bytes",
    "Expected_V_Record_Bytes",
    "Verified_K_Record_Bytes",
    "Verified_V_Record_Bytes",
    "Memory_Rank",
    "Memory_Rank_Status",
    "Frontier_Status",
    "Skip_Reason",
    "Run_ID",
    "Evidence_Path",
)
PHASE_ORDER = (
    "phase-0-preflight",
    "phase-1-source-admission",
    "phase-2-documented-build",
    "phase-3-conformance",
    "phase-4-capability-sweep",
    "phase-5-granite-3b-frontier",
    "phase-6-granite-3b-formal",
    "phase-7-asymmetric-cross-family",
    "phase-8-granite-8b-frontier",
    "phase-9-ablations-repeatability",
    "phase-10-conclusion",
)
PHASE_RANK = {phase: index for index, phase in enumerate(PHASE_ORDER)}
ROUTE_RANK = {"route-a-merged-openvino": 0, "route-b-experimental-qjl-polar": 1}


def phase_for(category: str, title: str) -> str:
    """Map existing semantic test IDs into the approved execution phases."""

    category_lower = category.lower()
    title_lower = title.lower()
    if "build" in category_lower or "setup" in category_lower:
        return "phase-1-source-admission"
    if "unit" in category_lower or "conformance" in category_lower:
        return "phase-3-conformance"
    if "capability sweep" in category_lower:
        return "phase-4-capability-sweep"
    if "granite 8b" in title_lower or "8b" in title_lower and "granite" in title_lower:
        return "phase-8-granite-8b-frontier"
    if any(term in title_lower for term in ("ablation", "repeatability", "stability", "restart", "fused quant")):
        return "phase-9-ablations-repeatability"
    if any(term in title_lower for term in ("asymmetric", "key-only", "value-only", "cross-family", "mixed ")):
        return "phase-7-asymmetric-cross-family"
    if any(term in title_lower for term in ("context-scaling", "context scaling", "maximum context", "context series")):
        return "phase-5-granite-3b-frontier"
    return "phase-6-granite-3b-formal"


def generate(traceability: Path) -> list[dict[str, str]]:
    """Create one explicit index record for each WB-04 and WB-05 traceability ID."""

    with traceability.open(newline="", encoding="utf-8-sig") as handle:
        source_rows = [
            row
            for row in csv.DictReader(handle)
            if row["Workbook_ID"] in {"WB-04", "WB-05"}
        ]

    records: list[dict[str, str]] = []
    for row in source_rows:
        route = "route-a-merged-openvino" if row["Workbook_ID"] == "WB-04" else "route-b-experimental-qjl-polar"
        phase = phase_for(row["Category"], row["Test_Title"])
        build_or_setup = "build" in row["Category"].lower() or "setup" in row["Category"].lower()
        records.append(
            {
                "Execution_Record_ID": "",
                "Test_ID": row["Test_ID"],
                "Workbook_ID": row["Workbook_ID"],
                "Route_ID": route,
                "Phase_ID": phase,
                "Category": row["Category"],
                "Test_Title": row["Test_Title"],
                "Expected_K_Record_Bytes": "",
                "Expected_V_Record_Bytes": "",
                "Verified_K_Record_Bytes": "",
                "Verified_V_Record_Bytes": "",
                "Memory_Rank": "",
                "Memory_Rank_Status": "Not applicable" if build_or_setup else "Pending conformance",
                "Frontier_Status": "Not started" if build_or_setup else "Blocked pending verified storage",
                "Skip_Reason": "",
                "Run_ID": "",
                "Evidence_Path": "",
            }
        )

    records.sort(
        key=lambda row: (
            ROUTE_RANK[row["Route_ID"]],
            PHASE_RANK[row["Phase_ID"]],
            row["Category"],
            row["Test_ID"],
        )
    )
    for index, record in enumerate(records, start=1):
        record["Execution_Record_ID"] = f"MF-{index:04d}"
    return records


def write_index(records: list[dict[str, str]], output: Path) -> None:
    """Write stable UTF-8 CSV with LF endings and a fixed column order."""

    output.parent.mkdir(parents=True, exist_ok=True)
    with output.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=FIELDNAMES, lineterminator="\n")
        writer.writeheader()
        writer.writerows(records)


def main(argv: Iterable[str] | None = None) -> int:
    """CLI for generation and reproducibility tests."""

    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--traceability", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    arguments = parser.parse_args(list(argv) if argv is not None else None)
    records = generate(arguments.traceability.resolve())
    write_index(records, arguments.output.resolve())
    print(f"Generated {len(records)} execution records: {arguments.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
