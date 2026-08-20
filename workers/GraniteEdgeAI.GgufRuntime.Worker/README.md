# GGUF runtime supervisor

This Windows x64 process owns one CLI child at a time. It receives framed
commands over inherited standard streams, launches without a shell, keeps the
process tree in a kill-on-close Job, bounds output, and separates Stop from
session Close.

The deterministic integration fixture uses `__G1_READY__`,
`__G1_RESPONSE_START__`, and `__G1_RESPONSE_DONE__` markers. These are fixture
protocol markers, not documented llama.cpp output. A real CLI pin is accepted
only after its actual `--simple-io` boundaries are captured and the controlled
real-model smoke passes.
