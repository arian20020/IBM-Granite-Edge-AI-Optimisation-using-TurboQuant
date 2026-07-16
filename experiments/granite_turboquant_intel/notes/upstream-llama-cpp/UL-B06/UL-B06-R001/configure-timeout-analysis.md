# UL-B06-R001 Configure Wrapper Timeout

The outer PowerShell execution wrapper reached its 120-second timeout, but the captured CMake process completed configuration and generation in 20.1 seconds and produced both `CMakeCache.txt` and `build.ninja`. The cache records `GGML_SYCL=ON`, the oneAPI 2026.1 compiler, SYCL, oneDNN, and oneMKL.

CMake warned that direct Level Zero loader or headers were not found and disabled `GGML_SYCL_SUPPORT_LEVEL_ZERO_API`. This does not by itself prove or disprove runtime SYCL device execution; runtime device evidence is required. R001 remains a harness-timeout record. R002 uses the valid generated tree for compilation.
