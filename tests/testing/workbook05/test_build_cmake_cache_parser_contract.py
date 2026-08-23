from __future__ import annotations

import unittest
from pathlib import Path


# Resolve the exact committed Workbook 05 control files so this regression
# protects the real architecture rather than a synthetic fixture.
REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
MODULE = REPOSITORY_ROOT / "scripts/testing/workbook05/Workbook05.Build.psm1"
RUNTIME = (
    REPOSITORY_ROOT
    / "scripts/testing/workbook05/Invoke-Workbook05RouteARuntimeBuild.ps1"
)
ROUTE_B = REPOSITORY_ROOT / "scripts/testing/workbook05/Invoke-Workbook05RouteBBuild.ps1"


class BuildCMakeCacheParserContractTests(unittest.TestCase):
    """Keep Runtime and Route B on one reviewed cache-parser implementation."""

    def test_shared_module_exports_cache_parser_and_both_routes_use_it(self) -> None:
        # Decode strictly so malformed source text cannot masquerade as a valid
        # parser refactor.
        module_text = MODULE.read_text(encoding="utf-8", errors="strict")
        runtime_text = RUNTIME.read_text(encoding="utf-8", errors="strict")
        route_b_text = ROUTE_B.read_text(encoding="utf-8", errors="strict")

        # Require one public shared primitive in the reviewed build module.
        self.assertIn("function Get-Wb05CMakeCacheValue", module_text)
        self.assertIn("'Get-Wb05CMakeCacheValue'", module_text)

        # Require both live CMake-cache consumers to call the same primitive.
        shared_call = "Get-Wb05CMakeCacheValue -Lines $cacheLines -Name $name"
        self.assertIn(shared_call, runtime_text)
        self.assertIn(shared_call, route_b_text)

        # Prevent the production-proven private parser copies from returning.
        self.assertNotIn("function Get-CMakeCacheValue", runtime_text)
        self.assertNotIn("function Get-CMakeCacheValue", route_b_text)


if __name__ == "__main__":
    unittest.main()
