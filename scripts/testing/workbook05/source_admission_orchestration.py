"""Run one controlled Workbook 05 source-admission step at a time.

The PowerShell orchestrator owns ordering and exit semantics. This module is a
narrow adapter around the already-tested Python components so PowerShell never
needs to evaluate generated command strings or duplicate evidence logic.
"""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
import shutil
import subprocess
from pathlib import Path, PureWindowsPath
from typing import Any, Iterable, Mapping, Sequence

from scripts.testing.workbook05.capture_documented_commands import capture_documents
from scripts.testing.workbook05.checkpoint import load_checkpoint, record_step
from scripts.testing.workbook05.cmake_test_discovery import audit_target_per_test
from scripts.testing.workbook05.configure_probe import (
    build_route_a_configure_command,
    ensure_fresh_build_directory,
    evaluate_route_a_configure_probe,
    write_configure_probe_report,
)
from scripts.testing.workbook05.hash_manifest import write_hash_manifest
from scripts.testing.workbook05.measurement_controls import (
    capture_measurement_controls,
    validate_measurement_controls,
)
from scripts.testing.workbook05.schema_validation import validate_json_file
from scripts.testing.workbook05.source_admission_phase import assemble_phase_outputs
from scripts.testing.workbook05.source_capability import write_capability_report
from scripts.testing.workbook05.source_verification import verify_source_tree


CAMPAIGN_ID = "GTQ-WB05-MF-v1"
PHASE_ID = "phase-1-source-admission"
ROUTE_A_ID = "route-a-merged-openvino"
ROUTE_B_ID = "route-b-experimental-qjl-polar"

SUPPORTED_STEPS = {
    "measurement-control-capture",
    "route-a-runtime-verification",
    "route-a-genai-verification",
    "route-b-verification",
    "document-capture",
    "capability-inspection",
    "route-b-cmake-audit",
    "route-a-configure-probe",
    "route-decisions",
    "hashes",
}


class StepIntegrityError(RuntimeError):
    """Raised when orchestration inputs or produced evidence are untrustworthy."""


def _load_json(path: Path) -> dict[str, Any]:
    """Read one BOM-tolerant JSON object."""

    value = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(value, dict):
        raise StepIntegrityError(f"Expected a JSON object: {path}")
    return value


def _write_json(path: Path, value: Mapping[str, Any]) -> None:
    """Write deterministic reviewer-readable UTF-8 JSON."""

    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(dict(value), indent=2) + "\n",
        encoding="utf-8",
        newline="\n",
    )


def _sha256(path: Path) -> str:
    """Hash one complete file without loading it all into memory."""

    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _schema_path(repository_root: Path, name: str) -> Path:
    """Return one version-controlled Workbook 05 schema path."""

    return (
        repository_root
        / "experiments/granite_turboquant_intel/schemas/workbook05"
        / name
    )


def _assert_schema(
    repository_root: Path,
    instance_path: Path,
    schema_name: str,
) -> None:
    """Turn schema issues into one integrity failure before admission."""

    issues = validate_json_file(instance_path, _schema_path(repository_root, schema_name))
    if issues:
        details = "; ".join(
            f"{issue.json_path}: {issue.message}" for issue in issues
        )
        raise StepIntegrityError(
            f"Schema validation failed for {instance_path}: {details}"
        )


def _result(
    step_id: str,
    kind: str,
    status: str,
    reason: str,
    **extra: Any,
) -> dict[str, Any]:
    """Create one standard step result consumed by PowerShell."""

    result: dict[str, Any] = {
        "StepId": step_id,
        "Kind": kind,
        "Status": status,
        "Reason": reason,
    }
    result.update(extra)
    return result


def _write_step_result(output_root: Path, result: Mapping[str, Any]) -> Path:
    """Persist one step result before PowerShell decides whether to continue."""

    destination = output_root / "steps" / f"{result['StepId']}.json"
    _write_json(destination, result)
    return destination


def _settings(arguments: argparse.Namespace) -> dict[str, Any]:
    """Load the frozen source-admission settings supplied by PowerShell."""

    return _load_json(arguments.settings.resolve())


def _route_directory(output_root: Path, route_name: str) -> Path:
    """Return one separated route evidence directory."""

    return output_root / "routes" / route_name


