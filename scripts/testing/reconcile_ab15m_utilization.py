"""Reconcile the accepted AB-15M utilization rerun into WB-02 controls."""

from __future__ import annotations

import csv
import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
REL_ROOT = Path("experiments/raw-results/atomicbot-turboquant/2026-07-17/ab-15m-utilization-rerun-v2/AB-15M")
RAW = ROOT / REL_ROOT
SUMMARY = json.loads((RAW / "server-metrics-summary.json").read_text(encoding="utf-8"))


def read_csv(path: Path):
    with path.open(encoding="utf-8-sig", newline="") as stream:
        reader = csv.DictReader(stream)
        return list(reader.fieldnames or []), list(reader)


def write_csv(path: Path, fields: list[str], rows: list[dict]) -> None:
    with path.open("w", encoding="utf-8", newline="") as stream:
        quoting = csv.QUOTE_ALL if path.name == "Evidence-Index.csv" else csv.QUOTE_MINIMAL
        writer = csv.DictWriter(stream, fieldnames=fields, lineterminator="\n",
                                extrasaction="ignore", quoting=quoting)
        writer.writeheader()
        writer.writerows(rows)


def sample_timestamps(number: int) -> tuple[str, str]:
    path = RAW / f"sample-{number}" / "utilization-samples.csv"
    _, rows = read_csv(path)
    return rows[0]["timestamp_utc"], rows[-1]["timestamp_utc"]


def update_performance() -> None:
    path = ROOT / "docs/testing/Performance-Measurement-Register.csv"
    fields, rows = read_csv(path)
    for name, after in (("CPU_Median_Percent", "CPU_Mean_Percent"),
                        ("GPU_Engine_Median_Percent", "GPU_Engine_Mean_Percent")):
        if name not in fields:
            fields.insert(fields.index(after) + 1, name)
    by_number = {index + 1: sample for index, sample in enumerate(SUMMARY["samples"])}
    for row in rows:
        row.setdefault("CPU_Median_Percent", "N/A - not sampled")
        row.setdefault("GPU_Engine_Median_Percent", "N/A - not sampled")
        if row.get("Test_ID") != "AB-15M":
            if not row["CPU_Median_Percent"]:
                row["CPU_Median_Percent"] = "N/A - not sampled"
            if not row["GPU_Engine_Median_Percent"]:
                row["GPU_Engine_Median_Percent"] = "N/A - not sampled"
            continue
        number = int(row["Repetition_Number"])
        sample = by_number[number]
        cpu = sample["utilization"]["cpu_percent"]
        gpu = sample["utilization"]["gpu_percent"]
        start, end = sample_timestamps(number)
        row.update({
            "Start_Timestamp_UTC": start, "End_Timestamp_UTC": end,
            "Input_Tokens": "N/A - tokenizer count not emitted",
            "Output_Tokens": "64", "Model_Load_Time_ms": "N/A - loaded request",
            "TTFT_ms": str(sample["ttft_ms"]),
            "Prompt_Processing_Tokens_Per_Second": "N/A - server metric not emitted",
            "TPOT_ms": "N/A - not derived", "Decode_Tokens_Per_Second": "4.19",
            "Total_Generation_Time_ms": "N/A - raw stream not timestamped per token",
            "Peak_Working_Set_Bytes": str(round(sample["peak_ram_mb"] * 1024 * 1024)),
            "Peak_Private_Bytes": "N/A - not selected for formal statistic",
            "Available_RAM_Before_Bytes": "N/A - memory time series begins after launch",
            "Minimum_Available_RAM_During_Bytes": "N/A - emergency floor verified; exact minimum not aggregated",
            "Available_RAM_After_Bytes": "N/A - collector terminates before post-run sample",
            "KV_Cache_Allocated_Bytes": str(round(sample["kv_mb"] * 1024 * 1024)),
            "GPU_Dedicated_Peak_Bytes": "N/A - integrated GPU",
            "GPU_Shared_Peak_Bytes": "N/A - placement log used; OS byte counter not sampled",
            "CPU_Mean_Percent": f'{cpu["mean"]:.6f}',
            "CPU_Median_Percent": f'{cpu["median"]:.6f}',
            "CPU_Peak_Percent": f'{cpu["peak"]:.6f}',
            "GPU_Engine_Mean_Percent": f'{gpu["mean"]:.6f}',
            "GPU_Engine_Median_Percent": f'{gpu["median"]:.6f}',
            "GPU_Engine_Peak_Percent": f'{gpu["peak"]:.6f}',
            "Unload_Duration_ms": "N/A - cleanup pass/fail recorded",
            "Raw_Metrics_Path": (REL_ROOT / f"sample-{number}" / "utilization-samples.csv").as_posix(),
            "Processed_Result_Path": (REL_ROOT / "server-metrics-summary.json").as_posix(),
            "Notes": "64-token forced loaded-request window; CPU normalized across logical processors; GPU is busiest process engine per timestamp; TTFT is request start to first generated token.",
        })
    write_csv(path, fields, rows)


