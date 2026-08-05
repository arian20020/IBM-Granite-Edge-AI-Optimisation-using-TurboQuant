from __future__ import annotations

import sys
from pathlib import Path


def _value(flag: str) -> str:
    index = sys.argv.index(flag)
    return sys.argv[index + 1]


source = Path(_value("-S"))
build = Path(_value("-B"))
generator = _value("-G")
architecture = _value("-A")
gpu = next(value.split("=", 1)[1] for value in sys.argv if value.startswith("-DENABLE_INTEL_GPU="))
build.mkdir(parents=True, exist_ok=True)
(build / "CMakeCache.txt").write_text(
    "\n".join(
        [
            f"CMAKE_GENERATOR:INTERNAL={generator}",
            f"CMAKE_GENERATOR_PLATFORM:INTERNAL={architecture}",
            f"ENABLE_INTEL_GPU:BOOL={gpu}",
            f"CMAKE_HOME_DIRECTORY:INTERNAL={source.as_posix()}",
            "",
        ]
    ),
    encoding="utf-8",
)
print("Fake configure completed without compilation.")
