# Workbook 05 Route A Phase 2 Closure

## Decision

```text
Campaign: GTQ-WB05-MF-v1
Route: route-a-merged-openvino
Phase: phase-2-documented-build
Phase 2 status: BuildCandidate
Runtime component: Passed
GenAI component: Passed
Model execution authorised: no
TurboQuant activation claim authorised: no
Packed-storage claim authorised: no
Performance claim authorised: no
Quality claim authorised: no
```

Route A has completed the Workbook 05 Phase 2 documented-build boundary. The accepted result is deliberately classified as `BuildCandidate`, not as a model-execution or optimisation result. It proves that one exact OpenVINO Runtime and OpenVINO GenAI source pair was configured, built, installed, retained, and independently validated on the controlled Intel Windows runner.

The accepted project repository head for both final component runs was:

```text
ec454afde54a309b57ce9117ab12c602fe58b15c
```

## Accepted source pair

| Component | Repository | Exact source commit | Accepted local install |
|---|---|---|---|
| OpenVINO Runtime | `https://github.com/openvinotoolkit/openvino.git` | `b9a1f201c109e0bed74763934f79483cf6c4cbf4` | `C:\w5a\phase2-31391119557-4\i-ov` |
| OpenVINO GenAI | `https://github.com/openvinotoolkit/openvino.genai.git` | `bd8d6542e3ca1ac30042d5d8d4202ce00b5f4af0` | `C:\w5a\phase2-31661571860-1\i-genai` |

The escaped record forms retained for cross-platform text comparison are:

```text
C:\\w5a\\phase2-31391119557-4\\i-ov
C:\\w5a\\phase2-31661571860-1\\i-genai
```

The Runtime installation was consumed read-only by the GenAI build. GenAI used a separate fresh external workspace and resolved against the single installed `OpenVINOConfig.cmake` from the accepted Runtime.

## Runtime acceptance evidence

### Controlling workflow

- Workflow: `Workbook 05 Route A Runtime controlled resume`
- Workflow run: `31656417607`
- Run attempt: `1`
- Branch: `main`
- Project head: `ec454afde54a309b57ce9117ab12c602fe58b15c`
- Collector conclusion: `success`
- Independent hosted-validator conclusion: `success`

### Retained artifact

- Artifact name: `workbook-05-build-route-a-runtime-resume-31656417607-1`
- Artifact ID: `9165704574`
- Artifact size: `88,605` bytes
- GitHub-recorded SHA-256: `6fb86648780faf7e142cee2260369a567a228ed0ae1147d2034c2fd3f32e5fca`
- Independently verified SHA-256: `6fb86648780faf7e142cee2260369a567a228ed0ae1147d2034c2fd3f32e5fca`
- `decision.json` SHA-256: `5dae00b38edb9a20f2d82a99dcf4cd1e2d4e7aeaaf6984873ccc55abccf3ae38`
- GitHub retention expiry: `2026-09-12T02:03:49Z`

### Accepted permanent hand-off

```text
Runtime installation:
C:\w5a\phase2-31391119557-4\i-ov

Accepted Runtime decision:
C:\w5a\accepted-route-a-runtime-31656417607-1\decision.json
```

The project owner independently verified the downloaded ZIP, the decision hash, the normal-directory boundary, the two required ONNX and TensorFlow frontend headers, and the single installed `OpenVINOConfig.cmake`. The final local acceptance result was `PASSED`.

### Runtime command and resource outcome

| Evidence | Result |
|---|---:|
| Configure exit code | `0` |
| Resumed build exit code | `0` |
| Install exit code | `0` |
| Build elapsed time | `3,505.876 seconds` |
| Configure elapsed time | `43.684 seconds` |
| Install elapsed time | `2.531 seconds` |
| Build safety stop | `false` |
| Peak process-tree working set | `2,664,960,000 bytes` |
| Peak process-tree private bytes | `2,874,449,920 bytes` |
| Minimum available physical memory | `7,596,015,616 bytes` |
| Maximum Windows commit use | `62%` |
| Retained binary records | `32` |
| Captured MSBuild summary | `39 warnings, 0 errors` |

The successful resume reused only the previously validated incremental build state. It retained one CMake build job and one MSVC compiler process, then installed and checked the source-matched Runtime hand-off.

## GenAI acceptance evidence

### Controlling workflow

- Workflow: `Workbook 05 documented build`
- Stage: `route-a-genai`
- Workflow run: `31661571860`
- Run attempt: `1`
- Branch: `main`
- Project head: `ec454afde54a309b57ce9117ab12c602fe58b15c`
- Collector conclusion: `success`
- Independent hosted-validator conclusion: `success`

### Retained artifact

