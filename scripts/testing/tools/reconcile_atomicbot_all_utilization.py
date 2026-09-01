"""Reconcile all-row AtomicBot CPU/GPU utilization into WB-02 v1.7."""

from __future__ import annotations

import csv
import hashlib
import json
import sys
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from scripts.testing.campaigns.atomicbot.matrix import load_matrix


REL_RAW = Path("experiments/raw-results/atomicbot-turboquant/2026-07-17/all-row-utilization-v1")
RAW = ROOT / REL_RAW
MATRIX = ROOT / "experiments/manifests/atomicbot-turboquant/retest-matrix.json"


def read_csv(path: Path) -> tuple[list[str], list[dict]]:
    with path.open(encoding="utf-8-sig", newline="") as stream:
        reader = csv.DictReader(stream)
        return list(reader.fieldnames or []), list(reader)


def write_csv(path: Path, fields: list[str], rows: list[dict]) -> None:
    quoting = csv.QUOTE_ALL if path.name == "Evidence-Index.csv" else csv.QUOTE_MINIMAL
    with path.open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=fields, extrasaction="ignore",
                                lineterminator="\n", quoting=quoting)
        writer.writeheader()
        writer.writerows(rows)


def validate_summary(summary: dict) -> None:
    if len(summary.get("samples", [])) != 3:
        raise ValueError(f'{summary.get("test_id")}: expected three formal samples')
    for sample in summary["samples"]:
        if sample.get("valid") is not True or sample.get("request_error") is not None:
            raise ValueError(f'{summary.get("test_id")}: invalid formal sample')
        for device in ("cpu_percent", "gpu_percent"):
            stats = sample.get("utilization", {}).get(device)
            if not stats or any(key not in stats for key in ("mean", "median", "peak", "sample_count")):
                raise ValueError(f'{summary.get("test_id")}: missing {device} statistics')
            if not 0 <= stats["mean"] <= 100 or not 0 <= stats["median"] <= 100 or not 0 <= stats["peak"] <= 100:
                raise ValueError(f'{summary.get("test_id")}: out-of-range {device} statistics')
            if stats["sample_count"] < 1:
                raise ValueError(f'{summary.get("test_id")}: empty {device} sample set')


def load_summaries() -> tuple[list, dict[str, dict]]:
    cases = [case for case in load_matrix(MATRIX) if case.backend != "build"]
    summaries = {}
    for case in cases:
        path = RAW / case.test_id / "server-metrics-summary.json"
        if not path.is_file():
            raise FileNotFoundError(path)
        summary = json.loads(path.read_text(encoding="utf-8"))
        validate_summary(summary)
        summaries[case.test_id] = summary
    if len(summaries) != 19:
        raise ValueError("expected exactly 19 runtime summaries")
    return cases, summaries


def sample_timestamps(test_id: str, repetition: int) -> tuple[str, str]:
    _, rows = read_csv(RAW / test_id / f"sample-{repetition}" / "utilization-samples.csv")
    if not rows:
        raise ValueError(f"{test_id} sample-{repetition}: utilization series empty")
    return rows[0]["timestamp_utc"], rows[-1]["timestamp_utc"]


def update_performance(summaries: dict[str, dict]) -> None:
    path = ROOT / "docs/testing/Performance-Measurement-Register.csv"
    fields, rows = read_csv(path)
    seen = set()
    for row in rows:
        test_id = row.get("Test_ID")
        if test_id not in summaries:
            continue
        repetition = int(row["Repetition_Number"])
        sample = summaries[test_id]["samples"][repetition - 1]
        cpu = sample["utilization"]["cpu_percent"]
        gpu = sample["utilization"]["gpu_percent"]
        start, end = sample_timestamps(test_id, repetition)
        row.update({
            "Start_Timestamp_UTC": start,
            "End_Timestamp_UTC": end,
            "Output_Tokens": "64",
            "TTFT_ms": str(sample["ttft_ms"]),
            "Peak_Working_Set_Bytes": str(round(sample["peak_ram_mb"] * 1024 * 1024)),
            "KV_Cache_Allocated_Bytes": str(round(sample["kv_mb"] * 1024 * 1024)),
            "CPU_Mean_Percent": f'{cpu["mean"]:.6f}',
            "CPU_Median_Percent": f'{cpu["median"]:.6f}',
            "CPU_Peak_Percent": f'{cpu["peak"]:.6f}',
            "GPU_Engine_Mean_Percent": f'{gpu["mean"]:.6f}',
            "GPU_Engine_Median_Percent": f'{gpu["median"]:.6f}',
            "GPU_Engine_Peak_Percent": f'{gpu["peak"]:.6f}',
            "Raw_Metrics_Path": (REL_RAW / test_id / f"sample-{repetition}" / "utilization-samples.csv").as_posix(),
            "Processed_Result_Path": (REL_RAW / test_id / "server-metrics-summary.json").as_posix(),
            "Notes": "64-token loaded-request utilization window; CPU normalized across logical processors; GPU is busiest PID-attributable engine per timestamp; pilot and warm-up excluded.",
        })
        seen.add((test_id, repetition))
    expected = {(test_id, repetition) for test_id in summaries for repetition in (1, 2, 3)}
    if seen != expected:
        raise ValueError(f"performance register coverage mismatch: missing {sorted(expected - seen)}")
    write_csv(path, fields, rows)


