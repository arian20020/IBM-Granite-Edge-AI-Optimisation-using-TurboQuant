# GGUF Chat Template and Copy Design

**Status:** Approved on 2026-08-22

## Problem

Production chat currently constructs an LLamaSharp `ChatSession` without using
the imported GGUF model's embedded chat template. With Granite, a normal answer
can therefore continue as a synthetic multi-party transcript containing labels
such as `me:`, `user:`, and `assistant:`, followed by repeated Markdown fences.
This is a prompt-boundary defect rather than useful model output and must be
fixed at prompt construction instead of hidden by post-processing.

Chat messages are also rendered by non-selectable text controls and offer no
copy action. Users need native text selection, a ChatGPT/Codex-style copy action
for an individual message, and a way to copy the complete current conversation.

## Runtime formatting

The native LLamaSharp adapter will configure each production `ChatSession` with
LLamaSharp's `PromptTemplateTransformer`, backed by the already loaded model
weights. This activates the GGUF's embedded `tokenizer.chat_template` and
preserves the existing role-aware system, user, and assistant history.

Generation will rely on the model template and tokenizer's end-of-turn/end-of-
sequence behavior. The application will not hard-code Granite-only control
tokens and will not strip transcript-looking text after generation. Those
alternatives would either break other compatible imported GGUF models or risk
removing legitimate answer content.

Existing cancellation, streaming, inference limits, runtime trust validation,
and single-flight behavior remain unchanged. Previously persisted malformed
responses are historical content and will not be rewritten; validation is
performed in a new chat.

## Selectable messages and per-message copy

Both user and Granite message bodies will use native WinUI text selection, so a
user can select any substring and use ordinary Windows copy behavior.

Each non-empty message will have a small copy action aligned beneath its bubble.
The action is visually quiet and appears on pointer hover or keyboard focus,
while remaining in the keyboard tab order. Its accessible name is **Copy
message**. Activation copies the exact visible message text, including the
currently visible partial text if generation is still streaming. It excludes
the speaker label, generation status, timestamp, identifier, and other hidden
metadata.

Successful copying briefly changes the action to a checkmark and exposes
**Copied** feedback. Clipboard errors are reported through concise,
non-blocking, privacy-safe UI feedback and do not affect chat state.

## Whole-conversation copy

A subtle **Copy chat** action will be placed at the upper-right of the current
chat header. It is disabled when the current conversation contains no non-empty
messages and copies only the open conversation, never other dated history
entries.

The clipboard payload is deterministic plain text in chronological order. Each
message is separated by one blank line and formatted as:

```text
You:
Hello

Granite Edge AI:
Hi! How can I help?
```

Only non-empty visible message content is included. Transient generation
statuses, timestamps, internal identifiers, and hidden metadata are omitted. If
the active assistant response is streaming, the payload includes its visible
partial text at the instant the command is activated.

## UI state and rendering

Copy feedback is transient presentation state and is not persisted in dated
chat history. Transcript synchronization will retain existing message controls
and update only the active message's content while streaming. It must not
rebuild or blank the whole transcript, so selection, hover/focus state, scroll
position, and smooth token rendering remain stable.

A small clipboard boundary will isolate the Windows clipboard API from view
logic. A pure transcript formatter will own whole-chat role labels, ordering,
blank-line separation, and filtering. This keeps both behaviors deterministic
and directly unit-testable.

## Accessibility and interaction

- Copy actions support pointer activation, keyboard focus, Enter/Space, tooltips,
  and accessible names.
- Hover-only styling will not make the actions unreachable to keyboard users.
- Native selection and clipboard shortcuts remain available inside message
  text.
- Feedback does not steal focus or trigger transcript scrolling.

## Verification

Tests must first reproduce the current defects, then prove:

1. The native session activates the embedded GGUF prompt template while keeping
   role-aware history.
2. A controlled Granite real-model turn ends as one assistant response and does
   not continue into fabricated `me:`, `user:`, or `assistant:` turns or repeated
   empty fences.
3. Cancellation, token streaming, and multi-turn history still pass their
   existing suites.
4. User and assistant message text is selectable.
5. Per-message copy writes exactly the currently visible message text and gives
   success/failure feedback.
6. Whole-chat copy produces the approved role-labelled plain-text format in
   chronological order, filters empty/transient content, and includes a visible
   streaming partial.
7. Copy commands handle empty chats and clipboard failures without crashing or
   changing persisted history.
8. Streaming synchronization updates the active message without rebuilding or
   blanking the transcript.

The full Release/x64 build, packaged UI route suite, GGUF verification suite,
and controlled local Granite smoke test must pass before completion is claimed.

## Scope boundary

This change fixes model-native chat formatting and adds copying for the current
conversation. It does not add Markdown rendering, rich clipboard formats,
message editing, exporting files, copying all saved conversations, rewriting
old malformed content, retrieval-augmented generation, TurboVec retrieval, or
new TurboQuant inference support. Existing TurboQuant/runtime compatibility
gates remain authoritative.
