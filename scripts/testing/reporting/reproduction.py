"""Contributor-facing, read-only release verification guidance."""

from __future__ import annotations


_SUPPORTED_ROUTES = frozenset(
    {
        "upstream-llama",
        "atomicbot",
        "animehacker",
        "experimental-openvino",
        "official-openvino",
        "cross-route",
    }
)


def render_release_validation_guide(route: str) -> str:
    """Render the sole supported command surface for a settled route package."""
    if route not in _SUPPORTED_ROUTES:
        raise ValueError(f"unsupported release route: {route}")
    return (
        "# Release verification command\n\n"
        "Run from the repository root. This validates the settled package through "
        "the supported contributor CLI. These checks do not rerun benchmarks or "
        "quality scoring, regenerate reports, invoke Word, or rewrite catalogs. They "
        "do not modify evidence.\n\n"
        "```powershell\n"
        "python -m scripts.testing.cli.validate_results "
        f"--route {route} --output-root docs/testing/final-results\n"
        "```\n\n"
        "Internal normalizers and finalizers are provenance implementation details, "
        "not an additional reproduction command surface.\n\n"
    )


def render_release_validation_dependencies() -> str:
    """Describe validation dependencies without advertising mutating internals."""
    return (
        "# Validation dependencies\n\n"
        "- Python with the pinned packages in `scripts/testing/requirements.txt`.\n"
        "- The settled route package and its repository-relative evidence sources.\n"
        "- The supported command in `commands.md` (or this directory's `README.md`).\n\n"
        "Report generation, Word export, benchmark execution, and quality "
        "re-adjudication are outside this validation-only boundary.\n"
    )


def render_internal_scripts_notice() -> str:
    """Keep internal adapters discoverable only as provenance context."""
    return (
        "# Maintained implementation\n\n"
        "Internal normalization and finalization code is retained for provenance and "
        "maintenance. Contributors validate the settled package only through the "
        "supported command in `../commands.md`.\n"
    )
