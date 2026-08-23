# Token-limit completion and continuation design

Date: 2026-08-23

## Outcome

Granite Edge AI must distinguish a naturally completed response from one stopped by the configured generation budget. A length-limited response remains readable and copyable, shows an explicit status, and offers a keyboard-accessible **Continue generating** action that appends to the same assistant message. The runtime also receives a concise system instruction intended to improve directness and factual caution on small Granite instruct models.

This work does not claim that prompting can guarantee factual correctness. Model quality remains bounded by the selected model and quantization.

## Confirmed current behavior

- Production chat sets `MaximumGeneratedTokens` to 512.
- `LlamaSharpInferenceEngine` passes that value to `InferenceParams.MaxTokens`.
- LLamaSharp treats `MaxTokens` as a hard output-token limit, but `ChatSession.ChatAsync` exposes only streamed text and no terminal reason.
- The adapter always emits `G1DONE`; the worker always maps that frame to `ResponseCompletedEvent`; the UI therefore labels both EOS and budget exhaustion as completed.
- The reported response ended mid-outline at approximately the configured budget. The per-message copy click was coincidental; the copy event does not call the stop path.

## Source-backed decisions

1. Preserve the model's embedded chat template. IBM's Granite examples use `apply_chat_template`, and LLamaSharp recommends `PromptTemplateTransformer` for model-native chat formatting. The implementation must add a system-role history message and allow the existing transformer to format it; it must not hard-code Granite control tokens.
2. Keep the system prompt short. IBM's small-model prompting guidance recommends concise, direct, positively framed instructions because prompt tokens consume limited context and smaller models respond better to focused constraints.
3. Treat length as a distinct terminal reason. llama.cpp's OpenAI-compatible behavior uses the established `length` versus `stop` distinction. The internal protocol will carry the equivalent typed reason rather than infer it in the UI.
4. Retain a finite maximum. LLamaSharp recommends a maximum length and/or model-specific stop condition; unbounded generation would reintroduce runaway output.

Primary references:

- IBM Granite 3.3 model usage: https://huggingface.co/ibm-granite/granite-3.3-2b-instruct
- IBM small-model prompting practices: https://github.com/ibm-granite-community/granite-agent-cookbook/blob/main/model_prompting_best_practices.md
- LLamaSharp chat example: https://github.com/SciSharp/LLamaSharp
- LLamaSharp `MaxTokens` documentation: https://scisharp.github.io/LLamaSharp/0.4/LLamaExecutors/parameters/
- llama.cpp length-finish discussion: https://github.com/ggerganov/llama.cpp/issues/8856

## Runtime completion detection

### Typed result

Introduce a terminal reason with two values:

- `Stop`: the model reached EOS, a model/template turn boundary, or an approved anti-prompt before exhausting the visible budget.
- `Length`: the model attempted to generate beyond the configured visible budget.

Cancellation and runtime failure remain the existing stopped and failed paths; they are not completion reasons.

### One-token probe

The underlying LLamaSharp inference call receives `MaximumGeneratedTokens + 1`. A termination-aware output transform counts source token iterations before text aggregation:

- Tokens 1 through `MaximumGeneratedTokens` flow through the existing Granite boundary cleanup.
- If the source ends before producing the probe token, completion is `Stop`.
- If the extra token is produced, it is suppressed and completion is `Length`.
- If the existing role/fence boundary detector terminates the stream first, completion is `Stop`.

The probe token is never emitted, persisted, copied, or rendered. The checked addition must reject an impossible integer overflow during configuration validation. This strategy avoids retokenizing decoded text, whose token boundaries may differ from the originally sampled sequence.

The completion observer must be shared by cloned output transforms because `ChatSession` may clone a configured transform.

## Protocol and data flow

The terminal reason flows through every boundary as typed data:

1. Native inference records `Stop` or `Length`.
2. The adapter emits `G1DONE stop` or `G1DONE length`.
3. The CLI parser validates the exact token and rejects unknown reasons.
4. The worker emits `ResponseCompletedEvent` with `GgufCompletionReason`.
5. Worker-client transport preserves the reason.
6. `GgufChatSessionAdapter` maps it to the chat-domain completion event.
7. `GgufChatCoordinator` stores `Completed` or `LimitReached` on the assistant message.
8. `ChatPage` renders either no terminal label or **Response limit reached** with a continuation action.

Increment the GGUF runtime protocol version from v1 to v2 because the completion event and adapter terminal frame gain required reason data. Update the packaged worker, adapter, clients, serializers, fixtures, and contract hashes atomically; mixed v1/v2 components must fail closed.

## System instruction

