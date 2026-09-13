# TurboQuant source and binary licensing

The recovered implementation is part of `openvinotoolkit/openvino` main commit
`f5f594dc0c9e5961785f0d17743486d52eac87e7` and is licensed
under Apache-2.0. Its implementation entered upstream through pull request
`#35853`, merge commit `b9a1f201c109e0bed74763934f79483cf6c4cbf4`.

No source from the reviewed llama.cpp TurboQuant forks is copied into this
closure. The compatible OpenVINO GenAI main commit
`6fbc103538d30d42da4b0b5130a4792a20f728ba` remains governed by its own
Apache-2.0 and third-party notices staged with the worker closure.

The build must stage the upstream Apache license and complete runtime/GenAI
third-party notices. The worker is registered only when its exact source,
runtime, patch ledger, and package manifests all verify.