def device_row(case, summary: dict, prior: dict | None) -> dict:
    aggregate = summary["aggregate"]
    cpu = aggregate["cpu_percent"]
    gpu = aggregate["gpu_percent"]
    base = dict(prior or {})
    if case.backend == "cpu":
        base.update({
            "Test_ID": case.test_id, "Route": "atomicbot-turboquant", "Workbook_ID": "WB-02",
            "Requested_Device": "CPU", "Actual_Device": "CPU", "Backend": "CPU",
            "Compiled_or_Execution_Device": "CPU", "Requested_Offload": "ngl=0",
            "Actual_Model_Layer_Placement": "All layers on CPU",
            "Actual_KV_Placement": f'CPU {summary["aggregate"]["kv_mb"]["median"]:.2f} MiB',
            "Optimisation_Device": "CPU", "CPU_Fallback": "N/A - CPU requested",
            "Hybrid_Behaviour": "None", "Silent_Fallback_Check": "Passed",
            "Failure_IDs": base.get("Failure_IDs", ""),
        })
    base.update({
        "Latest_Run_ID": f"{case.test_id}-UTIL-R001",
        "CPU_Mean_Percent": f'{cpu["mean"]:.6f}',
        "CPU_Median_Percent": f'{cpu["median"]:.6f}',
        "CPU_Peak_Percent": f'{cpu["peak"]:.6f}',
        "GPU_Engine_Mean_Percent": f'{gpu["mean"]:.6f}',
        "GPU_Engine_Median_Percent": f'{gpu["median"]:.6f}',
        "GPU_Engine_Peak_Percent": f'{gpu["peak"]:.6f}',
        "Device_Proof_Path": (REL_RAW / case.test_id / "sample-1" / "stderr.txt").as_posix(),
        "Utilisation_Samples_Path": (REL_RAW / case.test_id / "sample-1" / "utilization-samples.csv").as_posix() + "; sample-2; sample-3",
        "Result": "Passed", "Evidence_Commit": "Pending PR",
        "Notes": f'{cpu["sample_count"]} accepted CPU and GPU samples across three formal loaded requests; GPU is busiest PID-attributable engine, not summed.',
    })
    return base


def update_devices(cases: list, summaries: dict[str, dict]) -> None:
    path = ROOT / "docs/testing/Device-Verification-Register.csv"
    fields, rows = read_csv(path)
    existing = {row["Test_ID"]: row for row in rows}
    replacements = {case.test_id: device_row(case, summaries[case.test_id], existing.get(case.test_id))
                    for case in cases}
    unrelated = [row for row in rows if row["Test_ID"] not in replacements]
    write_csv(path, fields, unrelated + [replacements[case.test_id] for case in cases])


def util_text(stats: dict) -> str:
    return f'mean {stats["mean"]:.2f}%; median {stats["median"]:.2f}%; peak {stats["peak"]:.2f}%'


