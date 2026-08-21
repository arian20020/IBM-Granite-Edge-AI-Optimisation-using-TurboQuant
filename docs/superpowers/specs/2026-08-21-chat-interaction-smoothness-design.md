# Chat Interaction and Streaming Smoothness Design

## Goal

Polish the existing Granite Edge AI chat preview so its sidebar remains legible at short window heights, its empty composer is compact and visually balanced, keyboard submission follows established chat conventions, and streamed output updates smoothly without flashing or stealing the user's scroll position.

## Scope

This follow-up changes only WinUI presentation and controller behavior. It does not change GGUF runtime protocols, conversation persistence formats, attachment policy, model import behavior, or the truthful preview boundary.

## Sidebar behavior

The history region will use a constrained grid rather than a stack layout that measures its list without a useful height limit. The `CHATS` heading remains above an independently scrolling history list. Settings remains fixed at the bottom in a dedicated footer separated from history by a subtle one-pixel divider and at least 12 pixels of vertical breathing room.

At short window heights, history rows must scroll or clip inside the history region and must never render underneath, touch, or visually merge with Settings. The Settings command keeps the same transparent-at-rest navigation style as other rail commands; separation comes from structure and spacing rather than making it a permanently filled button.

## Compact composer

When no attachments or rejection summary are present, the composer will be approximately 52 pixels high. The current invisible row-spacing contribution will be removed: the attachment area supplies its own bottom spacing only while visible.

The prompt row and action buttons will be 40 pixels high. The borderless prompt field will use a 36-pixel minimum height with centered single-line content and will continue growing up to the existing multiline limit. The outer surface remains the only persistent border, with a proportional corner radius and the existing focus treatment.

Attachment chips and rejection feedback remain above the prompt row. Their presence may increase the composer height, but hiding them must restore the compact baseline without residual space.

## Keyboard submission

The prompt textbox will handle keyboard input directly:

- Enter with no Shift modifier sends a non-empty prompt through the same guarded submission path as the Send button and suppresses the newline.
- Shift+Enter retains normal textbox behavior and inserts a newline.
- Enter does not submit whitespace-only text or submit while generation is active.
- Clicking Send remains supported and shares one submission method with keyboard submission.

This behavior must remain accessible and must not interfere with attachment selection or Stop.

## Incremental streaming

The current full-render path clears and recreates history and transcript controls on every conversation event. This causes flashing, repeated layout, focus loss risk, and repeated scrolling during token streaming.

`ChatPage` will retain transcript controls keyed by stable `ChatMessage.Id` values. Synchronization will:

1. reuse and update an existing bubble when its content or status changes;
2. add controls only for new message IDs;
3. remove controls only when a selected conversation genuinely changes or messages disappear;
4. avoid clearing and rebuilding unchanged controls.

History will be synchronized separately and refreshed only when its visible structure changes: conversation membership, grouping, title, or selected state. Token content changes do not rebuild the rail.

Rapid conversation notifications will be coalesced in `ChatDemoController` so at most one UI synchronization is queued at a time. The queued render always reads the newest coordinator state, preventing stale output while avoiding a dispatcher entry for every token.

## Scroll behavior

Before transcript synchronization, the page determines whether the transcript viewport is already near its bottom. Streaming follows the latest output only when the user is near the bottom. If the user scrolls upward, subsequent token updates preserve that reading position. Returning to the bottom resumes following, and sending a new prompt explicitly requests following for the newly submitted turn.

This matches established chat behavior and prevents generation from fighting deliberate user scrolling.

## Data and persistence boundaries

Stable conversation and message IDs already exist and will be used as presentation keys. No new persisted fields are required. Conversation storage remains authoritative, and terminal completion, stop, incomplete, and failure states remain rendered and persisted through the existing coordinator.

## Error and lifecycle handling

Queued renders must become harmless after controller disposal. A failed dispatcher enqueue must not mutate UI state off-thread. Incremental synchronization must fall back to a deterministic full synchronization when the selected conversation changes or the rendered ID sequence is inconsistent, without exposing internal errors to the user.

## Verification

Test-first coverage will require:

- a constrained history grid and separated Settings footer;
- compact empty-composer geometry with no hidden attachment gap;
- Enter submission, Shift+Enter newline behavior, and whitespace/generation guards;
- reuse of an existing message bubble for streamed content changes;
- history stability during token-only updates;
- coalescing of repeated conversation notifications;
- bottom-aware auto-scroll that pauses after user navigation;
- preservation of existing accessibility, attachment, Stop, history, source-contract, packaged UI, and full GGUF verification gates.

## Acceptance criteria

- Settings never overlaps or visually merges with the last chat row at supported window sizes.
- The empty composer is compact and its single-line prompt is vertically centered.
- Enter sends and Shift+Enter creates a newline.
- Streaming changes the existing assistant bubble without clearing the transcript or history.
- Rapid output appears smoothly without visible flashing.
- Manual upward scrolling is respected during generation.
- All focused and full release verification gates pass, with only the documented controlled real-model test allowed to skip when no local model is configured.
