"""Apply and audit the bounded Workbook 05 Route B CMake repair.

The module changes only the functional-test source-list accumulation pattern
covered by RB-SRC-001. It deliberately does not modify TurboQuant, QJL or
PolarQuant algorithm source.
"""

from __future__ import annotations

import argparse
import difflib
import hashlib
import json
import re
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Final


# These are the only source-list variables and directory expressions that the
# approved repair is allowed to touch.
_ARCH_VARIABLE: Final = "LIST_OF_TEST_ARCH_INSTANCES"
_COMMON_VARIABLE: Final = "LIST_OF_TEST_COMMON_INSTANCES"
_ALLOWED_ARCH_DIRECTORIES: Final = {
    "instances/x64",
    "x64",
    "instances/arm",
    "instances/riscv64",
}
_ALLOWED_COMMON_DIRECTORIES: Final = {
    "instances/common",
    "common",
}

# Match one narrow file(GLOB_RECURSE ...) line. The implementation intentionally
# avoids becoming a general CMake rewriter because a broad rewriter would make
# the repair boundary much harder to review and trust.
_GLOB_LINE = re.compile(
    r"^(?P<indent>\s*)file\(GLOB_RECURSE\s+"
    r"(?P<variable>LIST_OF_TEST_(?:ARCH|COMMON)_INSTANCES)\s+"
    r"\$\{TEST_DIR\}/(?P<directory>[^/\s\)]+(?:/[^/\s\)]+)*)/"
    r"\$\{TEST_CLASS_FILE_NAME\}\)\s*$"
)

# The repaired form uses a unique temporary variable for each directory and an
# explicit append operation immediately afterwards.
_REPAIRED_MARKERS: Final = (
    "list(APPEND LIST_OF_TEST_ARCH_INSTANCES",
    "list(APPEND LIST_OF_TEST_COMMON_INSTANCES",
)


class RouteBContractError(ValueError):
    """Raised when external source does not match the reviewed repair contract."""


@dataclass(frozen=True)
class BenchmarkContractDecision:
    """Read-only checks for current benchmark safeguards."""

    has_unconditional_f32: bool
    has_k_f32_v_tbq: bool
    reasons: tuple[str, ...]


@dataclass(frozen=True)
class RouteBRepairReport:
    """Serializable evidence produced by one controlled source repair."""

    schema_version: str
    source_commit: str
    target_path: str
    benchmark_path: str
    before_sha256: str
    after_sha256: str
    changed: bool
    benchmark_contract: BenchmarkContractDecision
    patch_path: str


def _temporary_variable(variable: str, directory: str) -> str:
    """Create a deterministic CMake variable name from an allowed directory."""

    suffix = re.sub(r"[^A-Za-z0-9]+", "_", directory).strip("_").upper()
    if not suffix:
        raise RouteBContractError("CMake source directory produced an empty suffix.")
    return f"{variable}_{suffix}"


def repair_target_per_test_text(cmake_text: str) -> str:
    """Replace repeated assignments with explicit, branch-local accumulation.

    The function is fail-closed: it repairs only the exact reviewed directory
    set. Already repaired text is returned unchanged, making the operation safe
    to rerun during validation.
    """

    # Preserve the source file's existing newline convention while generating a
    # final newline deterministically.
    newline = "\r\n" if "\r\n" in cmake_text else "\n"
    lines = cmake_text.splitlines()

    replacements: list[str] = []
    seen_arch: set[str] = set()
    seen_common: set[str] = set()
    matched_original = 0

    for line in lines:
        match = _GLOB_LINE.match(line)
        if match is None:
            replacements.append(line)
            continue

        variable = match.group("variable")
        directory = match.group("directory")
        indent = match.group("indent")
        allowed = (
            _ALLOWED_ARCH_DIRECTORIES
            if variable == _ARCH_VARIABLE
            else _ALLOWED_COMMON_DIRECTORIES
        )
        if directory not in allowed:
            raise RouteBContractError(
                f"Unexpected {variable} directory in pinned source: {directory}"
            )

        target_set = seen_arch if variable == _ARCH_VARIABLE else seen_common
        if directory in target_set:
            raise RouteBContractError(
                f"Duplicate reviewed CMake directory assignment: {directory}"
            )
        target_set.add(directory)
        matched_original += 1

        temporary = _temporary_variable(variable, directory)
        replacements.append(
            f"{indent}file(GLOB_RECURSE {temporary} "
            f"${{TEST_DIR}}/{directory}/${{TEST_CLASS_FILE_NAME}})"
        )
        replacements.append(
            f"{indent}list(APPEND {variable} ${{{temporary}}})"
        )

    if matched_original == 0:
        # An already repaired file contains both append markers and no unsafe
        # base-variable GLOB assignment. Return it byte-for-byte except for the
        # deterministic final newline.
        if all(marker in cmake_text for marker in _REPAIRED_MARKERS):
            return newline.join(lines) + newline
        raise RouteBContractError(
            "The pinned target_per_test.cmake shape was not recognised; refusing to edit it."
        )

    # The exact head contains all four architecture directories and both common
    # directories. A smaller or larger set means the pinned source moved.
    if seen_arch != _ALLOWED_ARCH_DIRECTORIES:
        raise RouteBContractError(
            "Architecture source directories did not match the reviewed set: "
            f"{sorted(seen_arch)}"
        )
    if seen_common != _ALLOWED_COMMON_DIRECTORIES:
        raise RouteBContractError(
            "Common source directories did not match the reviewed set: "
            f"{sorted(seen_common)}"
        )

    repaired = newline.join(replacements) + newline
    if repaired == cmake_text:
        raise RouteBContractError("The reviewed source was detected but no repair was produced.")
    return repaired


