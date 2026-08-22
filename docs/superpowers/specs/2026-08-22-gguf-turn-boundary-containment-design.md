# GGUF Turn-Boundary Containment Design

**Status:** Approved for specification on 2026-08-22  
**Scope:** Local GGUF chat generation in `GraniteEdgeAI.GgufRuntime.NativeAdapter`  
**Observed model:** IBM Granite 4.1 3B Q4_K_M, SHA-256 prefix `662b0626cd58`

## Problem

The embedded GGUF chat template is applied, but generation still uses LLamaSharp's stochastic default sampling without explicit turn anti-prompts or an output-boundary transform. Some samples therefore miss Granite's end-of-text token, begin a fabricated transcript with labels such as `Me:` and `User:`, or repeat empty Markdown fences until `MaximumGeneratedTokens` is exhausted.

The original smoke checks were insufficient because they exercised one random sample, accepted only line-leading `me`, `user`, or `assistant` labels, and recognized repeated fences only in one newline layout. The saved production response proves that the same controlled model can fail under another random sample.

## Required behavior

- Continue applying the GGUF model's embedded chat template.
- Use deterministic greedy sampling for the default local-chat profile. This matches IBM's basic Granite generation examples, which do not enable sampling.
- Remove a case-insensitive `Me:` label only when it is the first non-whitespace text in a newly generated assistant turn. Preserve the answer following the label.
- End the assistant turn before a subsequent line-leading `Me:`, `User:`, or `Assistant:` label, regardless of case. Do not expose or persist the fabricated label or anything after it.
- End the turn when a second empty Markdown fence follows an empty fence with only whitespace between them. Preserve normal fenced code containing non-whitespace content.
- Keep `MaximumGeneratedTokens` as the final safety bound.
- Apply normalization inside the LLamaSharp chat-session output boundary so the same sanitized text is streamed to the app and retained in session history.
- Do not rewrite existing chat-history JSON. Previously stored malformed messages remain visible until the user deletes that conversation.

## Architecture

`LlamaSharpInferenceEngine` will create a `GraniteTurnBoundaryTextTransform` and attach it with `ChatSession.WithOutputTransform`. The transform owns only streaming text normalization and turn containment:

1. Buffer the minimum prefix needed to decide whether the response begins with `Me:`.
2. Stream ordinary content as soon as it is known safe.
3. Retain a short rolling suffix so role labels and empty-fence sequences split across token chunks are detected.
4. Stop enumeration at the first fabricated-turn or repeated-empty-fence boundary.
5. Flush safe buffered text when the model ends normally.

Inference parameters will use `GreedySamplingPipeline` and case variants of the supported next-speaker labels as anti-prompts. Anti-prompts provide early executor termination for common boundaries; the transform remains authoritative for chunk-split detection, prefix cleanup, and fence-loop containment.

The transform must be cloneable because LLamaSharp clones stream transforms as part of session operations. Each clone starts with clean per-turn state.

## Edge cases

- `Tell me: why` and prose containing `me:` are unchanged because the prefix rule applies only at response start.
- A legitimate first code fence is emitted. A second fence is allowed when content exists between fences; only whitespace-only fence repetition terminates the turn.
- A role label inside a fenced code example is preserved unless it occurs after an already empty fence boundary. Role-boundary matching otherwise requires the start of a logical line.
- Empty or whitespace-only model output remains empty; the worker's existing completion behavior is unchanged.
- Cancellation and the user Stop action continue to flow through the existing cancellation token and worker protocol.

## Verification

Unit tests will feed deliberately fragmented chunks into the transform and prove:

- `Me: Hello` becomes `Hello`.
- mixed-case and chunk-split leading labels are removed;
- ordinary uses of `me:` remain;
- fabricated subsequent speaker turns are not emitted;
- the saved repeated-empty-fence shape terminates after the useful answer;
- legitimate fenced code remains intact;
- transform clones have independent state;
- greedy sampling and the required anti-prompts are configured.

Controlled real-model tests will use the imported Granite model and assert that complete responses contain no leading `Me:`, no fabricated next-speaker turn, and no repeated empty-fence loop. The full GGUF verification script, packaged WinUI chat suite, and self-contained Release/x64 build remain the completion gates.

## Non-goals

- Changing the GGUF format, model weights, or TurboQuant implementation.
- Adding user-configurable sampling controls in this fix.
- Removing legitimate Markdown or arbitrary text merely because it contains backticks.
- Editing or deleting the user's existing persisted conversations.
