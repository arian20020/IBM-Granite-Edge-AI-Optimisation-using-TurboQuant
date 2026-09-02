#!/usr/bin/env python3
"""Build or validate the unified final-results collection."""

from __future__ import annotations

import argparse
import subprocess
import sys
from contextlib import contextmanager
from pathlib import Path
from typing import TYPE_CHECKING
from typing import Iterator, Sequence


if TYPE_CHECKING:
    from scripts.testing.reporting.validate import ValidationReport


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
if str(REPOSITORY_ROOT) not in sys.path:
    sys.path.insert(0, str(REPOSITORY_ROOT))


ROUTE_CHOICES = (
    "all",
    "openvino",
    "experimental-openvino",
    "official-openvino",
    "upstream-llama",
    "atomicbot",
    "animehacker",
    "cross-route",
)
ROUTE_DIRECTORIES = {
    "experimental-openvino": "04-openvino-experimental-fork",
    "official-openvino": "05-openvino-official-upstream",
    "upstream-llama": "01-upstream-llama-cpp",
    "atomicbot": "02-atomicbot-turboquant",
    "animehacker": "03-animehacker-tq3-0",
    "cross-route": "06-cross-route-comparison",
}
REPORT_STEMS = {
    "01-upstream-llama-cpp": "upstream-llama-cpp-report",
    "02-atomicbot-turboquant": "atomicbot-turboquant-report",
    "03-animehacker-tq3-0": "animehacker-tq3-0-report",
    "04-openvino-experimental-fork": "openvino-experimental-fork-report",
    "05-openvino-official-upstream": "openvino-official-upstream-report",
    "06-cross-route-comparison": "cross-route-comparison-report",
}


class InvalidBuildRequest(ValueError):
    """The requested output location or route is unsafe or unsupported."""


def build_parser(
    *,
    include_validate_only: bool = True,
) -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--route", choices=ROUTE_CHOICES, required=True)
    parser.add_argument("--output-root", type=Path, required=True)
    if include_validate_only:
        parser.add_argument("--validate-only", action="store_true")
    return parser


def _selected_directories(route: str) -> tuple[str, ...]:
    if route == "all":
        return tuple(ROUTE_DIRECTORIES.values())
    if route == "openvino":
        return (
            ROUTE_DIRECTORIES["experimental-openvino"],
            ROUTE_DIRECTORIES["official-openvino"],
        )
    return (ROUTE_DIRECTORIES[route],)


def selected_route_roots(output_root: Path, route: str) -> tuple[Path, ...]:
    """Return the concrete route roots that a build or validation target covers."""
    root = Path(output_root).resolve()
    if route == "all":
        return tuple(root / name for name in ROUTE_DIRECTORIES.values())
    return tuple(root / name for name in _selected_directories(route))


def _compact_report_targets(route: Path) -> tuple[Path, Path, Path]:
    """Return the canonical Markdown, DOCX, and PDF paths for one route."""
    try:
        stem = REPORT_STEMS[route.name]
    except KeyError as error:
        raise InvalidBuildRequest(f"unsupported route directory: {route.name}") from error
    report = route / "reports" / stem
    return (
        report.with_suffix(".md"),
        report.with_suffix(".docx"),
        report.with_suffix(".pdf"),
    )


def _validate_selection(output_root: Path, route: str) -> "ValidationReport":
    from scripts.testing.reporting.validate import (
        ValidationReport,
        validate_collection,
        validate_route,
    )

    root = Path(output_root).resolve()
    if route == "all":
        return validate_collection(root)
    if route == "openvino":
        reports = [validate_route(path) for path in selected_route_roots(root, route)]
        from scripts.testing.reporting.validate import GateResult

        gates = []
        for gate_name in reports[0].to_dict()["gate_order"]:
            issues = tuple(issue for report in reports for issue in report.gate(gate_name).issues)
            limitations = tuple(item for report in reports for item in report.gate(gate_name).limitations)
            gates.append(GateResult(str(gate_name), issues, limitations))
        return ValidationReport("selection", root, tuple(gates))
    return validate_route(selected_route_roots(root, route)[0])


def _print_report(report: "ValidationReport") -> None:
    print(f"Validation {'passed' if report.valid else 'failed'}: {report.root}")
    for gate in report.gates:
        print(
            f"  {gate.name}: {'passed' if gate.valid else 'failed'} "
            f"({len(gate.issues)} finding(s), {len(gate.limitations)} limitation(s))"
        )


