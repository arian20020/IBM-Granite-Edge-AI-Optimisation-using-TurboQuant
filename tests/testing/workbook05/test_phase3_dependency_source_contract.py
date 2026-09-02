from __future__ import annotations

import importlib
import tempfile
import unittest
from pathlib import Path
from types import ModuleType

from scripts.testing.workbook05.phase3.conversion import (
    OPTIMUM_COMMIT,
    REVIEWED_OPTIMUM_INTEL_CONSTRAINTS,
)


REVIEWED_OPTIMUM_CONSTRAINTS: tuple[str, ...] = (
    "transformers>=4.29",
    "torch>=1.11",
    "packaging",
    "numpy",
    "huggingface_hub>=0.8.0",
)

REVIEWED_NORMAL_REQUIREMENT_INPUT: tuple[str, ...] = (
    "transformers==5.5.0",
    "huggingface-hub==1.21.0",
    "nncf==3.2.0",
    "openvino==2026.2.1",
    "openvino-tokenizers==2026.2.1.0",
    "torch>=2.1",
    "safetensors<0.8.0",
    "setuptools",
    "requests>=2.33,<3.0",
    "packaging",
    "numpy",
    "wheel",
)

_ENTRY_POINT = "optimum-cli=optimum.commands.optimum_cli:main"


def _canonical_name(value: str) -> str:
    """Return the canonical distribution name used by the assertions."""

    result: list[str] = []
    previous_dash = False
    for character in value.casefold():
        if character in "-_.":
            if not previous_dash:
                result.append("-")
            previous_dash = True
        else:
            result.append(character)
            previous_dash = False
    return "".join(result)


def _write_source_fixture(
    root: Path,
    *,
    name: str,
    version: str,
    requirements: tuple[str, ...],
    entry_point: str = _ENTRY_POINT,
    dependency_expression: str | None = None,
    pyproject_text: str = "[tool.black]\nline-length = 119\n",
) -> None:
    """Create inert source metadata matching one reviewed upstream layout."""

    if name == "optimum":
        requirement_name = "REQUIRED_PKGS"
        version_path = root / "optimum" / "version.py"
    elif name == "optimum-intel":
        requirement_name = "INSTALL_REQUIRE"
        version_path = root / "optimum" / "intel" / "version.py"
    else:
        raise ValueError(f"Unsupported fixture name: {name}")

    root.mkdir(parents=True, exist_ok=False)
    version_path.parent.mkdir(parents=True, exist_ok=True)
    version_path.write_text(
        f'__version__ = "{version}"\n',
        encoding="utf-8",
        newline="\n",
    )
    (root / "pyproject.toml").write_text(
        pyproject_text,
        encoding="utf-8",
        newline="\n",
    )

    requirement_value = (
        dependency_expression
        if dependency_expression is not None
        else repr(list(requirements))
    )
    (root / "setup.py").write_text(
        "\n".join(
            (
                'raise RuntimeError("setup.py metadata must never execute")',
                "from setuptools import setup",
                "",
                f"{requirement_name} = {requirement_value}",
                "",
                "setup(",
                f'    name="{name}",',
                "    version=__version__,",
                f"    install_requires={requirement_name},",
                (
                    "    entry_points={"
                    f'"console_scripts": [{entry_point!r}]'
                    "},"
                ),
                ")",
                "",
            )
        ),
        encoding="utf-8",
        newline="\n",
    )


