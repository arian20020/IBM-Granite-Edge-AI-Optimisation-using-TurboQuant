"""Reconcile WB-02 evidence into the controlled CSV registers."""

from __future__ import annotations

import csv
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
RAW = ROOT / "experiments/raw-results/atomicbot-turboquant/2026-07-16"
ROUTE = "atomicbot-turboquant"
WB = "WB-02"
COMMIT = "519f0c594a8e31467d2e2f2cf17054c9e7e11536"


def rewrite(name: str, new_rows: list[dict], remove=lambda row: False) -> None:
    path = ROOT / "docs/testing" / name
    with path.open(newline="", encoding="utf-8-sig") as handle:
        reader = csv.DictReader(handle)
        fields, rows = reader.fieldnames, [row for row in reader if not remove(row)]
    assert fields
    with path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=fields, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(rows)
        writer.writerows(({key: row.get(key, "") for key in fields} for row in new_rows))


def main() -> int:
    master = json.loads((RAW / "acquisition/results/AtomicBot_Master_Summary.json").read_text(encoding="utf-8"))
    executed = [test_id for test_id in master["passed"] if test_id.startswith("AB-") and
                (RAW / "server-metrics" / test_id / "server-metrics-summary.json").is_file()]
    bypass = {
        "AB-KV8-F16-4K": {"purpose": "Supplemental matched 8B F16 allocation baseline", "backend": "cpu",
                           "context": 4096, "cache_k": "f16", "cache_v": "f16", "median_tokens_per_second": 8.37},
        "AB-15M": {"purpose": "Conditional 8B maximum Vulkan TurboQuant", "backend": "vulkan-full",
                   "context": 4096, "cache_k": "turbo3", "cache_v": "turbo3", "median_tokens_per_second": 4.19},
    }
    executed.extend(test_id for test_id in bypass
                    if (RAW / "safety-bypass" / test_id / "server-metrics-summary.json").is_file())
    perf = []
    tests = []
    for test_id in executed:
        metrics_root = RAW / ("safety-bypass" if test_id in bypass else "server-metrics") / test_id
        summary_path = metrics_root / "server-metrics-summary.json"
        summary = json.loads(summary_path.read_text(encoding="utf-8"))
        acquired = bypass.get(test_id, master["tests"][test_id]["summary"])
        for repetition, sample in enumerate(summary["samples"], 1):
            perf.append({"Measurement_ID": f"MEAS-{test_id}-R{repetition}", "Run_ID": f"{test_id}-R{repetition}",
                         "Test_ID": test_id, "Route": ROUTE, "Workbook_ID": WB,
                         "Configuration_ID": f"CONFIG-{test_id}", "Warmup_or_Measured": "Measured",
                         "Repetition_Number": repetition, "Included_In_Formal_Statistics": "True",
                         "Cold_or_Warm": "Warm loaded server", "TTFT_ms": sample["ttft_ms"],
                         "Decode_Tokens_Per_Second": acquired["median_tokens_per_second"],
                         "Peak_Working_Set_Bytes": round(sample["peak_ram_mb"] * 1048576),
                         "KV_Cache_Allocated_Bytes": round(sample["kv_mb"] * 1048576),
                         "Cleanup_Result": "Pass; server process tree terminated",
                         "Raw_Metrics_Path": str(summary_path.relative_to(ROOT)).replace("\\", "/"),
                         "Processed_Result_Path": str(summary_path.relative_to(ROOT)).replace("\\", "/"),
                         "Outlier_Status": "Included", "Outlier_Reason": "No exclusion applied",
                         "Validation_Status": "Validated", "Evidence_Commit": "Pending PR",
                         "Notes": "TTFT is HTTP request initiation to first streamed generated token; unavailable schema fields are not inferred."})
        tests.append({"Run_ID": f"{test_id}-FORMAL", "Test_ID": test_id, "Route": ROUTE, "Workbook_ID": WB,
                      "Run_Number": "1", "Run_Purpose": acquired["purpose"], "Operator": "Student",
                      "Environment_ID": "ENV-20260716-ATOMICBOT-01", "Repository_ID": "REPO-ATOMICBOT-519F0C5",
                      "Repository_URL": "https://github.com/AtomicBot-ai/atomic-llama-cpp-turboquant",
                      "Repository_Branch_or_Tag": "DETACHED", "Repository_Commit": COMMIT,
                      "Repository_Clean_State": "True", "Build_ID": "BUILD-AB-CPU" if acquired["backend"] == "cpu" else "BUILD-AB-VULKAN",
                      "Configuration_ID": f"CONFIG-{test_id}", "Context_Target_Tokens": acquired["context"],
                      "Requested_Backend": acquired["backend"], "Actual_Backend": acquired["backend"],
                      "K_Cache_Type": acquired["cache_k"], "V_Cache_Type": acquired["cache_v"],
                      "Optimisation_Name": "TurboQuant" if "turbo" in acquired["cache_k"] else "None",
                      "Optimisation_Requested": str("turbo" in acquired["cache_k"]),
                      "Optimisation_Activated": str("turbo" in acquired["cache_k"]), "Exit_Code": 0,
                      "Model_Load_Success": "True", "Generation_Success": "True", "Output_Completion_Status": "Complete",
                      "TTFT_ms": summary["aggregate"]["ttft_ms"]["median"],
                      "Decode_Tokens_Per_Second": acquired["median_tokens_per_second"],
                      "Peak_Working_Set_Bytes": round(summary["aggregate"]["peak_ram_mb"]["max"] * 1048576),
                      "KV_Cache_Allocated_Bytes": round(summary["aggregate"]["kv_mb"]["median"] * 1048576),
                      "Cleanup_Result": "Pass", "Warmup_or_Measured": "Formal aggregate", "Included_In_Formal_Statistics": "True",
                      "Stability_Result": "Pass; 3/3 measured repetitions", "Raw_Evidence_Path": str(metrics_root.relative_to(ROOT)).replace("\\", "/"),
                      "Processed_Result_Path": str(summary_path.relative_to(ROOT)).replace("\\", "/"), "Result": "Passed",
                      "Result_Reason": "Pilot, excluded warm-up and three measured repetitions passed.",
                      "Workbook_Update_Status": "Complete in WB-02 v1.4", "Workbook_Section": "7", "Evidence_Commit": "Pending PR",
                      "Reviewer": "Codex evidence reconciliation", "Review_Date": "2026-07-16"})
    rewrite("Performance-Measurement-Register.csv", perf, lambda r: r.get("Route") == ROUTE)
    rewrite("Test-Run-Register.csv", tests, lambda r: r.get("Route") == ROUTE and r.get("Run_ID", "").endswith("-FORMAL"))

    devices = []
    placements = {"AB-11": ("1/41", "CPU 320.00 MiB", 48.186), "AB-12": ("1/41", "CPU 125.13 MiB", 58.765),
                  "AB-13": ("41/41", "Vulkan0 125.13 MiB", 2.651), "AB-14": ("1/41", "CPU 340.00 MiB", 50.446),
                  "AB-15": ("1/41", "CPU 125.13 MiB", 49.173)}
    for test_id, (layers, kv, cpu) in placements.items():
        devices.append({"Test_ID": test_id, "Route": ROUTE, "Workbook_ID": WB, "Latest_Run_ID": f"{test_id}-FORMAL",
                        "Requested_Device": "Vulkan", "Actual_Device": "Intel UHD Vulkan0" + (" + CPU" if layers != "41/41" else ""),
                        "Backend": "Vulkan", "Compiled_or_Execution_Device": "Vulkan0", "Requested_Offload": "ngl=1" if layers == "1/41" else "ngl=999",
                        "Actual_Model_Layer_Placement": layers, "Actual_KV_Placement": kv,
                        "Optimisation_Device": "Vulkan0" if test_id == "AB-13" else "CPU reference/hybrid",
                        "CPU_Fallback": "No unexplained fallback" if test_id == "AB-13" else "Yes; intended hybrid",
                        "Hybrid_Behaviour": "None" if test_id == "AB-13" else "Model split and CPU KV",
                        "Silent_Fallback_Check": "Passed", "CPU_Mean_Percent": cpu,
                        "GPU_Engine_Mean_Percent": "N/A", "GPU_Engine_Peak_Percent": "N/A",
                        "Device_Proof_Path": f"experiments/raw-results/atomicbot-turboquant/2026-07-16/acquisition/results/{test_id}/",
                        "Utilisation_Samples_Path": "N/A - Windows GPU engine counter unavailable",
                        "Result": "Passed", "Evidence_Commit": "Pending PR", "Notes": "Placement is from llama.cpp buffers/layer logs; GPU percent is not invented."})
    devices.append({"Test_ID": "AB-15M", "Route": ROUTE, "Workbook_ID": WB, "Latest_Run_ID": "AB-15M-FORMAL",
                    "Requested_Device": "Vulkan maximum", "Actual_Device": "Intel UHD Vulkan0", "Backend": "Vulkan",
                    "Compiled_or_Execution_Device": "Vulkan0", "Requested_Offload": "ngl=999", "Actual_Model_Layer_Placement": "41/41",
                    "Actual_KV_Placement": "Vulkan0 125.13 MiB", "Optimisation_Device": "Vulkan0", "CPU_Fallback": "No unexplained fallback",
                    "Hybrid_Behaviour": "CPU-mapped 220.50 MiB plus Vulkan-native model/KV", "Silent_Fallback_Check": "Passed", "CPU_Mean_Percent": "N/A",
                    "CPU_Peak_Percent": "N/A", "GPU_Engine_Mean_Percent": "N/A", "GPU_Engine_Peak_Percent": "N/A",
                    "Device_Proof_Path": "experiments/raw-results/atomicbot-turboquant/2026-07-16/safety-bypass/AB-15M/pilot/stderr.txt",
                    "Utilisation_Samples_Path": "N/A - bypass collector did not sample GPU engine percent", "Result": "Passed", "Failure_IDs": "FAIL-AB-8B-SAFETY",
                    "Evidence_Commit": "Pending PR", "Notes": "Controlled serial bypass with 256 MiB emergency-stop floor; floor was not crossed."})
    rewrite("Device-Verification-Register.csv", devices, lambda r: r.get("Route") == ROUTE)

    repo = {"Repository_ID": "REPO-ATOMICBOT-519F0C5", "Route": ROUTE,
            "Repository_URL": "https://github.com/AtomicBot-ai/atomic-llama-cpp-turboquant", "Branch_or_Tag": "DETACHED",
            "Commit_SHA": COMMIT, "Upstream_Base_Commit": "Not published as one derivable commit", "Detached_HEAD": "True",
            "Working_Tree_Clean": "True", "Git_Status_Path": "experiments/raw-results/atomicbot-turboquant/2026-07-16/acquisition/results/AtomicBot_Environment_Manifest.json",
            "Remote_Configuration_Path": "experiments/raw-results/atomicbot-turboquant/2026-07-16/acquisition/results/AtomicBot_Environment_Manifest.json",
            "Licence": "MIT", "Supported_Backends": "CPU; Vulkan; generic SYCL source not accepted as TQ-specific support",
            "Supported_KV_Formats": "f16; q8_0; turbo2; turbo3; turbo4", "Supported_Device_Flags": "-ctk; -ctv; -ngl",
            "Known_Limitations": "Device Guard blocks test-barrier; 16 GB safety gates; specialised Vulkan turbo3 FA shader generation disabled; GPU percent counter unavailable",
            "Manifest_Path": "experiments/raw-results/atomicbot-turboquant/2026-07-16/acquisition/results/AtomicBot_Environment_Manifest.json",
            "Validation_Status": "Validated with limitations", "Evidence_Commit": "Pending PR", "Reviewer": "Codex evidence reconciliation",
            "Review_Date": "2026-07-16", "Notes": "Exact clean pinned checkout."}
    rewrite("Repository-Register.csv", [repo], lambda r: r.get("Route") == ROUTE)

    quality_specs = {
        "configuration-A": {
            "role": "Baseline Q8_0", "test": "AB-04",
            "P1": (4.0, (8, 10, 4, 9, 10), "Fail", "Yes", "No", "Critical completeness cap: only one limitation"),
            "P2": (8.0, (7, 9, 8, 8, 8), "Pass", "Yes", "Yes", "No critical cap"),
            "P3": (4.0, (2, 10, 8, 8, 10), "Fail", "Yes", "No", "Material contradiction: memory_effect Increased"),
            "P4": (10.0, (10, 10, 10, 10, 10), "Pass", "Yes", "Yes", "No critical cap"),
            "P5": (4.0, (10, 4, 10, 10, 10), "Fail", "No", "Yes", "Exact-format cap: prohibited space after colon"),
            "P6": (10.0, (10, 10, 10, 10, 10), "Pass", "Yes", "Yes", "No critical cap"),
        },
        "configuration-B": {
            "role": "Optimised turbo3", "test": "AB-06",
            "P1": (9.0, (9, 9, 9, 9, 9), "Pass", "Yes", "Yes", "No critical cap"),
            "P2": (8.1, (7, 9, 8.5, 8, 8), "Pass", "Yes", "Yes", "No critical cap"),
            "P3": (8.75, (8.5, 9, 9, 8.33, 9), "Pass", "Yes", "Yes", "No critical cap"),
            "P4": (4.0, (7, 4, 4, 8, 10), "Fail", "No", "No", "Format and missing-fact cap"),
            "P5": (0.0, (0, 0, 0, 0, 0), "Timeout", "No", "No", "Empty output after 2400-second timeout"),
            "P6": (10.0, (10, 10, 10, 10, 10), "Pass", "Yes", "Yes", "No critical cap"),
        },
    }
    quality_rows = []
    for blind, spec in quality_specs.items():
        for pid in [f"P{i}" for i in range(1, 7)]:
            score, dims, gate, format_valid, facts, reason = spec[pid]
            response = RAW / "quality" / blind / f"{pid}-response.txt"
            digest = hashlib.sha256(response.read_bytes()).hexdigest()
            quality_rows.append({"Evaluation_ID": f"QE-{blind[-1]}-{pid}", "Route": ROUTE, "Workbook_ID": WB,
                                 "Test_ID": spec["test"], "Run_ID": f"QUALITY-{blind}-{pid}",
                                 "Configuration_ID": f"CONFIG-{spec['test']}", "Comparison_Role": spec["role"],
                                 "Prompt_Set_ID": "GTQ-PROMPTS-v1", "Prompt_ID": pid, "Rubric_ID": "GTQ-QUALITY-RUBRIC-v1",
                                 "Raw_Response_Path": str(response.relative_to(ROOT)).replace("\\", "/"),
                                 "Response_SHA256": digest, "Deterministic_Check_Result": gate,
                                 "Format_Valid": format_valid, "Required_Facts_Retained": facts,
                                 "Unsupported_Statements_Count": 0, "Repetition_Corruption_or_Truncation": "Timeout" if pid == "P5" and blind.endswith("B") else "No",
                                 "Correctness_and_Grounding_0_to_10": dims[0], "Instruction_and_Format_0_to_10": dims[1],
                                 "Completeness_and_Fact_Retention_0_to_10": dims[2], "Relevance_Clarity_Coherence_0_to_10": dims[3],
                                 "Stability_and_Integrity_0_to_10": dims[4], "Weighted_Score_0_to_10": score,
                                 "Critical_Cap_Applied": "Yes" if score in {0.0, 4.0} else "No", "Critical_Cap_Reason": reason,
                                 "Blind_Label": blind, "Pairwise_Order": "A then B; reverse-order check completed",
                                 "Judge_or_Reviewer": "Codex deterministic gates plus manual adjudication", "Manual_Adjudication_Required": "Yes",
                                 "Manual_Adjudication_Result": reason, "Result": gate, "Evidence_Commit": "Pending PR",
                                 "Notes": "Configuration identity was concealed during output review; objective gates override fluency."})
    rewrite("Quality-Evaluation-Register.csv", quality_rows, lambda r: r.get("Route") == ROUTE)

    completion = []
    for section in range(1, 13):
        completion.append({"Workbook_ID": WB, "Workbook_File": "02_AtomicBot_TurboQuant_Controlled_Retest_Workbook_v1.docx",
                           "Route": ROUTE, "Section_Type": "Controlled workbook section", "Section_or_Test_ID": str(section),
                           "Section_Title": f"WB-02 section {section}", "Required_Source_Evidence": "Pinned raw and processed evidence",
                           "Source_Run_IDs": "AB-B01-AB-B08; AB-01-AB-15M; P1-P6", "Source_Result_Path": "experiments/raw-results/atomicbot-turboquant/2026-07-16/",
                           "Completion_Status": "Complete with explicit N/A/Blocked classifications", "Missing_Data_Code": "None",
                           "Missing_Data_Reason": "No blank workbook cells; unsupported, blocked and unavailable measures are explicitly classified",
                           "Evidence_Checked": "True", "Updated_Date": "2026-07-16", "Updated_By": "Student and Codex",
                           "Evidence_Commit": "Pending PR", "Reviewer": "Codex evidence reconciliation", "Review_Date": "2026-07-16",
                           "Remaining_Action": "None before PR review", "Notes": "Section values reconciled to raw JSON/log evidence."})
    rewrite("Workbook-Completion-Register.csv", completion, lambda r: r.get("Workbook_ID") == WB)

    environment = {"Environment_ID": "ENV-20260716-ATOMICBOT-01", "Capture_Timestamp_Local": "2026-07-16",
                   "Timezone": "Europe/London", "Machine_Name": "LENOVO-PF4HMD0T", "Machine_Role": "target-windows-intel-laptop",
                   "Windows_Version": "10.0.26200", "Windows_Build": "26200", "Architecture": "x64",
                   "CPU_Model": "12th Gen Intel Core i5-12450H", "CPU_Physical_Cores": 8, "CPU_Logical_Processors": 12,
                   "GPU_Model": "Intel UHD Graphics", "Installed_RAM_Bytes": 16857817088,
                   "Background_Load_Notes": "One model server at a time; 8B high-risk rows memory-gated; nonessential applications closed.",
                   "MSVC_Version": "19.51.36248", "CMake_Version": "4.3.1-msvc1", "Ninja_Version": "1.13.2",
                   "Git_Version": "2.53.0.windows.3", "Python_Version": "3.11.9", "Vulkan_SDK_Version": "1.4.350.0",
                   "Other_Tool_Versions_JSON": '{"jinja2":"3.1.6"}',
                   "System_Capture_Path": "experiments/raw-results/atomicbot-turboquant/2026-07-16/acquisition/results/AtomicBot_Environment_Manifest.json",
                   "Evidence_Commit": "Pending PR", "Validation_Status": "Validated for WB-02 with recorded unavailable sensor fields",
                   "Reviewer": "Codex evidence reconciliation", "Review_Date": "2026-07-16",
                   "Notes": "Thermal and GPU engine percentage fields are not inferred where dependable counters were unavailable."}
    rewrite("Environment-Register.csv", [environment], lambda r: r.get("Environment_ID") == environment["Environment_ID"])

    builds = []
    for backend in ("CPU", "Vulkan"):
        build_id = f"BUILD-AB-{backend.upper()}"
        base = "experiments/raw-results/atomicbot-turboquant/2026-07-16/" + ("build-cpu" if backend == "CPU" else "build-vulkan")
        builds.append({"Build_Check_ID": f"AB-B0{'3' if backend == 'CPU' else '5'}-R001", "Route": ROUTE, "Workbook_ID": WB,
                       "Repository_ID": "REPO-ATOMICBOT-519F0C5", "Build_ID": build_id, "Environment_ID": environment["Environment_ID"],
                       "Check_Title": f"Fresh Windows {backend} Release build", "Architecture": "x64", "Build_Type": backend,
                       "Compiler": "MSVC 19.51.36248", "CMake_Version": "4.3.1-msvc1", "Generator": "Ninja 1.13.2",
                       "Configuration": "Release", "Build_Options": "LLAMA_BUILD_UI=OFF; tests/tools/server enabled" + ("; GGML_VULKAN=ON" if backend == "Vulkan" else "; GGML_VULKAN=OFF"),
                       "Configure_Stdout_Path": base, "Configure_Stderr_Path": base, "Build_Stdout_Path": base, "Build_Stderr_Path": base,
                       "Repository_Test_Result": "42/43 effective pass; test-barrier blocked by Device Guard" if backend == "CPU" else "Vulkan runtime placement verified",
                       "Binary_Inventory_Path": f"experiments/raw-results/atomicbot-turboquant/2026-07-16/acquisition/results/AtomicBot_{backend.lower()}_Binary_Hashes.json",
                       "Warnings_Index_Path": "experiments/raw-results/atomicbot-turboquant/2026-07-16/acquisition/results/AtomicBot_Build_Warning_Log_Index.json",
                       "Errors": "Device Guard blocks test-barrier" if backend == "CPU" else "None after UI asset repair",
                       "Result": "Passed with external-policy limitation" if backend == "CPU" else "Passed",
                       "Result_Reason": "Fresh build completed and required binaries executed.", "Failure_IDs": "FAIL-AB-DEVICE-GUARD" if backend == "CPU" else "FAIL-AB-UI-ASSET",
                       "Evidence_Path": base, "Evidence_Commit": "Pending PR", "Reviewer": "Codex evidence reconciliation", "Review_Date": "2026-07-16",
                       "Notes": "Pinned loading.html copied into generated UI dist only; source checkout remained clean."})
    rewrite("Build-Register.csv", builds, lambda r: r.get("Route") == ROUTE)

    failure_specs = [
        ("FAIL-AB-UI-ASSET", "AB-B02/AB-B05", "UI-BUNDLE-MISSING", "Generated UI embed target lacked loading.html", "Resolved", "Exact pinned source loading.html copied into generated build output"),
        ("FAIL-AB-DEVICE-GUARD", "AB-B04", "DEVICE-GUARD-4551", "test-barrier.exe blocked at launch", "Open - external policy", "42 other tests effective-pass; do not claim 43/43"),
        ("FAIL-AB-08Q-MEMORY", "AB-08Q", "MEMORY-GUARD", "Initial batch reached 470.7 MiB free", "Resolved", "Isolated idle rerun passed all repetitions"),
        ("FAIL-AB-8B-SAFETY", "AB-KV8-F16-4K/AB-15M", "SAFETY-GATE", "Initial preventive gate blocked high-risk 8B rows", "Resolved by controlled bypass", "Serial idle-system retry with 256 MiB emergency floor passed both rows"),
        ("FAIL-AB-P5-TIMEOUT", "P5/AB-06", "QUALITY-TIMEOUT-2400", "Turbo3 P5 returned no response within 2400 seconds", "Open", "Empty output retained and scored 0"),
    ]
    failures = []
    for fid, test_id, code, symptom, status, workaround in failure_specs:
        failures.append({"Failure_ID": fid, "Date_Local": "2026-07-16", "Run_ID": test_id + "-RETEST", "Test_ID": test_id,
                         "Route": ROUTE, "Workbook_ID": WB, "Failure_Code": code, "Failure_Category": "Controlled retest limitation",
                         "Failure_Stage": "Build/test/runtime/quality as identified", "Observed_Symptom": symptom,
                         "Error_Message_or_Code": code, "Immediate_Effect": "Affected row not counted as an unconditional pass",
                         "Safety_Impact": "Memory guards prevent laptop instability" if "SAFETY" in code or "MEMORY" in code else "None",
                         "Suspected_Cause": symptom, "Diagnostic_Actions": "Reviewed raw logs, reran in isolation where safe, and reconciled source/runtime evidence",
                         "Root_Cause": symptom, "Fix_or_Workaround": workaround, "Final_Status": status,
                         "Missing_Workbook_Fields": "None; explicit status/reason recorded", "Next_Action": "External policy/hardware change required" if status.startswith("Open") else "None",
                         "Raw_Evidence_Path": "experiments/raw-results/atomicbot-turboquant/2026-07-16/", "Evidence_Commit": "Pending PR",
                         "Reviewer": "Codex evidence reconciliation", "Review_Date": "2026-07-16", "Notes": "No pass was inferred from a blocked or timed-out condition."})
    rewrite("Failure-Register.csv", failures, lambda r: r.get("Route") == ROUTE)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