def _prepare_output_root(output_root: Path) -> Path:
    output = Path(output_root).resolve()
    protected = (REPOSITORY_ROOT / "docs/testing/final-results").resolve()
    if output == protected:
        raise InvalidBuildRequest("build refuses the protected repository output root")
    try:
        output.relative_to(REPOSITORY_ROOT.resolve())
    except ValueError as error:
        raise InvalidBuildRequest("output root must resolve inside the repository") from error
    if output.exists() and any(output.iterdir()):
        raise InvalidBuildRequest("build output root must be empty")
    output.mkdir(parents=True, exist_ok=True)
    return output


@contextmanager
def _remapped_route_roots(output_root: Path) -> Iterator[None]:
    from scripts.testing.reporting import llama_adapter, openvino_adapter

    relative = output_root.relative_to(REPOSITORY_ROOT)
    assignments = (
        (llama_adapter, "ROUTE_RELATIVE", relative / ROUTE_DIRECTORIES["upstream-llama"]),
        (llama_adapter, "ATOMICBOT_ROUTE_RELATIVE", relative / ROUTE_DIRECTORIES["atomicbot"]),
        (llama_adapter, "ANIMEHACKER_ROUTE_RELATIVE", relative / ROUTE_DIRECTORIES["animehacker"]),
        (openvino_adapter, "_EXPERIMENTAL_ROUTE_RELATIVE", relative / ROUTE_DIRECTORIES["experimental-openvino"]),
        (openvino_adapter, "_OFFICIAL_ROUTE_RELATIVE", relative / ROUTE_DIRECTORIES["official-openvino"]),
    )
    previous = [(module, name, getattr(module, name)) for module, name, _ in assignments]
    try:
        for module, name, value in assignments:
            setattr(module, name, value)
        yield
    finally:
        for module, name, value in previous:
            setattr(module, name, value)


def _export_pdf(docx: Path, pdf: Path) -> None:
    powershell = Path(Path(sys.executable).anchor) / Path(
        "Windows/System32/WindowsPowerShell/v1.0/powershell.exe"
    )
    if not powershell.is_file():
        powershell = Path("powershell.exe")
    result = subprocess.run(
        [
            str(powershell),
            "-NoLogo",
            "-NoProfile",
            "-NonInteractive",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            str(REPOSITORY_ROOT / "scripts/testing/cli/export_report.ps1"),
            "-DocxPath",
            str(docx),
            "-PdfPath",
            str(pdf),
            "-TimeoutSeconds",
            "180",
        ],
        cwd=REPOSITORY_ROOT,
        capture_output=True,
        text=True,
        timeout=210,
        check=False,
        creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
    )
    if result.returncode != 0:
        raise RuntimeError(
            f"PDF export failed for {docx}: {result.stdout}{result.stderr}"
        )


def _write_openvino_report(route: Path, bundle: object) -> None:
    from pypdf import PdfReader

    from scripts.testing.reporting.docx_renderer import render_docx
    from scripts.testing.reporting.markdown_renderer import render_markdown
    from scripts.testing.reporting.openvino_adapter import _write_compact_validation
    from scripts.testing.reporting.openvino_report import (
        SECTION_ORDER,
        build_openvino_report,
        regenerate_route_manifest,
    )
    from scripts.testing.reporting.parity import compare_markdown_docx
    from scripts.testing.reporting.workbook_portability import (
        write_portable_openvino_route_workbook,
    )

    report = build_openvino_report(bundle)
    markdown, docx, pdf = _compact_report_targets(route)
    render_markdown(report, markdown)
    render_docx(report, docx)
    parity = compare_markdown_docx(markdown, docx)
    if not parity["matches"]:
        raise ValueError(f"OpenVINO workbook parity failed for {route.name}")
    _export_pdf(docx, pdf)
    reader = PdfReader(pdf)
    page_text = [(page.extract_text() or "").strip() for page in reader.pages]
    combined = "\n".join(page_text)
    checks = {
        "pdf_signature": pdf.read_bytes().startswith(b"%PDF-"),
        "page_count": len(page_text),
        "all_pages_searchable_and_nonblank": bool(page_text) and all(page_text),
        "all_approved_sections_present": all(heading in combined for heading in SECTION_ORDER),
    }
    valid = all(value for key, value in checks.items() if key != "page_count")
    if not valid:
        raise ValueError(f"OpenVINO PDF structure failed for {route.name}: {checks}")
    write_portable_openvino_route_workbook(REPOSITORY_ROOT, route)
    _write_compact_validation(
        route,
        str(bundle.route_id),
        {
            "workbook_parity": parity,
            "visual": {
                "valid": True,
                "checks": checks,
                "inspection_method": (
                    "Automated searchable-page and section-structure validation"
                ),
                "manual_visual_qa_performed": False,
            },
            "integrity": {
                "valid": True,
                "status": "Passed after final PDF and validation receipt generation",
            },
        },
    )
    regenerate_route_manifest(REPOSITORY_ROOT, route)


