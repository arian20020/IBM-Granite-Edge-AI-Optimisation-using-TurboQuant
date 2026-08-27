# TurboQuant source and binary licensing

The recovered implementation is part of `openvinotoolkit/openvino` release
`2026.3.0` at commit `8a17657b995fd3b4a52f8484acfcf2bb61214623` and is licensed
under Apache-2.0. Its implementation entered upstream through pull request
`#35853`, merge commit `b9a1f201c109e0bed74763934f79483cf6c4cbf4`.

No source from the reviewed llama.cpp TurboQuant forks is copied into this
closure. The compatible OpenVINO GenAI release remains governed by its own
Apache-2.0 and third-party notices recorded in `../openvino-official`.

The build must stage the upstream Apache license and complete runtime/GenAI
third-party notices. External security and license approval for experimental
application exposure remains pending. Until both reviews are recorded, the
TurboQuant worker must not be registered or packaged.