def _write_reviewed_pair(
    root: Path,
    *,
    optimum_version: str = "2.3.0",
    optimum_intel_version: str = "2.2.0.dev0",
    optimum_requirements: tuple[str, ...] = REVIEWED_OPTIMUM_CONSTRAINTS,
    optimum_intel_requirements: tuple[str, ...] = (
        REVIEWED_OPTIMUM_INTEL_CONSTRAINTS
    ),
    optimum_entry_point: str = _ENTRY_POINT,
    optimum_intel_entry_point: str = _ENTRY_POINT,
    optimum_pyproject: str = "[tool.black]\nline-length = 119\n",
    optimum_intel_pyproject: str = "[tool.black]\nline-length = 119\n",
) -> tuple[Path, Path]:
    """Create both exact reviewed source fixtures under one temporary root."""

    optimum_root = root / "optimum"
    optimum_intel_root = root / "optimum-intel"
    _write_source_fixture(
        optimum_root,
        name="optimum",
        version=optimum_version,
        requirements=optimum_requirements,
        entry_point=optimum_entry_point,
        pyproject_text=optimum_pyproject,
    )
    _write_source_fixture(
        optimum_intel_root,
        name="optimum-intel",
        version=optimum_intel_version,
        requirements=optimum_intel_requirements,
        entry_point=optimum_intel_entry_point,
        pyproject_text=optimum_intel_pyproject,
    )
    return optimum_root, optimum_intel_root