def _source_specification(
    route_id: str,
    source_role: str,
    configured: Mapping[str, Any],
) -> dict[str, Any]:
    """Convert the controlled settings record into the verifier interface."""

    return {
        "route_id": route_id,
        "source_role": source_role,
        "repository_full_name": configured["repository_full_name"],
        "origin_url": configured["origin_url"],
        "commit": configured["commit"],
    }


def _run_measurement_capture(arguments: argparse.Namespace) -> dict[str, Any]:
    """Freeze and validate every later performance and quality control."""

    destination = arguments.output_directory / "measurement/measurement-controls.json"
    configuration = (
        arguments.repository_root
        / "experiments/granite_turboquant_intel/configurations/workbook05/measurement-controls.json"
    )
    report = capture_measurement_controls(
        arguments.repository_root,
        configuration,
        destination,
    )
    issues = validate_measurement_controls(
        report,
        repository_root=arguments.repository_root,
        configuration_path=configuration,
    )
    if issues:
        details = "; ".join(
            f"{issue.code} at {issue.path}: {issue.message}" for issue in issues
        )
        raise StepIntegrityError(details)

    return _result(
        arguments.step,
        "Success",
        "Passed",
        "Measurement and quality controls were captured and validated.",
        EvidencePath="measurement/measurement-controls.json",
    )


def _run_source_verification(
    arguments: argparse.Namespace,
    *,
    route_id: str,
    source_role: str,
    configured: Mapping[str, Any],
    route_folder: str,
    report_name: str,
) -> dict[str, Any]:
    """Verify or clone one exact source tree without destructive cleanup."""

    route_directory = _route_directory(arguments.output_directory, route_folder)
    result = verify_source_tree(
        _source_specification(route_id, source_role, configured),
        Path(configured["source_directory"]),
        route_directory / "commands",
        int(_settings(arguments)["clone_timeout_seconds"]),
    )
    report_path = route_directory / report_name
    _write_json(report_path, result.report)
    _assert_schema(
        arguments.repository_root,
        report_path,
        "source-tree-report.schema.json",
    )

    if result.report["decision"] == "Passed":
        return _result(
            arguments.step,
            "Success",
            "Passed",
            result.report["decision_reason"],
            EvidencePath=report_path.relative_to(arguments.output_directory).as_posix(),
        )

    # A source-tree mismatch means the evidence boundary itself is untrusted.
    # It is therefore an integrity failure, not a scientific capability result.
    return _result(
        arguments.step,
        "IntegrityFailure",
        "Failed",
        result.report["decision_reason"],
        EvidencePath=report_path.relative_to(arguments.output_directory).as_posix(),
    )


def _run_document_capture(arguments: argparse.Namespace) -> dict[str, Any]:
    """Capture pinned documents and fenced commands as inert evidence only."""

    configuration = (
        arguments.repository_root
        / "experiments/granite_turboquant_intel/configurations/workbook05/pinned-document-sources.json"
    )
    created = capture_documents(configuration, arguments.output_directory)
    expected = {
        arguments.output_directory
        / "commands/route-a-merged-openvino-documented-commands.json",
        arguments.output_directory
        / "commands/route-b-experimental-qjl-polar-documented-commands.json",
    }
    if not expected.issubset(set(created)):
        raise StepIntegrityError("Document capture did not create both route manifests.")

    return _result(
        arguments.step,
        "Success",
        "Passed",
        "Pinned documents and inert command blocks were captured for both routes.",
        EvidencePath="commands/route-a-merged-openvino-documented-commands.json",
    )


def _run_capability_inspection(arguments: argparse.Namespace) -> dict[str, Any]:
    """Inspect exact source files without promoting presence to runtime support."""

    settings = _settings(arguments)
    route_a = settings["routes"][ROUTE_A_ID]
    route_b = settings["routes"][ROUTE_B_ID]

    route_a_directory = _route_directory(arguments.output_directory, "route-a")
    route_b_directory = _route_directory(arguments.output_directory, "route-b")
    report_a = write_capability_report(
        Path(route_a["runtime"]["source_directory"]),
        route_a["source_requirements"],
        ROUTE_A_ID,
        route_a_directory,
    )
    report_b = write_capability_report(
        Path(route_b["runtime"]["source_directory"]),
        route_b["source_requirements"],
        ROUTE_B_ID,
        route_b_directory,
    )
    _assert_schema(
        arguments.repository_root,
        route_a_directory / "source-capabilities.json",
        "source-capability-report.schema.json",
    )
    _assert_schema(
        arguments.repository_root,
        route_b_directory / "source-capabilities.json",
        "source-capability-report.schema.json",
    )

    blocked = report_a["decision"] != "Passed" or report_b["decision"] != "Passed"
    kind = "ScientificBlocker" if blocked else "Success"
    status = "Blocked" if blocked else "Passed"
    reason = (
        f"Route A capability result: {report_a['decision']}. "
        f"Route B capability result: {report_b['decision']}."
    )
    return _result(
        arguments.step,
        kind,
        status,
        reason,
        RouteAStatus=report_a["decision"],
        RouteBStatus=report_b["decision"],
    )