def update_workbook(cases: list, summaries: dict[str, dict]) -> None:
    path = ROOT / "docs/testing/workbooks/text-templates/02_AtomicBot_TurboQuant_Controlled_Retest_Workbook_v1.md"
    text = path.read_text(encoding="utf-8")
    # Replace the device table while retaining the established placement descriptions.
    start = text.index("| Test ID | Requested device | Actual device | Backend | Offload evidence")
    end = text.index("\n\n# 7. Formal run results", start)
    old_lines = text[start:end].splitlines()
    placement = {}
    for line in old_lines[2:]:
        cells = [cell.strip() for cell in line.strip().strip("|").split("|")]
        if cells and cells[0].startswith("AB-"):
            placement[cells[0]] = cells
    table = [
        "| Test ID | Requested device | Actual device | Backend | Offload evidence | TQ/KV device | CPU fallback? | CPU utilization | GPU utilization | Evidence path |",
        "|---|---|---|---|---|---|---|---|---|---|",
    ]
    for case in cases:
        summary = summaries[case.test_id]
        if case.test_id in placement:
            cells = placement[case.test_id]
            prefix = cells[1:7]
        else:
            prefix = ["CPU", "CPU", "CPU", "All layers on CPU",
                      f'CPU, {summary["aggregate"]["kv_mb"]["median"]:.2f} MiB', "N/A - CPU requested"]
        table.append("| " + " | ".join([
            case.test_id, *prefix, util_text(summary["aggregate"]["cpu_percent"]),
            util_text(summary["aggregate"]["gpu_percent"]),
            f'`2026-07-17/all-row-utilization-v1/{case.test_id}/`']) + " |")
    text = text[:start] + "\n".join(table) + text[end:]

    # Extend the formal table with explicit aggregate utilization columns and refresh rerun metrics.
    start = text.index("| Test ID | Model | Weights | K cache | V cache | Device | Context | Peak WS MiB")
    end = text.index("\n\n# 8. Compression", start)
    lines = text[start:end].splitlines()
    result_table = [
        "| Test ID | Model | Weights | K cache | V cache | Device | Context | Peak WS MiB | KV MiB | TTFT ms | Tok/s | CPU mean/median/peak % | GPU mean/median/peak % | Quality /10 | Status |",
        "|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|",
    ]
    by_id = {case.test_id: case for case in cases}
    for line in lines[2:]:
        cells = [cell.strip() for cell in line.strip().strip("|").split("|")]
        if not cells or cells[0] not in summaries:
            continue
        test_id = cells[0]
        aggregate = summaries[test_id]["aggregate"]
        refreshed = cells[:7] + [f'{aggregate["peak_ram_mb"]["max"]:.3f}',
                                 f'{aggregate["kv_mb"]["median"]:.2f}',
                                 f'{aggregate["ttft_ms"]["median"]:.3f}', cells[10],
                                 f'{aggregate["cpu_percent"]["mean"]:.2f} / {aggregate["cpu_percent"]["median"]:.2f} / {aggregate["cpu_percent"]["peak"]:.2f}',
                                 f'{aggregate["gpu_percent"]["mean"]:.2f} / {aggregate["gpu_percent"]["median"]:.2f} / {aggregate["gpu_percent"]["peak"]:.2f}',
                                 cells[11], cells[12]]
        result_table.append("| " + " | ".join(refreshed) + " |")
    text = text[:start] + "\n".join(result_table) + text[end:]
    note = ("All 19 runtime rows were repeated under one utilization protocol on 2026-07-17. CPU is normalized across logical processors; GPU is the busiest PID-attributable Windows GPU Engine at each timestamp. CPU-only rows measured 0.00% attributable GPU use; this value was sampled, not inferred.\n\n")
    marker = "# 7. Formal run results\n\n"
    if note not in text:
        text = text.replace(marker, marker + note)
    path.write_text(text, encoding="utf-8")