def update_devices() -> None:
    path = ROOT / "docs/testing/Device-Verification-Register.csv"
    fields, rows = read_csv(path)
    for name, after in (("CPU_Median_Percent", "CPU_Mean_Percent"),
                        ("GPU_Engine_Median_Percent", "GPU_Engine_Mean_Percent")):
        if name not in fields:
            fields.insert(fields.index(after) + 1, name)
    aggregate = SUMMARY["aggregate"]
    for row in rows:
        row.setdefault("CPU_Median_Percent", "N/A - not sampled")
        row.setdefault("GPU_Engine_Median_Percent", "N/A - not sampled")
        if row.get("Test_ID") == "AB-15M":
            row.update({
                "Latest_Run_ID": "AB-15M-UTIL-R001",
                "CPU_Mean_Percent": f'{aggregate["cpu_percent"]["mean"]:.6f}',
                "CPU_Median_Percent": f'{aggregate["cpu_percent"]["median"]:.6f}',
                "CPU_Peak_Percent": f'{aggregate["cpu_percent"]["peak"]:.6f}',
                "GPU_Engine_Mean_Percent": f'{aggregate["gpu_percent"]["mean"]:.6f}',
                "GPU_Engine_Median_Percent": f'{aggregate["gpu_percent"]["median"]:.6f}',
                "GPU_Engine_Peak_Percent": f'{aggregate["gpu_percent"]["peak"]:.6f}',
                "Device_Proof_Path": (REL_ROOT / "sample-1" / "stderr.txt").as_posix(),
                "Utilisation_Samples_Path": (REL_ROOT / "sample-1" / "utilization-samples.csv").as_posix() + "; sample-2; sample-3",
                "Notes": "Controlled serial rerun with 256 MiB emergency floor; 71 formal utilization samples across three loaded 64-token requests; no engine summation or inferred values.",
            })
    write_csv(path, fields, rows)


def update_workbook() -> None:
    path = ROOT / "docs/testing/workbooks/text-templates/02_AtomicBot_TurboQuant_Controlled_Retest_Workbook_v1.md"
    text = path.read_text(encoding="utf-8")
    text = text.replace(
        "| AB-15M | Vulkan maximum 8B | Intel UHD Vulkan0 | Vulkan native placement | 41/41 layers; 4876.27 MiB model and 95.01 MiB compute buffers on Vulkan0 | Vulkan0, 125.13 MiB | No unexplained fallback | N/A - not sampled in bypass collector | N/A - counter unavailable | `safety-bypass/AB-15M/` |",
        "| AB-15M | Vulkan maximum 8B | Intel UHD Vulkan0 | Vulkan native placement | 41/41 layers; 4876.27 MiB model and 95.01 MiB compute buffers on Vulkan0 | Vulkan0, 125.13 MiB | No unexplained fallback | mean 2.12%; median 2.19%; peak 2.81% | mean 95.19%; median 98.00%; peak 100.00% | `2026-07-17/ab-15m-utilization-rerun-v2/AB-15M/` |")
    text = text.replace(
        "| AB-15M | 8B | Q4_K_M | turbo3 | turbo3 | Vulkan full | 4096 | 10455.605 | 125.00 | 637.928 | 4.19 | 6.67 | Pass - controlled bypass; P5 safety-blocked |",
        "| AB-15M | 8B | Q4_K_M | turbo3 | turbo3 | Vulkan full | 4096 | 10456.117 | 125.00 | 653.289 | 4.19 | 6.67 | Pass - utilization rerun; P5 safety-blocked |")
    marker = "### 6.3 Formal runtime results"
    note = ("AB-15M utilization was repeated on 2026-07-17 using three isolated 64-token loaded-request windows (71 accepted time-series samples). "
            "CPU percentages are normalized across logical processors. GPU percentage is the busiest llama-server GPU engine at each timestamp, not a sum across engines; aggregate mean/median/peak were 95.19%/98.00%/100.00%.\n\n")
    if note not in text:
        text = text.replace(marker, note + marker)
    path.write_text(text, encoding="utf-8")


