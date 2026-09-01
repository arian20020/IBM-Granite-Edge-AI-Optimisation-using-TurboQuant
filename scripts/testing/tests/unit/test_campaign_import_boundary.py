from __future__ import annotations

import importlib
import importlib.util


def test_llama_atomicbot_and_animehacker_modules_use_campaign_namespace():
    modules = (
        "scripts.testing.campaigns.llama_cpp.measure_run",
        "scripts.testing.campaigns.llama_cpp.measure_server",
        "scripts.testing.campaigns.llama_cpp.parse_measurement",
        "scripts.testing.campaigns.atomicbot.runner",
        "scripts.testing.campaigns.animehacker.runner",
    )
    for name in modules:
        assert importlib.util.find_spec(name) is not None
        importlib.import_module(name)


def test_superseded_campaign_import_paths_are_absent():
    assert importlib.util.find_spec("scripts.testing.atomicbot") is None
    assert importlib.util.find_spec("scripts.testing.animehacker") is None
    assert importlib.util.find_spec("scripts.testing.measure_llama_run") is None
    assert importlib.util.find_spec("scripts.testing.measure_llama_server") is None
    assert importlib.util.find_spec("scripts.testing.parse_llama_measurement") is None