def verify_benchmark_contract(benchmark_text: str) -> BenchmarkContractDecision:
    """Verify safeguards already present at the exact experimental head.

    This function is read-only. It intentionally does not rewrite benchmark
    source because the current pinned revision already contains both safeguards.
    """

    has_unconditional_f32 = bool(
        re.search(
            r"std::vector<ov::AnyMap>\s+config\s*\{\s*\{\s*\{"
            r"ov::hint::inference_precision\.name\(\)\s*,\s*"
            r"std::string\(\"f32\"\)",
            benchmark_text,
            flags=re.DOTALL,
        )
    )

    asymmetric = re.search(
        r"INSTANTIATE_TEST_SUITE_P\(benchmark_KVCacheBench_llama8b_Kf32_Vtbq,"
        r"(?P<body>.*?)ConcatSDPKVBenchBase::getTestCaseName\);",
        benchmark_text,
        flags=re.DOTALL,
    )
    has_k_f32_v_tbq = False
    if asymmetric is not None:
        body = asymmetric.group("body")
        none_index = body.find("ValuesIn(mode_none)")
        tbq_index = body.find("ValuesIn(mode_tbq)")
        has_k_f32_v_tbq = 0 <= none_index < tbq_index

    reasons: list[str] = []
    if not has_unconditional_f32:
        reasons.append("The benchmark precision list has no unconditional F32 entry.")
    if not has_k_f32_v_tbq:
        reasons.append("The K=F32/V=TurboQuant benchmark parameters are not present in order.")

    return BenchmarkContractDecision(
        has_unconditional_f32=has_unconditional_f32,
        has_k_f32_v_tbq=has_k_f32_v_tbq,
        reasons=tuple(reasons),
    )


def _sha256_text(text: str) -> str:
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def apply_repair(source_root: Path, evidence_root: Path, source_commit: str) -> RouteBRepairReport:
    """Apply the one-file repair and write text/JSON evidence outside source."""

    target_relative = Path(
        "src/plugins/intel_cpu/tests/functional/cmake/target_per_test.cmake"
    )
    benchmark_relative = Path(
        "src/plugins/intel_cpu/tests/functional/custom/subgraph_tests/benchmark/x64/"
        "concat_sdp_kv_bench.cpp"
    )
    target_path = source_root / target_relative
    benchmark_path = source_root / benchmark_relative

    if not target_path.is_file() or not benchmark_path.is_file():
        raise RouteBContractError("Required Route B source file is missing.")
    if target_path.is_symlink() or benchmark_path.is_symlink():
        raise RouteBContractError("Required Route B source files must not be symbolic links.")

    before = target_path.read_text(encoding="utf-8")
    benchmark_text = benchmark_path.read_text(encoding="utf-8")
    benchmark_decision = verify_benchmark_contract(benchmark_text)
    if benchmark_decision.reasons:
        raise RouteBContractError("; ".join(benchmark_decision.reasons))

    after = repair_target_per_test_text(before)
    target_path.write_text(after, encoding="utf-8", newline="\n")

    evidence_root.mkdir(parents=True, exist_ok=False)
    patch_name = "route-b-target-per-test.patch"
    patch_text = "".join(
        difflib.unified_diff(
            before.splitlines(keepends=True),
            after.splitlines(keepends=True),
            fromfile=f"a/{target_relative.as_posix()}",
            tofile=f"b/{target_relative.as_posix()}",
        )
    )
    (evidence_root / patch_name).write_text(
        patch_text,
        encoding="utf-8",
        newline="\n",
    )

    report = RouteBRepairReport(
        schema_version="1.0",
        source_commit=source_commit,
        target_path=target_relative.as_posix(),
        benchmark_path=benchmark_relative.as_posix(),
        before_sha256=_sha256_text(before),
        after_sha256=_sha256_text(after),
        changed=before != after,
        benchmark_contract=benchmark_decision,
        patch_path=patch_name,
    )
    report_data = asdict(report)
    (evidence_root / "route-b-repair-report.json").write_text(
        json.dumps(report_data, indent=2) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    return report


def _parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-root", type=Path, required=True)
    parser.add_argument("--evidence-root", type=Path, required=True)
    parser.add_argument("--source-commit", required=True)
    return parser.parse_args()


def main() -> int:
    args = _parse_args()
    try:
        report = apply_repair(
            args.source_root.resolve(),
            args.evidence_root.resolve(),
            args.source_commit,
        )
    except (OSError, RouteBContractError) as error:
        print(f"ROUTE_B_REPAIR_FAILED: {error}")
        return 1

    print(
        "ROUTE_B_REPAIR_APPLIED: "
        f"{report.target_path} {report.before_sha256} -> {report.after_sha256}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