- Artifact name: `workbook-05-build-route-a-genai-31661571860-1`
- Artifact ID: `9167835183`
- Artifact size: `678,981` bytes
- GitHub-recorded SHA-256: `a70492bdabc6ce5a9334b9a43eb51309193f65ee4452b823329c6c9b573ae023`
- Independently verified SHA-256: `a70492bdabc6ce5a9334b9a43eb51309193f65ee4452b823329c6c9b573ae023`
- `decision.json` SHA-256: `0f273e1f345e1512e8e159af9b83f9ad521a26aa5e0ae813f2599161a5cb4a79`
- GitHub retention expiry: `2026-09-12T04:01:50Z`

### Accepted permanent hand-off

```text
GenAI installation:
C:\w5a\phase2-31661571860-1\i-genai

Accepted GenAI decision:
C:\w5a\accepted-route-a-genai-31661571860-1\decision.json
```

The project owner independently verified the complete ZIP, every entry in `manifest.sha256`, the GenAI decision, the Runtime/GenAI compatibility record, the retained Runtime-decision hash, the normal-directory boundary, and the required installed GenAI DLL and Python extension files. The final local acceptance result was `PASSED`.

### GenAI command and resource outcome

| Evidence | Result |
|---|---:|
| Configure exit code | `0` |
| Build exit code | `0` |
| Install exit code | `0` |
| Build elapsed time | `4,397.871 seconds` |
| Configure elapsed time | `248.222 seconds` |
| Install elapsed time | `3.000 seconds` |
| Build safety stop | `false` |
| Peak process-tree working set | `4,086,177,792 bytes` |
| Peak process-tree private bytes | `3,998,498,816 bytes` |
| Minimum available physical memory | `6,068,125,696 bytes` |
| Maximum Windows commit use | `72%` |
| Retained binary records | `18` |
| Captured MSBuild summary | `18,589 warnings, 0 errors` |

The `18,589 warnings` are retained as build technical debt. They do not change the zero-error build result, but this closure does not describe the source pair as warning-free. Any later source revision or warning-reduction change requires a new configuration identity and a new build qualification.

## What Phase 2 proves

The accepted evidence proves that:

1. the exact Runtime and GenAI source revisions were used;
2. recursive source acquisition, configuration, compilation, and installation completed;
3. GenAI resolved against the accepted source-built Runtime rather than an unrelated system package;
4. required installed headers and principal Runtime/GenAI outputs were present;
5. configure, build, and install command records ended with exit code `0`;
6. resource watchdog thresholds did not trigger;
7. binary metadata and SHA-256 values were retained without uploading binaries in the evidence artifacts;
8. both artifacts were independently validated as untrusted data on GitHub-hosted runners;
9. the project owner independently preserved and rechecked the accepted artifacts and decisions.

## What Phase 2 does not prove

No Granite model was downloaded or executed by this stage. Phase 2 therefore provides no evidence for:

- model loading or tokenizer compatibility with IBM Granite 4.1;
- standard-cache inference correctness;
- TurboQuant U3 or U4 activation;
- independent requested and selected K/V cache formats;
- fallback absence;
- packed K or V record sizes;
- full KV-cache memory allocation;
- time to first token;
- prompt-processing throughput;
- decode throughput or time per output token;
- peak inference RAM;
- maximum stable context;
- perplexity;
- P1–P6 output quality;
- quality preservation relative to a matched baseline.

Accordingly, every scientific authorisation flag remains `false` in the route decision.

## Route B remains separate

Route B remains closed as `Blocked` for the current Phase 2 matrix. The incomplete Route B repair evidence did not establish an independently validated and project-owner-accepted `ExecutableCandidate`, so no QJL or PolarQuant Runtime, GenAI, activation, packed-storage, performance, or quality claim is admitted by this Route A closure.

No conclusion is drawn that QJL or PolarQuant cannot work. The narrower conclusion is that the current retained Route B evidence is insufficient to include those codecs in this campaign stage.

## Next controlled checkpoint

The next authorised work is implementation and verification of the Phase 3 and measured-run foundations, in this order:

1. freeze the diagnostic and IBM Granite 4.1 model/tokenizer assets by repository, revision, filenames, precision, licence, hashes, context limit, and local path;
2. implement the child-process inference harness with raw stdout/stderr, complete output retention, resource sampling, watchdog, cooldown, retry, and durable resume checkpoints;
3. implement requested-versus-selected K/V activation, fallback, packed-record, and full KV-allocation evidence;
4. implement deterministic and rubric-based quality records using the frozen P1–P6 controls;
5. run one standard-cache diagnostic smoke sequence: one excluded pilot, one excluded warm-up, and at least three measured repetitions;
6. independently validate that evidence before any compressed capability sweep begins.

Only after these gates pass may the campaign order executable K/V configurations by measured total storage and begin the low-memory-first diagnostic and Granite 3B frontier stages.
