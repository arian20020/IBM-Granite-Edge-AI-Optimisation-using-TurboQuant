# G1 GGUF chat MVP verification

## Verified locally

- Contract, framing, capability, worker, client, and process suites run without
  network acquisition.
- Deterministic child-process coverage includes two turns, streaming, Stop,
  forced reload, stderr bounds, inherited-handle policy, and Job containment.
- The self-contained WinUI preview builds and launches on Windows x64.
- Preview conversations are stored atomically per user and regrouped by local
  date after restart.
- Runtime packaging accepts only an explicit local input root and creates a
  strict hash manifest for the supervisor, CLI, dependencies, and license.

## Controlled result not yet available

No approved local inference-capable GGUF model or pinned Windows x64
`llama-cli.exe` closure was present. The controlled test therefore reports an
explicit skip. The supplied planning pack contains requirements and workflow
evidence, not those executable artifacts.

The deterministic fixture markers are not treated as llama.cpp behavior.
Official llama.cpp documentation describes `--simple-io` as basic I/O for
subprocess compatibility, but does not promise the fixture's ready/start/done
markers. Real-model acceptance remains blocked until an exact pin is staged and
its observed boundaries pass the controlled smoke.

Primary source: <https://github.com/ggml-org/llama.cpp/blob/master/tools/cli/README.md>
