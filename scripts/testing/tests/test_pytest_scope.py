"""Regression coverage for repository-owned pytest collection scope."""

from configparser import ConfigParser
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]


def test_pytest_scope_is_limited_to_repository_harness():
    config = ConfigParser()
    loaded = config.read(ROOT / "pytest.ini", encoding="utf-8")

    assert loaded == [str(ROOT / "pytest.ini")]
    pytest_config = config["pytest"]
    assert pytest_config["testpaths"] == "scripts/testing/tests"
    assert {"external", "experiments/raw-results"}.issubset(
        pytest_config["norecursedirs"].split()
    )
    assert pytest_config["python_classes"] == "*Tests"