def _run_route_b_audit(arguments: argparse.Namespace) -> dict[str, Any]:
    """Audit RB-SRC-001 without configuring or modifying the experimental fork."""

    settings = _settings(arguments)
    route = settings["routes"][ROUTE_B_ID]
    source_root = Path(route["runtime"]["source_directory"])
    cmake_path = source_root / route["source_requirements"][-1]["path"]
    test_root = source_root / route["test_root"]
    generated_path = (
        arguments.output_directory
        / "routes/route-b/cmake/route-b-generated-target.txt"
    )
    generated_path.parent.mkdir(parents=True, exist_ok=True)

    # Phase 1 deliberately does not configure Route B. An empty file records
    # that generated target metadata is unavailable rather than inventing it.
    if not generated_path.exists():
        generated_path.write_text("", encoding="utf-8", newline="\n")

    cmake_text = cmake_path.read_text(encoding="utf-8", errors="replace")
    generated_text = generated_path.read_text(encoding="utf-8-sig")
    decision = audit_target_per_test(
        cmake_text,
        test_root,
        route["test_class_file_name"],
        generated_text,
    )
    repeated_arch = any(
        "LIST_OF_TEST_ARCH_INSTANCES is assigned more than once" in reason
        for reason in decision.reasons
    )
    repeated_common = any(
        "LIST_OF_TEST_COMMON_INSTANCES is assigned more than once" in reason
        for reason in decision.reasons
    )
    status = (
        "Confirmed"
        if decision.blocker_confirmed
        else "Inconclusive"
        if repeated_arch or repeated_common
        else "Disproved"
    )
    report = {
        "schema_version": "1.0",
        "campaign_id": CAMPAIGN_ID,
        "phase_id": PHASE_ID,
        "route_id": ROUTE_B_ID,
        "blocker_id": route["blocker_id"],
        "source_path": route["source_requirements"][-1]["path"],
        "class_file_name": route["test_class_file_name"],
        "repeated_arch_assignment": repeated_arch,
        "repeated_common_assignment": repeated_common,
        "intended_sources": list(decision.intended_sources),
        "final_sources": list(decision.final_sources),
        "omitted_sources": list(decision.omitted_sources),
        "generated_metadata_path": "cmake/route-b-generated-target.txt",
        "status": status,
        "reasons": list(decision.reasons),
    }
    report_path = arguments.output_directory / "routes/route-b/cmake-test-discovery.json"
    _write_json(report_path, report)
    _assert_schema(
        arguments.repository_root,
        report_path,
        "cmake-test-discovery-report.schema.json",
    )

    kind = "Success" if status == "Disproved" else "ScientificBlocker"
    step_status = "Passed" if status == "Disproved" else "Blocked"
    return _result(
        arguments.step,
        kind,
        step_status,
        " ".join(decision.reasons),
        EvidencePath="routes/route-b/cmake-test-discovery.json",
        BlockerStatus=status,
    )


def _read_report(output_root: Path, relative_path: str) -> dict[str, Any] | None:
    """Read an optional prior report without converting absence into a crash."""

    path = output_root / relative_path
    return _load_json(path) if path.is_file() else None


def _route_a_prerequisites_pass(output_root: Path) -> tuple[bool, list[str]]:
    """Decide whether the configure-only probe is safe to attempt."""

    reasons: list[str] = []
    checks = (
        ("preflight/preflight-report.json", "overall_status", "Passed"),
        ("routes/route-a/source-tree-runtime.json", "decision", "Passed"),
        ("routes/route-a/source-tree-genai.json", "decision", "Passed"),
        ("routes/route-a/source-capabilities.json", "decision", "Passed"),
    )
    for relative_path, field, expected in checks:
        report = _read_report(output_root, relative_path)
        if report is None:
            reasons.append(f"Missing prerequisite report: {relative_path}")
        elif report.get(field) != expected:
            reasons.append(
                f"Prerequisite {relative_path} has {field}={report.get(field)!r}."
            )

    for relative_path in (
        "measurement/measurement-controls.json",
        "commands/route-a-merged-openvino-documented-commands.json",
    ):
        if not (output_root / relative_path).is_file():
            reasons.append(f"Missing prerequisite evidence: {relative_path}")
    return not reasons, reasons


