# UL-B06-R002 SYCL Repository Test Failures

The SYCL build completed all 619 Ninja targets. CTest then passed 49 of 52 tests and failed three iGPU-backend tests:

- `test-llama-archs` crashed with Windows status `0xc0000409` after extensive Intel UHD Graphics execution.
- `test-backend-ops` reached a `SET_ROWS` case requiring FP64, which the Intel UHD device reports as unsupported.
- `test-save-load-state` reported `UR_RESULT_ERROR_DEVICE_LOST` while waiting for the Level Zero queue and then terminated with `0xc0000409`.

The device inventory proves that `SYCL0` maps to the Intel UHD Graphics through the oneAPI Level Zero runtime. These are backend/device failures, not Python/Jinja2 failures. The broad SYCL CTest gate must remain labelled 49/52 with three failures and must not be silently replaced by the OpenCL CPU device. Separately, the pinned Granite Q4_K_M project workload completed successfully on `SYCL0`; that workload may be labelled passed only with the edge-suite limitation retained.
