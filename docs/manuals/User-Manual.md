# Using Granite

Granite is a Windows desktop research application for running selected IBM Granite models on your own computer. This guide follows the normal app, not its developer test galleries.

If you only want to try the app, start with a verified package from the project owner. You do not need to build the source. See [installation](Build-and-Installation.md) first.

## Before you start

- Use the tested Windows 11 x64 setup where possible. The recorded checks centred on an Intel computer with about 16 GB of RAM; that is a test setup, not a guaranteed minimum.
- Allow space for the model, temporary work and any exported copy. Model files can be several gigabytes.
- Close memory-heavy programs if RAM is tight. Free RAM changes while other programs run.
- Keep the app's runtime files together. Do not replace individual DLLs or workers with downloads from elsewhere.
- Use public or non-sensitive test prompts. This is not a clinical system or an approved tool for real patient or pupil data.

A **runtime** is the software that loads the model and generates replies. A **GGUF** is a model file used by the llama.cpp route. An **OpenVINO package** is a folder of model and tokenizer files. A **KV cache** holds information used during generation; it is different from the model's weights.

## 1. Choose a model

There are two ways to begin.

**Download a recommended model:** select a preference, review the format and size, then choose **Download selected model**. This route needs internet access. The app checks the pinned file size and SHA-256 hash before accepting the download. A hash is a file fingerprint; a mismatch means the download cannot be trusted as the expected file.

**Use a local model:** choose **Choose model**, then select a GGUF file or an OpenVINO folder. Select the whole OpenVINO package, not just its XML file. Keep its weights, tokenizer and configuration files together.

The recommended download catalogue and the experiment models are separate. The app's recommended downloads use Granite 4.0 H Micro; the recorded inspection tests also used selected Granite 4.1 files. Do not assume that every Granite model is supported.

For local imports, wait for the quick scan. **Continue to model inspection** becomes available only when the selection is accepted. Verified downloads can move into inspection automatically. If a selection fails, use the recovery action shown; do not rename a file to make it look like another format.

## 2. Inspect the model

The app checks the package, configuration, tokenizer, structure and runtime support. Let the checks finish before moving on.

A successful result can offer **Check hardware fit**. A warning may offer a separate continuation action; read the warning before accepting it. A runtime or integrity failure is not a hardware-fit result.

If you cancel, wait for the operation to stop. Cancelling should not be treated as a successful check. Recovery buttons depend on the result and route; use the actions actually shown.

## 3. Check hardware fit

Hardware inspection collects local computer facts. Compatibility then compares the model's estimated needs with the memory available for it.

- A fit result is an estimate, not a guarantee of speed or successful execution under every load.
- An optimisation result means an alternative configuration is available.
- A memory block means the current safe budget is too small.
- **We can't answer this yet** means there is not enough usable information to reach a decision. It does not mean the model is definitely incompatible.

Use **Hardware facts** and **How we worked this out** when available. If RAM is low, close other programs and start a fresh check through the available recovery path.

## 4. Choose a configuration

Where offered, choose optimisation and review the selected weight format, KV-cache format and context length. Moving towards lower memory use can change quality or performance; it is not a promise of faster replies.

Choices depend on the model, verified runtime evidence, RAM and disk checks. You may see fewer choices than someone using another model or computer. Read [known limitations](Known-Limitations.md) before assuming a missing position is a fault.

The recommended-download slider chooses a file to download. The later configuration slider chooses an available setup for the selected model. They are not interchangeable.

Choose **Start optimisation** only after reviewing the setup. If the selection is no longer available, return to compatibility and check again through the offered navigation. Do not keep forcing an unavailable choice.

## 5. Optimise, save and chat

Wait for optimisation and validation to finish. Temporary output is not a finished model. The workflow is designed to preserve the imported source and validate outputs before offering them.

When **Save model to this computer** is offered, select a destination with enough free space and wait for export to complete. Runtime-only changes do not necessarily produce a new standalone model file. Do not move temporary files out of the app's working folders.

Use **Chat with this model** when available. Enter a short prompt first, then check the response. Use the displayed stop action to interrupt generation. Generated answers can be wrong; review them before relying on them.

## Offline use and local data

Download the required model and obtain the complete runtime package before going offline. The developer reported completing inspection, optimisation, export and chat with Wi-Fi disconnected. This was a manual check of one journey, not a full network audit or proof for every route.

Downloaded models use the packaged app's local-data folder under `GraniteEdgeAI/Models`. The GGUF chat-history store uses `%LOCALAPPDATA%/GraniteEdgeAI/ChatHistory`; optimisation work uses `%LOCALAPPDATA%/GraniteEdgeAI/Optimization`. These are implementation locations, not folders to edit during a running operation.

Export important models to a location you control. Before reinstalling or resetting the app, back up data you need. Uninstall behaviour has not been verified for every storage location, so do not assume it either keeps or removes everything.

## If something goes wrong

### Keep a backup before repair or removal

1. Finish or cancel active work, then close the app.
2. Copy exported models from the destination you chose into a separate backup folder. For an OpenVINO export, keep the whole folder together.
3. In File Explorer, enter `%LOCALAPPDATA%/GraniteEdgeAI`. If present, copy the ChatHistory folder to your backup. Do not treat the Optimization working folder as a finished export.
4. Packaged downloads are stored below the app's Windows local-data location. Do not guess the package folder or delete similar-looking folders. If you cannot locate it, ask the maintainer to identify the installed package.
5. Check that the backup files open or have matching file hashes before removing the original.

To use an exported model again, choose it through the normal import screen and let inspection run again. A runtime-only setting is not a portable model export. Automatic chat-history restoration and uninstall retention have not been verified; keep the backup and do not overwrite a live store to try to restore it.

### Chat controls

The chat page provides new-chat, history and model-import controls. Open the navigation pane if it is collapsed. Settings includes appearance choices such as system, light and dark theme. Controls can vary with the active route; a disabled control is not an instruction to edit stored files.

Use the normal model-import action to change models. Save anything important before closing a session. This guide does not promise that every route restores the same conversation history.

For a bug report, follow [support](../../SUPPORT.md). It explains what to share safely.

See the [troubleshooting table](Known-Limitations.md#troubleshooting). When reporting a problem, include the app/package identity, route, visible message and diagnostic code. Redact usernames, private paths and prompts from screenshots or logs. Never upload a private model or your whole local-data folder.
