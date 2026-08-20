# Local GGUF chat

The current executable preview includes the approved chat layout, streaming UI,
centered Stop control, multiple conversations, dated history groups, full saved
prompt/response transcripts, and history restoration after an app restart.

From the repository root, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Run-ChatPreview.ps1
```

When Granite Edge AI opens, select **Preview Chat** in the upper-right corner.
The preview runtime is deterministic and does not require a downloaded model.
Its history is stored under `%LOCALAPPDATA%\GraniteEdgeAI\ChatHistory`.

The message composer can select `.txt` and `.md` knowledge files. These
selections are presentation-only: every selected file is labelled **Not
indexed**, and its contents do not influence prompts or model context yet.
Only the file name is shown in the chat interface.

The protected command-line runtime is implemented and exercised against a
deterministic fake CLI. Real GGUF inference still requires an authorized local
`llama.cpp` CPU build and an inference-capable GGUF model; neither artifact is
downloaded or committed by this repository.

Run all local verification with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\gguf-runtime\Invoke-GgufChatVerification.ps1
```

Production packaging is opt-in through `GgufRuntimeInputRoot` plus the exact
source commit, build identity, and build flags. The controlled real-model test
must pass before a staged CLI build is described as supported.