def _run_route_a_configure(arguments: argparse.Namespace) -> dict[str, Any]:
    """Run only the exact CPU CMake generation command after prerequisites pass."""

    prerequisites_pass, prerequisite_reasons = _route_a_prerequisites_pass(
        arguments.output_directory
    )
    if not prerequisites_pass:
        return _result(
            arguments.step,
            "ScientificBlocker",
            "Blocked",
            " ".join(prerequisite_reasons),
        )

    settings = _settings(arguments)
    route = settings["routes"][ROUTE_A_ID]
    source_root = Path(route["runtime"]["source_directory"])
    build_root = Path(route["build_directory"])
    cmake_path = Path(settings["cmake_path"])
    ensure_fresh_build_directory(build_root)

    command = build_route_a_configure_command(
        PureWindowsPath(str(cmake_path)),
        PureWindowsPath(str(source_root)),
        PureWindowsPath(str(build_root)),
    )
    evidence_directory = arguments.output_directory / "routes/route-a/configure"
    evidence_directory.mkdir(parents=True, exist_ok=True)
    stdout_path = evidence_directory / "route-a-configure.stdout.txt"
    stderr_path = evidence_directory / "route-a-configure.stderr.txt"

    from datetime import datetime, timezone

    started = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")
    try:
        completed = subprocess.run(
            list(command),
            cwd=settings["workspace_root"],
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=int(settings["configure_timeout_seconds"]),
            shell=False,
            check=False,
        )
    except subprocess.TimeoutExpired as error:
        stdout = error.stdout if isinstance(error.stdout, str) else ""
        stderr = error.stderr if isinstance(error.stderr, str) else ""
        completed = subprocess.CompletedProcess(list(command), -1, stdout, stderr)
    ended = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")
    stdout_path.write_text(completed.stdout or "", encoding="utf-8", newline="\n")
    stderr_path.write_text(completed.stderr or "", encoding="utf-8", newline="\n")

    cache_source = build_root / "CMakeCache.txt"
    if not cache_source.is_file():
        return _result(
            arguments.step,
            "ScientificBlocker",
            "Blocked",
            f"CMake generation exited with {completed.returncode} and produced no CMakeCache.txt.",
        )

    cache_evidence = evidence_directory / "CMakeCache.txt"
    shutil.copyfile(cache_source, cache_evidence)
    cache_text = cache_source.read_text(encoding="utf-8", errors="replace")
    decision = evaluate_route_a_configure_probe(
        command,
        completed.returncode,
        cache_text,
        expected_source_root=PureWindowsPath(str(source_root)),
        expected_build_root=PureWindowsPath(str(build_root)),
        expected_cmake_path=PureWindowsPath(str(cmake_path)),
    )
    if not decision.passed:
        return _result(
            arguments.step,
            "ScientificBlocker",
            "Blocked",
            " ".join(decision.reasons),
        )

    report_path = arguments.output_directory / "routes/route-a/configure-probe.json"
    write_configure_probe_report(
        report_path,
        decision=decision,
        started_utc=started,
        ended_utc=ended,
        exit_code=completed.returncode,
        stdout_path="configure/route-a-configure.stdout.txt",
        stderr_path="configure/route-a-configure.stderr.txt",
        cmake_cache_path=cache_evidence,
    )
    _assert_schema(
        arguments.repository_root,
        report_path,
        "configure-probe-report.schema.json",
    )
    return _result(
        arguments.step,
        "Success",
        "Passed",
        "The exact Route A CPU-only CMake generation probe passed.",
        EvidencePath="routes/route-a/configure-probe.json",
    )


def _set_proof(
    record: dict[str, Any],
    proof_id: str,
    status: str,
    evidence_path: str = "",
) -> None:
    """Update exactly one controlled proof in a runtime record copy."""

    matches = [proof for proof in record["proofs"] if proof["proof_id"] == proof_id]
    if len(matches) != 1:
        raise StepIntegrityError(f"Unknown or duplicate proof: {proof_id}")
    matches[0]["status"] = status
    matches[0]["evidence_path"] = evidence_path


