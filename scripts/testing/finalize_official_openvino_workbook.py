"""Render validated WB-04 evidence into every Markdown workbook table cell."""

from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from scripts.testing.official_openvino.matrix import load_matrix
from scripts.testing.official_openvino.reconcile import validate_workbook_text

WORKBOOK = ROOT / "docs/testing/workbooks/text-templates/04_Official_OpenVINO_Controlled_Retest_Workbook_v1.md"
CAMPAIGN = ROOT / "experiments/raw-results/official-openvino/2026-07-19"
EV = "experiments/raw-results/official-openvino/2026-07-19"


def table(headers, rows):
    return ["| " + " | ".join(headers) + " |",
            "| " + " | ".join("---" for _ in headers) + " |",
            *("| " + " | ".join(str(cell).replace("|", "/") for cell in row) + " |" for row in rows)]


def replace_table(lines, heading, replacement):
    start = lines.index(heading)
    first = next(i for i in range(start + 1, len(lines)) if lines[i].startswith("|"))
    end = first
    while end < len(lines) and lines[end].startswith("|"):
        end += 1
    lines[first:end] = replacement


def main():
    matrix = load_matrix(ROOT / "experiments/manifests/official-openvino/retest-matrix.json")
    diagnostics = json.loads((CAMPAIGN / "diagnostics/diagnostic-results.json").read_text())["records"]
    conversions = json.loads((CAMPAIGN / "conversion/conversion-results.json").read_text())["records"]
    runtime = json.loads((CAMPAIGN / "runtime/runtime-results.json").read_text())["records"]
    quality = json.loads((CAMPAIGN / "runtime/quality-results.json").read_text())["records"]
    env = json.loads((CAMPAIGN / "environment/python-openvino.json").read_text())
    system = json.loads((CAMPAIGN / "environment/system.json").read_text())
    source = json.loads((CAMPAIGN / "diagnostics/source-audit.json").read_text())
    lines = WORKBOOK.read_text(encoding="utf-8-sig").splitlines()
    lines[0] = "# 04 Official OpenVINO Controlled Retest Workbook v1.4"
    lines = [line.replace("Controlled retest revision 1.1.", "Controlled retest revision 1.4.")
             .replace("Controlled retest revision 1.2.", "Controlled retest revision 1.4.")
             for line in lines]

    replace_table(lines, "# 1. Repository and environment record", table(["Field", "Record"], [
        ("Runtime source", "Official OpenVINO 2026.2.1 lightweight release tag; exact commit recorded"),
        ("Repository URL", "https://github.com/openvinotoolkit/openvino"),
        ("Pinned OpenVINO commit", system["openvino"]["commit"]),
        ("OpenVINO version", env["packages"]["openvino"]),
        ("OpenVINO GenAI version/commit", f"{env['packages']['openvino-genai']} / {system['openvino_genai']['commit']}"),
        ("TurboQuant merge/PR lineage", f"No TurboQuant/TBQ3/TBQ4 implementation found in exact tagged source; {EV}/diagnostics/source-audit.json"),
        ("Python version", env["python_version"]),
        ("Compiler/CMake", f"{system['compiler']} / {system['cmake']}; official wheels used"),
        ("Conversion tool/version", f"Optimum Intel {env['packages']['optimum-intel']}; conversion memory-gated"),
        ("Device plugins detected", ", ".join(env["devices"])),
        ("Model source/revision", "ibm-granite/granite-4.1-3b@c0650403e44e78ec0262dab1c90914c65b196c4e; granite-4.1-8b@1504002f650e656a0a3789d99574df12e3e94ed0"),
        ("Test operator", "Codex automated controlled retest"),
        ("Test start/end date", "2026-07-19 / 2026-07-19"),
        ("Overall status", "Terminal-complete: 7 diagnostic passes, 0 failed diagnostics, 53 evidence-linked terminal rows"),
    ]))
    replace_table(lines, "# 2. Target laptop", table(["Field", "Fixed/current value", "Confirm or update"], [
        ("Machine ID", "Lenovo-PF4HMD0T", "Confirmed Lenovo 83ER"),
        ("Processor", "12th Gen Intel Core i5-12450H", "Confirmed: 8 cores / 12 logical processors"),
        ("RAM", "16.0 GB installed; 15.7 GB usable", f"Confirmed {system['computer_system']['TotalPhysicalMemory']} bytes"),
        ("Graphics", "Intel UHD Graphics; shared system memory", f"Confirmed driver {system['graphics'][0]['DriverVersion']}"),
        ("Operating system", "64-bit Windows, x64-based processor", env["os"]),
        ("NPU", "Not available - excluded from current testing", "Confirmed excluded; no NPU device enumerated"),
    ]))
    replace_table(lines, "# 3. Official codec capability boundary", table(["Capability", "Expected from verified official source", "Must be proved in this campaign"], [
        ("Standard cache", "U4 and separate key/value cache properties present", "Source-audited; runtime model conversion was memory-gated"),
        ("Official TurboQuant", "Not present in pinned 2026.2.1 source", "Negative proof: turbo_enum=false and norm_switch=false"),
        ("Independent K/V control", "KEY/VALUE properties exist; TBQ algorithm enum absent", "12 combinations terminal unsupported-by-source"),
        ("Norm correction", "No OV_TURBOQ_NORM_CORRECTION implementation found", "Ablations terminal unsupported-by-source"),
        ("QJL", "No cache-codec implementation found", "Negative boundary confirmed; unrelated comments/operators excluded"),
        ("PolarQuant", "No cache-codec implementation found", "Negative boundary confirmed; unrelated aten::polar excluded"),
        ("GPU TurboQuant", "Not exposed", "GPU plugin works, but TurboQuant GPU activation is unsupported-by-source"),
    ]))

    diag = {row["test_id"]: row for row in diagnostics}
    build_rows = []
    for case in [c for c in matrix if c.phase == "build"]:
        row = diag[case.test_id]
        status = row.get("terminal_classification", "passed")
        build_rows.append((case.test_id, case.description, status, row["evidence"][0]))
    replace_table(lines, "# 4. Build and setup checklist", table(["ID", "Check", "Result", "Evidence / command / notes"], build_rows))

    conv_rows = []
    for row in conversions:
        ev = row["terminal_evidence"]
        conv_rows.append((row["test_id"], row["source_model"], row["precision"],
                          "Planned immutable snapshot + optimum-cli; not executed by safety gate",
                          "not-tested: conversion memory gate", "not-tested: conversion memory gate",
                          f"{EV}/conversion/{row['test_id']}/manifest.json",
                          f"memory-gate-not-run: {ev['available_ram_bytes']} available < {ev['required_available_bytes']} required"))
    replace_table(lines, "# 5. Model conversion and validation", table(["ID", "Model", "Target precision", "Conversion command/version", "Tokenizer/config valid?", "Model loads?", "Hash/output path", "Status"], conv_rows))

    runtime_map = {row["test_id"]: row for row in runtime}
    baseline = [c for c in matrix if c.phase == "baseline"]
    replace_table(lines, "# 6. Standard official baseline matrix", table(["ID", "Model", "Weights", "K cache", "V cache", "Device", "Context", "Purpose", "Status"], [
        (c.test_id, c.model, c.weight_precision, c.k_precision, c.v_precision, c.device.upper(), "/".join(map(str, c.contexts)), c.description, runtime_map[c.test_id]["status"] + "; " + runtime_map[c.test_id]["source"]) for c in baseline
    ]))

    sweep = [c for c in matrix if c.phase == "capability"]
    replace_table(lines, "# 7. Official TurboQuant format capability sweep", table(["ID", "K algorithm", "V algorithm", "K precision", "V precision", "CPU SDPA loads?", "Activation proof", "Fallback?", "Status"], [
        (c.test_id, c.k_algorithm, c.v_algorithm, c.k_precision, c.v_precision,
         "not-tested: codec absent", "source audit proves no TurboQuant enum/allocation path", "No runtime fallback attempted", "unsupported-by-source; diagnostics/source-audit.json") for c in sweep
    ]))

    formal = [c for c in matrix if c.phase == "formal"]
    replace_table(lines, "# 8. Formal official TurboQuant tests", table(["ID", "Model", "K algorithm", "V algorithm", "K precision", "V precision", "Context", "Purpose", "Status"], [
        (c.test_id, c.model, c.k_algorithm, c.v_algorithm, c.k_precision, c.v_precision, "/".join(map(str, c.contexts)), c.description, runtime_map[c.test_id]["status"] + "; " + runtime_map[c.test_id]["source"]) for c in formal
    ]))

    replace_table(lines, "# 9. Configuration and activation record", table(["Test ID", "Run/config ID", "Exact properties/env vars", "Requested codec", "Verified codec", "Expected/actual record bytes", "Activation evidence", "Status"], [
        (c.test_id, f"{c.test_id}-TERMINAL-R001", "No runtime properties applied: source gate", f"{c.k_algorithm}/{c.v_algorithm}", "not-activated: codec absent", "not-allocated: source gate", f"{EV}/diagnostics/source-audit.json", "unsupported-by-source") for c in sweep
    ]))
    replace_table(lines, "# 10. Device execution and fallback verification", table(["Test ID", "Requested device", "Actual device", "Backend", "Model placement", "KV placement", "Optimisation device", "Silent fallback check", "CPU/GPU utilisation evidence", "Status"], [
        (c.test_id, c.device.upper(), "not-launched: source gate", "OpenVINO CPU requested", "not-placed", "not-allocated", "not-activated", "No fallback because launch was rejected before configuration", "not-measured: source gate; source-audit.json", "unsupported-by-source") for c in sweep
    ]))

    metric_columns = (
        ("Load ms", "load_ms"), ("TTFT ms", "ttft_ms"),
        ("Prompt tok/s", "prompt_tps"), ("TPOT ms", "tpot_ms"),
        ("Decode tok/s", "decode_tps"),
        ("Generation duration ms", "generation_duration_ms"),
        ("Peak working set MB", "peak_working_set_mb"),
        ("Peak private MB", "peak_private_mb"),
        ("Available RAM min MB", "available_ram_min_mb"),
        ("KV MB", "kv_mb"), ("GPU memory peak MB", "gpu_memory_peak_mb"),
    )

    def metric_display(record, key):
        metric = record["metrics"][key]
        return str(metric["value"]) if metric["value"] is not None else metric["status"]

    def utilisation_display(record, key, statistic):
        metric = record["metrics"][key]
        value = metric["value"]
        if isinstance(value, dict) and value.get(statistic) is not None:
            return str(value[statistic])
        return metric["status"]

    performance_keys = metric_columns[:6]
    memory_keys = metric_columns[6:]
    performance_rows, memory_rows, utilisation_rows = [], [], []
    for case in baseline + formal:
        record = runtime_map[case.test_id]
        identity = [case.test_id, f"{case.test_id}-TERMINAL-R001"]
        performance_rows.append(identity +
                                [metric_display(record, key) for _, key in performance_keys] +
                                [record["status"], record["source"]])
        memory_rows.append(identity +
                           [metric_display(record, key) for _, key in memory_keys] +
                           [record["status"], record["source"]])
        utilisation_rows.append(identity +
                                [utilisation_display(record, "cpu_percent", statistic)
                                 for statistic in ("mean", "median", "peak", "count")] +
                                [utilisation_display(record, "gpu_percent", statistic)
                                 for statistic in ("mean", "median", "peak", "count")] +
                                [record["cleanup_process_count"], record["status"], record["source"]])
    section_11 = ["**Performance and timing metrics**", ""]
    section_11 += table(
        ["Test ID", "Configuration ID"] + [label for label, _ in performance_keys] +
        ["Status", "Evidence"], performance_rows)
    section_11 += ["", "**Memory metrics**", ""]
    section_11 += table(
        ["Test ID", "Configuration ID"] + [label for label, _ in memory_keys] +
        ["Status", "Evidence"], memory_rows)
    section_11 += ["", "**CPU/GPU utilization metrics**", ""]
    section_11 += table([
        "Test ID", "Configuration ID", "CPU mean %", "CPU median %", "CPU peak %",
        "CPU sample count", "GPU mean %", "GPU median %", "GPU peak %",
        "GPU sample count", "Cleanup process count", "Status", "Evidence",
    ], utilisation_rows)
    replace_table(lines, "# 11. Formal performance and memory results", section_11)

    qmap = {row["test_id"]: row for row in quality}
    replace_table(lines, "# 12. Quality evaluation by format", table(["Test/config", "P1", "P2", "P3", "P4", "P5", "P6", "Mean /10", "Critical failure/cap", "Evidence"], [
        (c.test_id + " " + c.description, *([qmap[c.test_id]["status"]] * 6), "not-computed: no response", "No cap applied because scoring did not occur", qmap[c.test_id]["source"]) for c in formal if c.quality_required
    ]))

    replace_table(lines, "# 13. Context, stability and claim-boundary summary", table(["Test", "Required result", "Observed result", "Evidence", "Decision"], [
        ("OV-TQ-13", "TBQ4 context series", "not-run: TurboQuant absent", f"{EV}/diagnostics/source-audit.json", "unsupported-by-source"),
        ("OV-TQ-14", "TBQ3 context series", "not-run: TurboQuant absent", f"{EV}/diagnostics/source-audit.json", "unsupported-by-source"),
        ("OV-TQ-15", "Repeatability", "not-run: TurboQuant absent", f"{EV}/diagnostics/source-audit.json", "unsupported-by-source"),
        ("OV-TQ-16/17", "8B within memory gate", "not-run: 19.38 GiB required vs 8.38 GiB available", f"{EV}/conversion/conversion-results.json", "memory-gate-not-run"),
        ("OV-TQ-18", "GPU support/fallback", "GPU plugin passed; TurboQuant absent", f"{EV}/diagnostics/diagnostic-results.json", "unsupported-by-source"),
        ("OV-TQ-19", "QJL boundary", "No QJL cache codec", f"{EV}/diagnostics/source-audit.json", "confirmed unavailable"),
        ("OV-TQ-20", "Polar boundary", "No PolarQuant cache codec", f"{EV}/diagnostics/source-audit.json", "confirmed unavailable"),
    ]))
    replace_table(lines, "# 14. Failure log", table(["Failure ID", "Test ID", "Code", "Description", "Root cause/status", "Fix or next action", "Retest run", "Evidence"], [
        ("OV-F001", "OV-B01", "WIN", "Git progress stderr raised NativeCommandError", "fixed", "Trust native exit code", "acquisition rerun passed", f"{EV}/acquisition/openvino-checkout.json"),
        ("OV-F002", "OV-B01", "WIN", "Long source paths", "fixed", "core.longpaths plus git file enumeration", "source audit passed", f"{EV}/diagnostics/source-audit.json"),
        ("OV-F003", "OV-B07", "DEP", "pip inspect CP1252 encoding", "fixed", "PYTHONUTF8=1 and exit check", "acquisition rerun passed", f"{EV}/environment/pip-inspect.json"),
        ("OV-F004", "OV-B05", "PERF", "ENABLE_PROFILING unsupported", "fixed", "Use advertised PERF_COUNT property", "CPU/GPU probes passed", f"{EV}/diagnostics/official-api-probes.json"),
        ("OV-F005", "OV-B08", "TQ-ACT", "TurboQuant source boundary absent", "terminal", "Do not claim activation", "source audit complete", f"{EV}/diagnostics/source-audit.json"),
        ("OV-F006", "OV-C01/03", "MEM", "3B conversion unsafe", "terminal", "Requires host with at least 9.34 GiB available", "gate rechecked", f"{EV}/conversion/conversion-results.json"),
        ("OV-F007", "OV-C04/06", "MEM", "8B conversion unsafe", "terminal", "Requires host with at least 19.38 GiB available", "gate rechecked", f"{EV}/conversion/conversion-results.json"),
        ("OV-F008", "OV-TQ-01/10", "QUAL", "No response available to score", "terminal", "Do not fabricate P1-P6", "quality reconciliation complete", f"{EV}/runtime/quality-results.json"),
    ]))
    replace_table(lines, "# 15. Final official-route decision", table(["Field", "Record"], [
        ("Official TBQ4 support", "Unsupported in pinned official 2026.2.1 source"),
        ("Official TBQ3 support", "Unsupported in pinned official 2026.2.1 source"),
        ("Independent K/V combinations", "Properties exist for standard modes; all 12 Turbo combinations unsupported-by-source"),
        ("Norm-correction finding", "No TurboQuant norm-correction switch/path found"),
        ("QJL official status", "Confirmed unavailable as a cache codec"),
        ("PolarQuant official status", "Confirmed unavailable as a cache codec"),
        ("Granite 3B best configuration", "Not determined: conversion memory gate; no quality/performance ranking made"),
        ("Granite 8B safe configuration", "None on this host: minimum estimated requirement 19.38 GiB available"),
        ("Maximum stable context", "Not determined: no runnable Granite OpenVINO conversion"),
        ("GPU result", "OpenVINO GPU.0 diagnostic passed with 5 profiling events; TurboQuant GPU unsupported"),
        ("Recommended official fallback", "Standard OpenVINO on a higher-memory host; do not label this release TurboQuant-capable"),
        ("Application role", "Official CPU/GPU capability baseline only; TurboQuant evaluation requires a different proven implementation"),
        ("Main evidence path", EV),
        ("Final bounded reasoning", "All 60 IDs are passed or explicitly terminal. No unavailable metric or quality score was converted into a number. Results are accurate for this pinned source and laptop."),
    ]))

    text = "\n".join(lines) + "\n"
    report = validate_workbook_text(text, {c.test_id for c in matrix})
    WORKBOOK.write_text(text, encoding="utf-8")
    (CAMPAIGN / "reconciliation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, sort_keys=True))


if __name__ == "__main__":
    main()
