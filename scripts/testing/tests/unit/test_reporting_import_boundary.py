from __future__ import annotations

import importlib.util


def test_reporting_is_the_only_active_results_package():
    assert importlib.util.find_spec("scripts.testing.reporting.validate") is not None
    assert importlib.util.find_spec("scripts.testing.final_results") is None


def test_reporting_public_interfaces_are_stable():
    from scripts.testing.reporting.models import RouteBundle
    from scripts.testing.reporting.validate import validate_collection

    assert RouteBundle.__name__ == "RouteBundle"
    assert callable(validate_collection)