def _passed(report: dict[str, Any] | None, field: str, expected: Any) -> bool:
    """Return True only when one prior evidence report has the expected value."""

    return report is not None and report.get(field) == expected


def _finding_passes(report: dict[str, Any] | None, ids: Sequence[str]) -> bool:
    """Require configured source findings without treating contradictions as runtime support."""

    if report is None:
        return False
    by_id = {finding["capability_id"]: finding for finding in report["findings"]}
    return all(
        capability_id in by_id
        and by_id[capability_id]["classification"] != "Missing"
        for capability_id in ids
    )


def _runtime_record(
    repository_root: Path,
    name: str,
) -> dict[str, Any]:
    """Load one controlled route record as an output-only mutable copy."""

    path = (
        repository_root
        / "experiments/granite_turboquant_intel/manifests/campaigns"
        / CAMPAIGN_ID
        / name
    )
    return copy.deepcopy(_load_json(path))


def _run_route_decisions(arguments: argparse.Namespace) -> dict[str, Any]:
    """Populate runtime proof records and calculate truthful split-route decisions."""

    output = arguments.output_directory
    route_a = _runtime_record(
        arguments.repository_root,
        "route-a-source-admission.json",
    )
    route_b = _runtime_record(
        arguments.repository_root,
        "route-b-source-admission.json",
    )

    preflight = _read_report(output, "preflight/preflight-report.json")
    runtime_a = _read_report(output, "routes/route-a/source-tree-runtime.json")
    genai_a = _read_report(output, "routes/route-a/source-tree-genai.json")
    runtime_b = _read_report(output, "routes/route-b/source-tree-runtime.json")
    capability_a = _read_report(output, "routes/route-a/source-capabilities.json")
    capability_b = _read_report(output, "routes/route-b/source-capabilities.json")
    audit_b = _read_report(output, "routes/route-b/cmake-test-discovery.json")
    configure_a = _read_report(output, "routes/route-a/configure-probe.json")

    document_a = "commands/route-a-merged-openvino-documented-commands.json"
    document_b = "commands/route-b-experimental-qjl-polar-documented-commands.json"
    _set_proof(route_a, "RA-P01-document-capture", "Passed" if (output / document_a).is_file() else "Blocked", document_a if (output / document_a).is_file() else "")
    _set_proof(route_a, "RA-P02-runtime-source-tree", "Passed" if _passed(runtime_a, "decision", "Passed") else "Blocked", "routes/route-a/source-tree-runtime.json")
    _set_proof(route_a, "RA-P03-genai-source-tree", "Passed" if _passed(genai_a, "decision", "Passed") else "Blocked", "routes/route-a/source-tree-genai.json")
    _set_proof(route_a, "RA-P04-runtime-submodules", "Passed" if _passed(runtime_a, "submodules_complete", True) else "Blocked", "routes/route-a/source-tree-runtime.json")
    _set_proof(route_a, "RA-P05-genai-submodules", "Passed" if _passed(genai_a, "submodules_complete", True) else "Blocked", "routes/route-a/source-tree-genai.json")
    _set_proof(route_a, "RA-P06-source-capabilities", "Passed" if _passed(capability_a, "decision", "Passed") else "Blocked", "routes/route-a/source-capabilities.json")
    _set_proof(route_a, "RA-P07-toolchain", "Passed" if _passed(preflight, "overall_status", "Passed") else "Blocked", "preflight/preflight-report.json")
    _set_proof(route_a, "RA-P08-configure-probe", "Passed" if _passed(configure_a, "status", "Passed") else "Blocked", "routes/route-a/configure-probe.json" if configure_a else "")
    _set_proof(route_a, "RA-P09-generated-metadata", "Passed" if _passed(configure_a, "status", "Passed") else "Blocked", "routes/route-a/configure/CMakeCache.txt" if configure_a else "")

    _set_proof(route_b, "RB-P01-document-capture", "Passed" if (output / document_b).is_file() else "Blocked", document_b if (output / document_b).is_file() else "")
    _set_proof(route_b, "RB-P02-runtime-source-tree", "Passed" if _passed(runtime_b, "decision", "Passed") else "Blocked", "routes/route-b/source-tree-runtime.json")
    _set_proof(route_b, "RB-P03-runtime-submodules", "Passed" if _passed(runtime_b, "submodules_complete", True) else "Blocked", "routes/route-b/source-tree-runtime.json")
    _set_proof(route_b, "RB-P04-qjl-source-paths", "Passed" if _finding_passes(capability_b, ("RB-CAP-001-codec-properties", "RB-CAP-002-qjl-write-read-paths")) else "Blocked", "routes/route-b/source-capabilities.json")
    _set_proof(route_b, "RB-P05-polar-source-paths", "Passed" if _finding_passes(capability_b, ("RB-CAP-003-polar-api", "RB-CAP-004-polar-decode-codecs")) else "Blocked", "routes/route-b/source-capabilities.json")
    _set_proof(route_b, "RB-P06-encode-decode-source-paths", "Passed" if _finding_passes(capability_b, ("RB-CAP-002-qjl-write-read-paths", "RB-CAP-003-polar-api", "RB-CAP-004-polar-decode-codecs")) else "Blocked", "routes/route-b/source-capabilities.json")
    _set_proof(route_b, "RB-P07-independent-kv-source-paths", "Passed" if _finding_passes(capability_b, ("RB-CAP-001-codec-properties",)) else "Blocked", "routes/route-b/source-capabilities.json")
    _set_proof(route_b, "RB-P08-test-discovery-audit", "Passed" if audit_b else "Blocked", "routes/route-b/cmake-test-discovery.json" if audit_b else "")
    generated_metadata = output / "routes/route-b/cmake/route-b-generated-target.txt"
    _set_proof(route_b, "RB-P09-generated-test-metadata", "Passed" if generated_metadata.is_file() and generated_metadata.stat().st_size > 0 else "Blocked", "routes/route-b/cmake-test-discovery.json" if audit_b else "")

    route_a["admission_status"] = (
        "Admitted"
        if all(proof["status"] == "Passed" for proof in route_a["proofs"] if proof["required"])
        else "Blocked"
    )
    route_b["admission_status"] = "Blocked"
    route_a["decision_reason"] = (
        "Every approved Phase 1 source, toolchain, document, capability, and configure-generation proof passed."
        if route_a["admission_status"] == "Admitted"
        else "One or more approved Phase 1 Route A proofs are blocked; no build or runtime support is claimed."
    )
    route_b["decision_reason"] = (
        "Route B remains experimental and blocked because RB-SRC-001 is open; source presence is not execution support."
    )

    controls_directory = output / "controls"
    _write_json(controls_directory / "route-a-source-admission.json", route_a)
    _write_json(controls_directory / "route-b-source-admission.json", route_b)

    measurement_path = output / "measurement/measurement-controls.json"
    measurement = _load_json(measurement_path)
    checkpoint_path = output / "checkpoint/checkpoint.json"
    checkpoint = load_checkpoint(checkpoint_path)
    outputs = assemble_phase_outputs(
        route_a,
        route_b,
        measurement,
        checkpoint_template=checkpoint,
        measurement_controls_path="measurement/measurement-controls.json",
        measurement_controls_sha256=_sha256(measurement_path),
        summary_json_path="summary/source-admission-summary.json",
        summary_markdown_path="summary/source-admission-summary.md",
        checkpoint_path="checkpoint/checkpoint.json",
        hash_manifest_path="hash-manifest.sha256",
    )
    summary_path = output / "summary/source-admission-summary.json"
    markdown_path = output / "summary/source-admission-summary.md"
    _write_json(summary_path, outputs.summary)
    markdown_path.parent.mkdir(parents=True, exist_ok=True)
    markdown_path.write_text(outputs.summary_markdown, encoding="utf-8", newline="\n")
    _write_json(checkpoint_path, outputs.checkpoint_candidate)
    _assert_schema(
        arguments.repository_root,
        summary_path,
        "source-admission-summary.schema.json",
    )

    kind = "Success" if outputs.decision.checkpoint_status == "Passed" else "ScientificBlocker"
    status = "Passed" if outputs.decision.checkpoint_status == "Passed" else "Blocked"
    return _result(
        arguments.step,
        kind,
        status,
        " ".join(outputs.decision.reasons),
        RouteAStatus=outputs.decision.route_a_status,
        RouteBStatus=outputs.decision.route_b_status,
        CheckpointStatus=outputs.decision.checkpoint_status,
    )


