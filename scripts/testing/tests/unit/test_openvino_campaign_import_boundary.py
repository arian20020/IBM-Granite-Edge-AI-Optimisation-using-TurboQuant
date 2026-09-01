from __future__ import annotations

import importlib
import importlib.util


def test_openvino_modules_use_the_campaign_namespace():
    modules = (
        "scripts.testing.campaigns.openvino.conversion",
        "scripts.testing.campaigns.openvino.runner",
        "scripts.testing.campaigns.openvino.metrics",
        "scripts.testing.campaigns.openvino.quality",
        "scripts.testing.campaigns.openvino.format_boundary",
    )
    for name in modules:
        assert importlib.util.find_spec(name) is not None
        importlib.import_module(name)


def test_superseded_openvino_package_is_absent():
    assert importlib.util.find_spec("scripts.testing.official_openvino") is None
