# Research-to-development handoff

> **Document status:** Step-by-step implementation handoff
> **Version:** 3.0
> **Last updated:** 14 July 2026

## Purpose

Research is ready for development only when the next developer can identify the decision, supporting evidence, implementation boundary, acceptance criteria and remaining uncertainty. This document turns the research folders into small development increments.

## Stage 1: Freeze the first application milestone

Build only:

- packaged WinUI 3 shell;
- first-launch model-import page;
- `.gguf` file selection;
- OpenVINO `.xml` and `.bin` pair selection;
- simple validation messages;
- navigation to a placeholder chat screen after a valid model is selected.

Do not add inference, deep model parsing or TurboQuant into the first UI milestone.

## Stage 2: Define shared model records

Create application-level records for:

- model path and format;
- display name;
- size on disk;
- model family and architecture where known;
- weight format;
- expected backend;
- validation warnings;
- hardware-fit result.

The UI should depend on these records rather than reading backend-specific objects directly.

## Stage 3: Implement the llama.cpp diagnostic path

1. Add a backend service that starts `llama-cli` safely.
2. Use a small diagnostic GGUF model.
3. Stream generated output.
4. Capture warnings and exit codes.
5. Implement cancellation.
6. Store the command and log file.
7. Show a simple success or error result in the application.

## Stage 4: Validate Granite through llama.cpp

1. Select the exact Granite model revision.
2. Verify or create the GGUF conversion.
3. Test loading before integrating it into the UI.
4. Record unsupported architecture or tokenizer errors.
5. Add the working configuration to the model catalogue.

## Stage 5: Implement the OpenVINO diagnostic path

1. Validate XML/BIN model pairs.
2. Create the OpenVINO Core or GenAI pipeline.
3. Query available devices.
4. Run a CPU baseline.
5. Add GPU and NPU tests only when supported.
6. Return requested device, actual device and fallback information.

## Stage 6: Add common chat behaviour

- conversation state;
- prompt submission;
- token streaming;
- stop generation;
- model-load progress;
- clear error messages;
- optional local chat saving;
- no mandatory server port.

## Stage 7: Add metrics

Expose:

- model-load time;
- time to first token;
- tokens per second;
- peak memory where measurable;
- context length;
- weight and KV formats;
- requested and actual device.

## Stage 8: Add optimisation modes only from evidence

- **Quality:** highest tested quality that fits the machine.
- **Balanced:** tested compromise between quality, memory and speed.
- **Efficiency:** lowest-memory tested configuration that still passes the quality gate.
- **Automatic:** selects among tested profiles using hardware and model compatibility.

Do not hard-code attractive names onto untested configurations.

## Stage 9: Isolate TurboQuant experiments

1. Keep experimental forks outside the production application repository or in a clearly separated experiment area.
2. Build the pinned upstream baseline.
3. Reproduce the candidate fork.
4. Identify the exact algorithm stages.
5. Run Granite quality and performance tests.
6. Write an architecture decision before bringing code into the product backend.

## Stage 10: Definition of done for a backend feature

A backend feature is done only when:

- code builds in a clean environment;
- automated or repeatable tests pass;
- failure behaviour is tested;
- logs and metrics are captured;
- documentation and change log are updated;
- no secrets or local absolute paths are committed;
- a pull request explains what changed, why it changed, evidence, limitations and rollback.

## Suggested small commits

```text
feat(model-import): validate GGUF selection
feat(model-import): validate OpenVINO XML and BIN pair
feat(runtime): add llama.cpp diagnostic process runner
feat(runtime): stream and cancel llama.cpp generation
feat(runtime): add OpenVINO CPU diagnostic pipeline
feat(metrics): normalise generation measurements
feat(chat): connect validated backend to chat page
docs(testing): record controlled Granite baseline
```

## Sources used

- `SRC-MS-WINDOWS-DESKTOP-2026`
- `SRC-BOOK-ENGINEERING-SOFTWARE-PRODUCTS`
- `SRC-BOOK-SYSTEMS-ENGINEERING`
- `SRC-BOOK-FUNDAMENTALS-SOFTWARE-ARCHITECTURE`
- `SRC-BOOK-UX-BOOK`
