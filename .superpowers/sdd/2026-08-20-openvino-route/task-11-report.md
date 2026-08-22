# Task 11 report: sealed offline Granite converter

## Outcome

Task 11 is complete locally from base `9a862570`. Dense Granite Safetensors
sources now have a retained-handle inspection path, and the converter runs from
an exact, hash-locked CPython/wheel closure with isolated flags, offline-only
environment, blocked sockets, fixed stdin/stdout JSON, sanitized failures, and a
complete FP16 model/tokenizer/detokenizer export.

`DEP-02` is closed by the approved correction from the nonexistent
`optimum==2.1.0` pin to the compatible exact `optimum==2.3.0` pin. The resolver
produced 55 CPython 3.13 Windows x64 wheels, each bound by filename, length, and
SHA-256 in both the requirements lock and wheel manifest. A second independent
`--require-hashes` download passed the verifier; a one-byte wheel mutation
failed with only `runtime_integrity_failed`.

## Security and integrity properties

- Source admission is the exact dense tuple `granite` /
  `GraniteForCausalLM` / `text-generation-with-past`.
- Source files are regular, flat, allowlisted resources held with retained read
  leases and bound to a deterministic relative-path/length/SHA-256 manifest.
- Safetensors have exact single-or-canonical-sharded ownership, valid dtype /
  shape / offsets, and contiguous complete payload coverage. Pickle/bin,
  scripts, native files, remote-code metadata, MoE, hybrid, multimodal, and
  speech sources fail closed.
- The worker launches only as `python.exe -I -s -E -S -B -m converter`, imports
  exclusively beneath its staged root, never adds source data to `sys.path`,
  denies socket creation/name lookup/connect audit events, and suppresses native
  stderr at descriptor level.
- Runtime profile/cache/temp paths are operation-owned synthetic scratch paths;
  Hub use is forced offline and OpenVINO telemetry is pre-disabled.
- Builder closure, build, and stage roots must be pairwise non-overlapping. The
  stage is read-only and its manifest enumerates every non-manifest file with
  exact length and SHA-256.
- Parent-visible events use a bounded, strict, duplicate/unknown-field-rejecting
  protocol. Raw paths, environment, exceptions, and native traces are not
  emitted.

## Reproducible fixture and sealed build

The repository includes the MIT synthetic `TinyGraniteV1` source generated with
the exact pinned Transformers/Torch versions and seed. Its seven source files
are independently verified against `manifest.json`; fixture byte preservation
and the Safetensors ignore exception are scoped to that fixture.

Final clean Stage J:

- manifest SHA-256:
  `8946d3e3d20558b39a6f2a41276e0b2de44007cfd180cf365fffb53d889e4f3c`
- manifested runtime files: 23,756
- duplicate, missing, extra, mismatched, and bytecode entries: zero
- repository fixture manifest SHA-256:
  `44704111e5ddebd22d2fdff40a258f4210f688e187338dfe43d4419638e91781`

The Stage J production worker exported the repository fixture with network
denied, empty stderr, typed completion, all model/tokenizer/detokenizer IR
artifacts, unchanged telemetry consent, empty Hub cache, and no bytecode.

## Verification

- Source inspector: 15/15 passed.
- Dependency lock contracts: 6/6 passed.
- Complete OpenVINO route unit suite with official closure: 196/196 passed,
  zero skipped.
- Complete worker-process integration suite with converter Stage I and official
  native Stages A/B: 47/47 applicable passed; the sole skip is the deliberately
  gated physical Intel GPU test (`GPU-01` remains open on this host).
- Fresh corrected Stage J build: `converter_worker_built`.
- Stage J manifest-verified offline export: 1/1 passed in 34 seconds.
- Release x64 application build with the pinned official worker closure:
  succeeded with zero errors (one pre-existing missing publish-profile warning).
- `git diff --check`: clean apart from checkout line-ending notices.

The user directed inline execution, so review was an inline security/diff audit.
It caught and corrected the ignored Safetensors fixture and pairwise build-root
overlap before this task was closed.