def _run_hashes(arguments: argparse.Namespace) -> dict[str, Any]:
    """Finalize the runtime checkpoint and then hash the complete bundle."""

    summary_path = arguments.output_directory / "summary/source-admission-summary.json"
    summary = _load_json(summary_path)
    checkpoint_path = arguments.output_directory / "checkpoint/checkpoint.json"
    checkpoint = load_checkpoint(checkpoint_path)
    checkpoint_status = summary["checkpoint_status"]
    persisted_status = (
        "Passed"
        if checkpoint_status == "Passed"
        else "Blocked"
        if checkpoint_status == "Blocked"
        else "In progress"
    )
    record_step(
        checkpoint_path,
        expected_generation=int(checkpoint["generation"]),
        step_id=PHASE_ID,
        status=persisted_status,
        evidence_sha256=_sha256(summary_path) if persisted_status == "Passed" else "",
    )

    result = _result(
        arguments.step,
        "Success",
        "Passed",
        "The final checkpoint and complete evidence bundle were hashed.",
        EvidencePath="hash-manifest.sha256",
    )
    _write_step_result(arguments.output_directory, result)
    write_hash_manifest(
        arguments.output_directory,
        arguments.output_directory / "hash-manifest.sha256",
    )
    result["AlreadyWritten"] = True
    return result


