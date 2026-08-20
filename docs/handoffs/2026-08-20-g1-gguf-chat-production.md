# G1 GGUF chat production handoff

## Branch and scope

- Branch: `feature/gguf-cli-chat-production`
- Base: `feature/gguf-cli-chat-runtime` at design closure `06d19eaa`
- Runtime: Windows x64 CPU, one local session, no server or listener
- Preview: self-contained deterministic WinUI stream with persistent history

## Runnable commands

Launch the visual preview:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Run-ChatPreview.ps1
```

Run the complete local verification set:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\gguf-runtime\Invoke-GgufChatVerification.ps1
```

## Production inputs still required

1. An approved reproducible Windows x64 CPU llama.cpp build.
2. Its exact 40-character source commit, build flags, dependency closure, and
   license material.
3. An approved inference-capable Granite GGUF model and SHA-256.
4. Controlled evidence that the pinned CLI's real `--simple-io` boundaries are
   compatible with the supervisor parser.

The build never downloads or searches for these inputs. Supplying a directory
through `GgufRuntimeInputRoot` opts into packaging; missing provenance fields or
closure members fail the build.

## Explicitly deferred

Vulkan, SYCL, TurboQuant, artifact quantization, attachments, and cross-route
ranking remain outside the CPU chat MVP.