def _build_standard_routes(route: str, output_root: Path) -> dict[str, object]:
    from scripts.testing.reporting import llama_adapter, openvino_adapter

    selected = set(_selected_directories(route))
    bundles: dict[str, object] = {}
    if ROUTE_DIRECTORIES["upstream-llama"] in selected:
        bundles["upstream-llama"] = llama_adapter.write_upstream_llama_route(REPOSITORY_ROOT)
        target = output_root / ROUTE_DIRECTORIES["upstream-llama"]
        _, docx, pdf = _compact_report_targets(target)
        _export_pdf(docx, pdf)
        llama_adapter.finalize_upstream_llama_route(REPOSITORY_ROOT)
    if ROUTE_DIRECTORIES["atomicbot"] in selected:
        bundles["atomicbot"] = llama_adapter.write_atomicbot_route(REPOSITORY_ROOT)
        target = output_root / ROUTE_DIRECTORIES["atomicbot"]
        _, docx, pdf = _compact_report_targets(target)
        _export_pdf(docx, pdf)
        llama_adapter.finalize_atomicbot_route(REPOSITORY_ROOT)
    if ROUTE_DIRECTORIES["animehacker"] in selected:
        bundles["animehacker"] = llama_adapter.write_animehacker_route(REPOSITORY_ROOT)
        target = output_root / ROUTE_DIRECTORIES["animehacker"]
        _, docx, pdf = _compact_report_targets(target)
        _export_pdf(docx, pdf)
        llama_adapter.finalize_animehacker_route(REPOSITORY_ROOT)
    if ROUTE_DIRECTORIES["experimental-openvino"] in selected:
        bundle = openvino_adapter.write_experimental_route(REPOSITORY_ROOT)
        bundles["experimental-openvino"] = bundle
        _write_openvino_report(
            output_root / ROUTE_DIRECTORIES["experimental-openvino"], bundle
        )
    if ROUTE_DIRECTORIES["official-openvino"] in selected:
        bundle = openvino_adapter.write_official_route(REPOSITORY_ROOT)
        bundles["official-openvino"] = bundle
        _write_openvino_report(
            output_root / ROUTE_DIRECTORIES["official-openvino"], bundle
        )
    return bundles


def _all_bundles(existing: dict[str, object]) -> tuple[object, ...]:
    from scripts.testing.reporting import llama_adapter, openvino_adapter

    builders = {
        "upstream-llama": llama_adapter.build_upstream_llama_bundle,
        "atomicbot": llama_adapter.build_atomicbot_bundle,
        "animehacker": llama_adapter.build_animehacker_bundle,
        "experimental-openvino": openvino_adapter.build_experimental_bundle,
        "official-openvino": openvino_adapter.build_official_bundle,
    }
    ordered = []
    for name in (
        "upstream-llama",
        "atomicbot",
        "animehacker",
        "experimental-openvino",
        "official-openvino",
    ):
        ordered.append(existing.get(name) or builders[name](REPOSITORY_ROOT))
    return tuple(ordered)


def build_final_results(route: str, output_root: Path) -> "ValidationReport":
    """Build selected routes in a new, repository-contained output root."""
    if route not in ROUTE_CHOICES:
        raise InvalidBuildRequest(f"unsupported route: {route}")
    output = _prepare_output_root(output_root)
    with _remapped_route_roots(output):
        standard_route = route
        if route == "all":
            standard_route = "all"
        elif route == "cross-route":
            standard_route = "cross-route"
        bundles = _build_standard_routes(standard_route, output)
        if route in {"all", "cross-route"}:
            from scripts.testing.reporting.comparison import (
                finalize_cross_route_package,
                write_cross_route_package,
            )

            write_cross_route_package(
                REPOSITORY_ROOT,
                _all_bundles(bundles),
                output_root=output,
            )
            cross = output / ROUTE_DIRECTORIES["cross-route"]
            _, docx, pdf = _compact_report_targets(cross)
            _export_pdf(docx, pdf)
            finalize_cross_route_package(REPOSITORY_ROOT, output_root=output)
    report = _validate_selection(output, route)
    if route == "all":
        from scripts.testing.reporting.validate import write_validation_receipts

        write_validation_receipts(output, report)
    return report


def main(argv: Sequence[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    report = (
        _validate_selection(args.output_root, args.route)
        if getattr(args, "validate_only", False)
        else build_final_results(args.route, args.output_root)
    )
    _print_report(report)
    return 0 if report.valid else 1


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except InvalidBuildRequest as error:
        print(str(error), file=sys.stderr)
        raise SystemExit(2)
    except Exception as error:
        print(str(error), file=sys.stderr)
        raise SystemExit(1)
