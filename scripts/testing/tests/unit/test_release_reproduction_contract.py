from __future__ import annotations

import importlib
import importlib.util
from pathlib import Path

import pytest


ROUTES = (
    "upstream-llama",
    "atomicbot",
    "animehacker",
    "experimental-openvino",
    "official-openvino",
    "cross-route",
)

FORBIDDEN_GENERATION_SURFACES = (
    "scripts.testing.reporting",
    "scripts/testing/cli/export_report.ps1",
    "powershell.exe",
    "write_upstream_llama_route",
    "write_atomicbot_route",
    "write_animehacker_route",
    "write_experimental_route",
    "write_official_route",
    "write_cross_route_package",
    "finalize_",
)

PUBLICATION_ROOT = Path(__file__).resolve().parents[4] / "docs/testing/final-results"
PUBLISHED_GUIDES = (
    ("upstream-llama", "01-upstream-llama-cpp/reproduction/commands.md", True),
    ("upstream-llama", "01-upstream-llama-cpp/reproduction/README.md", True),
    ("atomicbot", "02-atomicbot-turboquant/reproduction/commands.md", True),
    ("atomicbot", "02-atomicbot-turboquant/reproduction/README.md", True),
    ("animehacker", "03-animehacker-tq3-0/reproduction/commands.md", True),
    ("animehacker", "03-animehacker-tq3-0/reproduction/README.md", True),
    ("experimental-openvino", "04-openvino-experimental-fork/reproduction/README.md", False),
    ("official-openvino", "05-openvino-official-upstream/reproduction/README.md", False),
    ("cross-route", "06-cross-route-comparison/reproduction/commands.md", True),
    ("cross-route", "06-cross-route-comparison/reproduction/README.md", True),
)


@pytest.mark.parametrize("route", ROUTES)
def test_release_reproduction_guide_is_cli_only_read_only_and_no_rerun(route: str):
    assert importlib.util.find_spec("scripts.testing.reporting.reproduction") is not None, (
        "maintained generators need one release-guide source of truth"
    )
    module = importlib.import_module("scripts.testing.reporting.reproduction")
    render = getattr(module, "render_release_validation_guide", None)

    assert render is not None, "maintained generators need one release-guide source of truth"
    guide = render(route)
    command_lines = [
        line.strip()
        for line in guide.splitlines()
        if line.strip().startswith(("python ", "& ", "powershell"))
    ]

    assert command_lines == [
        "python -m scripts.testing.cli.validate_results "
        f"--route {route} --output-root docs/testing/final-results"
    ]
    assert "do not rerun" in guide.casefold()
    assert "do not modify evidence" in guide.casefold()
    assert not any(forbidden in guide for forbidden in FORBIDDEN_GENERATION_SURFACES)


@pytest.mark.parametrize("route, relative_path, exact", PUBLISHED_GUIDES)
def test_published_reproduction_guide_matches_maintained_renderer(
    route: str, relative_path: str, exact: bool
):
    module = importlib.import_module("scripts.testing.reporting.reproduction")
    expected = module.render_release_validation_guide(route)
    actual = (PUBLICATION_ROOT / relative_path).read_text(encoding="utf-8")

    if exact:
        assert actual == expected.rstrip() + "\n"
    else:
        assert actual.startswith(expected)