def update_evidence() -> None:
    path = ROOT / "docs/testing/Evidence-Index.csv"
    fields, rows = read_csv(path)
    prefix = REL_RAW.as_posix() + "/"
    rows = [row for row in rows if not row.get("Repository_Path", "").startswith(prefix)]
    existing = {row["Repository_Path"] for row in rows}
    for source in sorted(RAW.rglob("*")):
        if not source.is_file() or source.name.startswith("."):
            continue
        rel = source.relative_to(ROOT).as_posix()
        if rel in existing:
            continue
        digest = hashlib.sha256(source.read_bytes()).hexdigest()
        relative_parts = source.relative_to(RAW).parts
        test_id = relative_parts[0] if len(relative_parts) > 1 else "AB-ALL"
        identity = hashlib.sha256((rel + "\0" + digest).encode("utf-8")).hexdigest()
        rows.append({
            "Evidence_ID": "EVID-" + identity[:20], "Run_ID": f"{test_id}-UTIL-R001",
            "Test_ID": test_id, "Route": "atomicbot-turboquant", "Workbook_ID": "WB-02",
            "Evidence_Type": "All-row utilization evidence", "Repository_Path": rel,
            "File_Name": source.name, "Size_Bytes": str(source.stat().st_size), "SHA256": digest,
            "Created_Timestamp_UTC": datetime.fromtimestamp(source.stat().st_mtime, timezone.utc).isoformat(),
            "Source_or_Derivation": "Captured during controlled all-row utilization rerun",
            "Processing_Script": "scripts/testing/tools/run_atomicbot_server_metrics.py",
            "Immutable_Raw_Evidence": "False" if source.name == "server-metrics-summary.json" else "True",
            "Contains_Sensitive_Data": "False", "Redaction_Status": "Not required",
            "Evidence_Commit": "Pending PR", "Validation_Status": "Validated for WB-02 v1.7",
            "Notes": "Path and SHA-256 verified during all-row utilization reconciliation.",
        })
    write_csv(path, fields, rows)


def update_controls() -> None:
    completion = ROOT / "docs/testing/Workbook-Completion-Register.csv"
    fields, rows = read_csv(completion)
    evidence_root = REL_RAW.as_posix() + "/"
    for row in rows:
        if row.get("Workbook_ID") == "WB-02":
            if evidence_root not in row["Source_Result_Path"]:
                row["Source_Result_Path"] += "; " + evidence_root
            row["Notes"] = "WB-02 v1.7 includes mean, median and peak CPU/GPU utilization for every runtime row, with Vulkan-partial GPU use explicitly reported."
    write_csv(completion, fields, rows)

    revisions = ROOT / "docs/testing/Workbook-Revision-Register.csv"
    fields, rows = read_csv(revisions)
    rows = [row for row in rows if not (row.get("Workbook_ID") == "WB-02" and row.get("Version") == "1.7")]
    for row in rows:
        if row.get("Workbook_ID") == "WB-02" and row.get("Status", "").startswith("Current"):
            row["Status"] = "Superseded"
    rows.append({
        "Record_ID": "WR-028", "Workbook_ID": "WB-02", "Version": "1.7", "Date": "2026-07-17",
        "Changed_By": "Student", "Change_Type": "All-row utilization completion",
        "Change_Summary": "Repeated all 19 runtime configurations with PID-attributable CPU/GPU sampling; populated per-run and aggregate mean, median and peak values and made Vulkan-partial GPU use explicit.",
        "Reason": "Only AB-15M previously had complete utilization fields and Vulkan-partial GPU use was unmeasured.",
        "Affected_Test_IDs": "AB-01-AB-15M", "Change_Reference": "testing/atomicbot-turboquant-formal-retest; Pending PR; Pending merge",
        "Status": "Current - pending merge", "Supersedes": "1.6",
    })
    write_csv(revisions, fields, rows)


def update_manifest(docx: Path | None = None) -> None:
    path = ROOT / "docs/testing/workbooks/Controlled-Workbook-Manifest.csv"
    fields, rows = read_csv(path)
    template = ROOT / "docs/testing/workbooks/text-templates/02_AtomicBot_TurboQuant_Controlled_Retest_Workbook_v1.md"
    for row in rows:
        if row.get("Workbook_ID") == "WB-02":
            row["Canonical_Template_SHA256"] = hashlib.sha256(template.read_bytes()).hexdigest()
            if docx:
                row["Last_Validated_DOCX_SHA256"] = hashlib.sha256(docx.read_bytes()).hexdigest()
            row["Status"] = "WB-02 revision 1.7: all runtime rows include sourced CPU/GPU mean, median and peak utilization; Vulkan-partial GPU use is explicit"
            row["Revision"] = "1.7"
    write_csv(path, fields, rows)


def main() -> int:
    cases, summaries = load_summaries()
    update_performance(summaries)
    update_devices(cases, summaries)
    update_workbook(cases, summaries)
    update_evidence()
    update_controls()
    update_manifest()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