class Phase3DependencySourceContractTests(unittest.TestCase):
    """Prove source metadata is inspected as data and cannot drift silently."""

    def _module(self) -> ModuleType:
        module_name = (
            "scripts.testing.workbook05.phase3.dependency_source_contract"
        )
        try:
            return importlib.import_module(module_name)
        except ModuleNotFoundError as error:
            if error.name != module_name:
                raise
            self.fail(
                "Task 3 dependency_source_contract module has not been "
                "implemented."
            )

    def test_exact_reviewed_sources_are_parsed_without_execution(self) -> None:
        module = self._module()
        with tempfile.TemporaryDirectory() as temporary:
            optimum_root, optimum_intel_root = _write_reviewed_pair(
                Path(temporary)
            )

            report = module.validate_reviewed_source_contracts(
                optimum_root,
                optimum_intel_root,
            )

        self.assertEqual("1.0", report["schema_version"])
        self.assertEqual(
            "dependency-source-contract",
            report["record_type"],
        )
        self.assertFalse(report["source_metadata_execution"])
        self.assertFalse(report["build_system_declared"])
        self.assertEqual(
            "setuptools-no-build-isolation",
            report["installer_build_mode"],
        )
        self.assertEqual(
            ["setuptools", "wheel"],
            report["reviewed_build_tools"],
        )
        self.assertEqual(
            "2.3.0",
            report["sources"]["optimum"]["base_version"],
        )
        self.assertEqual(
            "2.2.0.dev0",
            report["sources"]["optimum-intel"]["base_version"],
        )
        self.assertEqual(
            [_ENTRY_POINT],
            report["sources"]["optimum"]["console_entry_points"],
        )
        self.assertEqual(
            [_ENTRY_POINT],
            report["sources"]["optimum-intel"][
                "console_entry_points"
            ],
        )

    def test_dynamic_dependency_construction_is_rejected(self) -> None:
        module = self._module()
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary) / "optimum-intel"
            first = repr(list(REVIEWED_OPTIMUM_INTEL_CONSTRAINTS[:5]))
            second = repr(list(REVIEWED_OPTIMUM_INTEL_CONSTRAINTS[5:]))
            _write_source_fixture(
                root,
                name="optimum-intel",
                version="2.2.0.dev0",
                requirements=REVIEWED_OPTIMUM_INTEL_CONSTRAINTS,
                dependency_expression=f"{first} + {second}",
            )

            with self.assertRaisesRegex(ValueError, "literal"):
                module.inspect_source_contract("optimum-intel", root)

    def test_unexpected_build_system_is_rejected(self) -> None:
        module = self._module()
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary) / "optimum"
            _write_source_fixture(
                root,
                name="optimum",
                version="2.3.0",
                requirements=REVIEWED_OPTIMUM_CONSTRAINTS,
                pyproject_text=(
                    "[build-system]\n"
                    'requires = ["setuptools", "wheel"]\n'
                    'build-backend = "setuptools.build_meta"\n'
                ),
            )

            with self.assertRaisesRegex(ValueError, "build-system"):
                module.inspect_source_contract("optimum", root)

    def test_changed_source_contract_fields_are_rejected(self) -> None:
        module = self._module()
        cases = (
            {
                "name": "changed optimum version",
                "kwargs": {"optimum_version": "2.3.1"},
                "message": "version",
            },
            {
                "name": "changed optimum-intel version",
                "kwargs": {"optimum_intel_version": "2.2.1.dev0"},
                "message": "version",
            },
            {
                "name": "changed entry point",
                "kwargs": {
                    "optimum_intel_entry_point": (
                        "optimum-cli=unexpected.module:main"
                    )
                },
                "message": "entry point",
            },
            {
                "name": "missing requirement",
                "kwargs": {
                    "optimum_intel_requirements": (
                        REVIEWED_OPTIMUM_INTEL_CONSTRAINTS[:-1]
                    )
                },
                "message": "runtime requirements",
            },
            {
                "name": "additional requirement",
                "kwargs": {
                    "optimum_requirements": (
                        REVIEWED_OPTIMUM_CONSTRAINTS
                        + ("unexpected-package>=1",)
                    )
                },
                "message": "runtime requirements",
            },
        )

        for case in cases:
            with self.subTest(case=case["name"]):
                with tempfile.TemporaryDirectory() as temporary:
                    optimum_root, optimum_intel_root = _write_reviewed_pair(
                        Path(temporary),
                        **case["kwargs"],
                    )
                    with self.assertRaisesRegex(
                        ValueError,
                        case["message"],
                    ):
                        module.validate_reviewed_source_contracts(
                            optimum_root,
                            optimum_intel_root,
                        )

    def test_reviewed_normal_input_covers_every_source_requirement(self) -> None:
        module = self._module()
        with tempfile.TemporaryDirectory() as temporary:
            optimum_root, optimum_intel_root = _write_reviewed_pair(
                Path(temporary)
            )
            report = module.validate_reviewed_source_contracts(
                optimum_root,
                optimum_intel_root,
            )

        requirements = module.build_reviewed_normal_requirement_input()
        self.assertEqual(REVIEWED_NORMAL_REQUIREMENT_INPUT, requirements)

        canonical_names = tuple(
            _canonical_name(
                requirement.split("=", 1)[0]
                .split("<", 1)[0]
                .split(">", 1)[0]
                .strip()
            )
            for requirement in requirements
        )
        self.assertEqual(len(canonical_names), len(set(canonical_names)))
        self.assertNotIn("optimum", canonical_names)
        self.assertNotIn("optimum-intel", canonical_names)

        expected_source_requirements = {
            ("optimum", requirement)
            for requirement in REVIEWED_OPTIMUM_CONSTRAINTS
        } | {
            ("optimum-intel", requirement)
            for requirement in REVIEWED_OPTIMUM_INTEL_CONSTRAINTS
        }
        coverage = report["requirement_coverage"]
        observed_source_requirements = {
            (row["source_package"], row["source_requirement"])
            for row in coverage
        }
        self.assertEqual(
            expected_source_requirements,
            observed_source_requirements,
        )
        self.assertEqual(len(coverage), len(expected_source_requirements))

        optimum_coverage = next(
            row
            for row in coverage
            if row["source_package"] == "optimum-intel"
            and row["source_requirement"] == "optimum~=2.3.0"
        )
        self.assertEqual("reviewed-vcs-source", optimum_coverage["mode"])
        self.assertEqual(
            f"optimum@{OPTIMUM_COMMIT}",
            optimum_coverage["covered_by"],
        )

    def test_requirements_writer_is_deterministic_and_never_overwrites(
        self,
    ) -> None:
        module = self._module()
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary) / "requirements.phase3-assets.in"
            result = module.write_reviewed_normal_requirement_input(output)

            expected = (
                "\n".join(REVIEWED_NORMAL_REQUIREMENT_INPUT) + "\n"
            ).encode("utf-8")
            self.assertEqual(REVIEWED_NORMAL_REQUIREMENT_INPUT, result)
            self.assertEqual(expected, output.read_bytes())
            self.assertFalse(Path(str(output) + ".tmp").exists())

            with self.assertRaises(FileExistsError):
                module.write_reviewed_normal_requirement_input(output)


if __name__ == "__main__":
    unittest.main()