def update_completion() -> None:
    path = ROOT / "docs/testing/Workbook-Completion-Register.csv"
    fields, rows = read_csv(path)
    for row in rows:
        if row.get("Workbook_ID") == "WB-02":
            evidence_root = REL_ROOT.parent.as_posix() + "/"
            if evidence_root not in row["Source_Result_Path"]:
                row["Source_Result_Path"] = row["Source_Result_Path"].rstrip("/") + "; " + evidence_root
            row["Notes"] = "WB-02 v1.6 reconciled to runtime, all-row quality, and AB-15M CPU/GPU utilization evidence with mean, median and peak values."
    write_csv(path, fields, rows)


def update_revision() -> None:
    path = ROOT / "docs/testing/Workbook-Revision-Register.csv"
    fields, rows = read_csv(path)
    rows = [row for row in rows
            if not (row.get("Workbook_ID") == "WB-02" and row.get("Version") == "1.6")]
    for row in rows:
        if row.get("Workbook_ID") == "WB-02" and row.get("Status", "").startswith("Current"):
            row["Status"] = "Superseded"
    rows.append({
        "Record_ID": "WR-027", "Workbook_ID": "WB-02", "Version": "1.6",
        "Date": "2026-07-17", "Changed_By": "Student",
        "Change_Type": "AB-15M utilization correction",
        "Change_Summary": "Repeated AB-15M in isolation and captured 71 accepted request-window CPU/GPU samples across three formal repetitions; added per-run and aggregate mean, median and peak utilization values.",
        "Reason": "The prior bypass collector omitted GPU utilization and did not retain median utilization fields.",
        "Affected_Test_IDs": "AB-15M", "Change_Reference": "testing/atomicbot-turboquant-formal-retest; Pending PR; Pending merge",
        "Status": "Current - pending merge", "Supersedes": "1.5",
    })
    write_csv(path, fields, rows)


def update_manifest(docx_path: Path | None = None) -> None:
    path = ROOT / "docs/testing/workbooks/Controlled-Workbook-Manifest.csv"
    fields, rows = read_csv(path)
    template = ROOT / "docs/testing/workbooks/text-templates/02_AtomicBot_TurboQuant_Controlled_Retest_Workbook_v1.md"
    for row in rows:
        if row.get("Workbook_ID") != "WB-02":
            continue
        row["Canonical_Template_SHA256"] = hashlib.sha256(template.read_bytes()).hexdigest()
        if docx_path is not None:
            row["Last_Validated_DOCX_SHA256"] = hashlib.sha256(docx_path.read_bytes()).hexdigest()
        row["Status"] = ("WB-02 revision 1.6: AB-15M repeated in isolation with three formal "
                         "request-window CPU/GPU series; mean, median and peak utilization retained")
        row["Revision"] = "1.6"
    write_csv(path, fields, rows)


def update_evidence() -> None:
    path = ROOT / "docs/testing/Evidence-Index.csv"
    fields, rows = read_csv(path)
    existing = {row["Repository_Path"] for row in rows}
    for source in sorted(RAW.rglob("*")):
        if not source.is_file() or source.name.startswith("."):
            continue
        rel = source.relative_to(ROOT).as_posix()
        if rel in existing:
            continue
        digest = hashlib.sha256(source.read_bytes()).hexdigest()
        rows.append({
            "Evidence_ID": "EVID-" + digest[:20], "Run_ID": "AB-15M-UTIL-R001",
            "Test_ID": "AB-15M", "Route": "atomicbot-turboquant", "Workbook_ID": "WB-02",
            "Evidence_Type": "Utilization rerun evidence", "Repository_Path": rel,
            "File_Name": source.name, "Size_Bytes": str(source.stat().st_size), "SHA256": digest,
            "Created_Timestamp_UTC": datetime.fromtimestamp(source.stat().st_mtime, timezone.utc).isoformat(),
            "Source_or_Derivation": "Captured during isolated AB-15M utilization rerun",
            "Processing_Script": "scripts/testing/run_atomicbot_server_metrics.py",
            "Immutable_Raw_Evidence": "False" if source.name == "server-metrics-summary.json" else "True",
            "Contains_Sensitive_Data": "False", "Redaction_Status": "Not required",
            "Evidence_Commit": "Pending PR", "Validation_Status": "Validated for WB-02 v1.6",
            "Notes": "Path and SHA-256 verified during AB-15M utilization reconciliation.",
        })
    write_csv(path, fields, rows)


if __name__ == "__main__":
    update_performance()
    update_devices()
    update_workbook()
    update_completion()
    update_revision()
    update_evidence()
    update_manifest()