Each newly prepared runtime conversation starts with one system-role message before visible history:

> You are Granite Edge AI, a concise general-purpose assistant. Answer the user's question directly and accurately. Distinguish facts from uncertainty and state when you are unsure. Check numerical claims and units before stating them. Use only as much detail as needed unless the user asks for more.

The instruction is passed as `AuthorRole.System` through `PromptTemplateTransformer`. It is not displayed, copied, or written as a visible chat message. The implementation must fail safely with a clear unsupported-template error if an imported model cannot apply its embedded template to the system role; it must not silently fall back to hand-authored role markers.

## Continuation behavior

### User experience

- Only the latest assistant message with `LimitReached` displays **Continue generating**.
- The action is a normal focusable button with a unique automation name.
- Activating it replaces the limit label with the existing generating state and Stop control.
- New chat, model import, history switching, and a second continuation are gated while generation is active.
- New deltas append to the same assistant bubble. Existing content never disappears or repeats through UI replacement.
- Natural completion removes the continuation action. A second length result restores it.

### Canonical history

Continuation uses a fixed internal control turn:

> Continue from exactly where the preceding response ended. Do not repeat text already given. Complete the answer concisely.

Add an explicit internal/control role to persisted chat history. It maps to a user role only at the runtime-template boundary and is excluded from:

- transcript rendering;
- chat titles and dated-history previews;
- per-message and whole-chat copying;
- user-visible message counts.

Persisting the control turn keeps a restored runtime session semantically identical to the live session. The continuation output is appended to the existing visible assistant message rather than creating a second bubble.

## Errors and context exhaustion

- If continuation fails before producing text, retain the original response, mark it incomplete, and show a safe retry message.
- If continuation fails after producing text, retain and persist all partial text and mark it incomplete.
- Stop retains the accumulated response under the existing stopped semantics.
- A context-capacity failure must not be mislabeled as a length completion. If the runtime provides a typed context-limit failure, show **Context limit reached — start a new chat or shorten the conversation**. Otherwise use the existing safe runtime-failure copy.
- Clipboard operations remain independent of generation state. Copying a streaming partial is allowed and must not emit a stop command or mutate completion state.

## Testing

Follow red-green-refactor for each behavior.

### Native adapter

- Natural EOS before the budget reports `Stop`.
- Turn-boundary cleanup reports `Stop`.
- Production of the probe token reports `Length`.
- The probe token never appears in output.
- A cloned transform shares the terminal observer.
- Checked configuration rejects a budget that cannot accommodate the probe.
- The system role is passed through the embedded template path.

### Protocol and worker

- Both exact completion frames round-trip.
- Missing, duplicate, or unknown reason tokens fail closed.
- Worker events preserve the completion reason and sequence invariants.
- Contract hashes and package manifests are updated where required.

### Coordinator and persistence

- Length completion stores `LimitReached`.
- Continue adds one internal control turn and appends to the same assistant message.
- Internal turns survive save/load and runtime reconstruction but remain absent from visible snapshots and copy formatting.
- Repeated continuation is supported when another length boundary occurs.
- Stop, failure-before-delta, and failure-after-delta retain the correct partial response.

### WinUI

- Continue appears only for the eligible latest assistant message.
- Mouse and keyboard activation raise one continuation request.
- Copy during streaming preserves `IsGenerating` and the current transcript.
- Copy feedback and continuation controls remain independently focusable.
- Streaming synchronization updates the existing bubble in place without blanking the transcript.

### Controlled real-model verification

Add an opt-in controlled GGUF case with a deliberately tiny output budget to prove the `Length` path and continuation end-to-end. Keep factual-quality evaluation separate from deterministic unit gates; use a small evaluation set to compare the system prompt against the previous baseline rather than asserting exact generated prose.

## Non-goals

- Increasing the default context window or enabling unbounded generation.
- Claiming that a system prompt eliminates hallucinations.
- Adding online retrieval or factual verification.
- Changing TurboQuant or KV-cache selection. Existing TurboQuant capability routing remains unchanged and applicable when a supported backend is present.
- Showing internal continuation instructions to the user.

## Acceptance criteria

1. A 512-token response cannot be silently labeled as normally completed when the model attempts token 513.
2. No probe token reaches the user or stored transcript.
3. Continue appends to the same assistant response without a visible synthetic user turn.
4. Reloading a continued conversation reconstructs the same runtime turn sequence.
5. Copying during generation does not stop, cancel, or mutate generation.
6. The imported model's embedded chat template formats the system role.
7. All GGUF contract, unit, integration, packaged WinUI, and controlled-model tests pass.