def _run_step(arguments: argparse.Namespace) -> dict[str, Any]:
    """Dispatch one reviewed step without permitting arbitrary action names."""

    settings = _settings(arguments)
    if arguments.step == "measurement-control-capture":
        return _run_measurement_capture(arguments)
    if arguments.step == "route-a-runtime-verification":
        route = settings["routes"][ROUTE_A_ID]
        return _run_source_verification(
            arguments,
            route_id=ROUTE_A_ID,
            source_role="runtime",
            configured=route["runtime"],
            route_folder="route-a",
            report_name="source-tree-runtime.json",
        )
    if arguments.step == "route-a-genai-verification":
        route = settings["routes"][ROUTE_A_ID]
        return _run_source_verification(
            arguments,
            route_id=ROUTE_A_ID,
            source_role="genai-compatibility-candidate",
            configured=route["genai"],
            route_folder="route-a",
            report_name="source-tree-genai.json",
        )
    if arguments.step == "route-b-verification":
        route = settings["routes"][ROUTE_B_ID]
        return _run_source_verification(
            arguments,
            route_id=ROUTE_B_ID,
            source_role="experimental-runtime",
            configured=route["runtime"],
            route_folder="route-b",
            report_name="source-tree-runtime.json",
        )
    if arguments.step == "document-capture":
        return _run_document_capture(arguments)
    if arguments.step == "capability-inspection":
        return _run_capability_inspection(arguments)
    if arguments.step == "route-b-cmake-audit":
        return _run_route_b_audit(arguments)
    if arguments.step == "route-a-configure-probe":
        return _run_route_a_configure(arguments)
    if arguments.step == "route-decisions":
        return _run_route_decisions(arguments)
    if arguments.step == "hashes":
        return _run_hashes(arguments)
    raise StepIntegrityError(f"Unsupported source-admission step: {arguments.step}")


def main(argv: Iterable[str] | None = None) -> int:
    """CLI called by the read-only Windows orchestration script."""

    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--step", choices=sorted(SUPPORTED_STEPS), required=True)
    parser.add_argument("--repository-root", type=Path, required=True)
    parser.add_argument("--output-directory", type=Path, required=True)
    parser.add_argument("--settings", type=Path, required=True)
    arguments = parser.parse_args(list(argv) if argv is not None else None)
    arguments.repository_root = arguments.repository_root.resolve()
    arguments.output_directory = arguments.output_directory.resolve()
    arguments.settings = arguments.settings.resolve()

    try:
        result = _run_step(arguments)
        if not result.get("AlreadyWritten"):
            _write_step_result(arguments.output_directory, result)
        print(json.dumps(result, indent=2))
        return 0
    except Exception as error:  # noqa: BLE001 - boundary converts every failure to evidence.
        result = _result(
            arguments.step,
            "IntegrityFailure",
            "Failed",
            str(error),
        )
        _write_step_result(arguments.output_directory, result)
        print(json.dumps(result, indent=2))
        return 1


if __name__ == "__main__":
    raise SystemExit(main())