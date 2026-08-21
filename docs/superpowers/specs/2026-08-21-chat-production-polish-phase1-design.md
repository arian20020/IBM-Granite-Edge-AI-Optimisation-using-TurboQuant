# Chat Production Polish Phase 1 Design

## Goal

Move the existing Granite Edge AI chat preview from a polished prototype toward a coherent Windows desktop product without presenting preview behavior as a real loaded-model session.

This phase incorporates the approved compact-blue consistency pass and the actionable P0/P1 presentation findings from the supplied UX review. Larger runtime-truth, conversation-management, and rich-output features remain separate work.

## Product-truth boundary

The preview must never invent a model, runtime, device, quantization, readiness state, token rate, memory value, or context limit.

Until the production runtime supplies verified session metadata, the header reads:

> Preview mode · No model loaded

The deterministic response uses user-facing preview language. It may explain that the screen demonstrates chat interaction, but it must not claim that a production model generated the answer. Real model/runtime/device/configuration details will be introduced only through a later typed runtime-status contract.

## Approved visual direction

The selected direction is **B — Restrained desktop panel**.

### Application shell

- The expanded history rail is 256 effective pixels wide.
- The existing narrow breakpoint continues to collapse the rail rather than squeezing the transcript.
- The conversation region retains a lightweight panel for continuity with the approved design, but uses a 14-pixel corner radius, a one-pixel semantic border, tighter padding, and minimal elevation.
- Granite blue is the only primary accent. Neutral surfaces and semantic foreground brushes provide the remaining hierarchy.
- The page keeps explicit light-theme behavior for this preview, while every changed color remains a semantic resource with a valid dark-theme counterpart.

### Rail and history

- New Chat, Import Model, Settings, and conversation rows share one leading icon/text grid and aligned content margin.
- New Chat remains visually quiet at rest and gains its primary/tinted treatment through pointer, pressed, keyboard-focus, and selected states rather than appearing as a permanently filled call-to-action.
- Import Model and Settings use the same navigation-row geometry.
- A selected conversation uses Granite primary blue (`#2563EB`) with white text.
- The selected row does not also show a redundant accent bar.
- Date headings use an accessible semantic secondary foreground; they must not use the faint `#CBD5E1`-on-white treatment identified in the review.
- Existing deterministic conversation titles, ellipsis behavior, dated grouping, and persistence remain unchanged in this phase.

### Transcript

- User messages use Granite primary blue with white text.
- Assistant messages use a pale blue surface with dark-blue text.
- Transcript containers provide at least 12 pixels of vertical separation at every supported width.
- Assistant turns show the identity label `Granite Edge AI` above their message content. User turns do not add a redundant `You` label.
- The label is presentation-only and does not alter persisted conversation records or message transport contracts.
- Bubble widths continue to wrap within the available transcript width and preserve left/right role alignment.

### Composer

- The empty single-line composer uses a 44-pixel prompt row with six pixels of outer vertical padding.
- Attachment and prompt controls are 40 pixels tall; Send and Stop remain 44 pixels tall.
- Placeholder and entered single-line text are vertically centered. Multiline text aligns to the top after the input grows.
- The prompt TextBox remains visually transparent. The outer composer surface owns the border, background, hover/focus outline, and corner radius.
- The attachment command uses a Fluent attachment glyph, the accessible name and tooltip `Attach files`, and the existing drop-up flyout.
- The flyout action remains `Add files` and retains the supported file-type/status explanation.
- Send uses the Fluent Send glyph and the Granite primary button style.
- Send is disabled whenever the trimmed prompt is empty or generation is active.
- Stop occupies the same action position and uses the same Granite primary treatment while generation is active.

## Branding and Windows identity

- The packaged display name, application visual-element display name, description, and runtime window title are `Granite Edge AI`.
- The full source-faithful lockup remains at the top of the history rail.
- The compact Granite symbol is used for Windows package/title/task-switching assets; the full wordmark is not squeezed into a square icon.
- Existing approved source SVG geometry remains authoritative. Any generated derivative or raster asset must be reproducible and covered by a fidelity/source-hash contract.
- Standard Windows minimize, maximize, and close controls remain native; this phase does not recreate window chrome.

## Component boundaries and state flow

### `ChatComposer`

`ChatComposer` owns prompt-entry presentation and submission availability. A single state-refresh method computes Send availability from `IsGenerating` and trimmed prompt text. It runs after initialization, text changes, prompt clearing, and generation-state changes.

The existing attachment picker, validation policy, rejection summary, and file limits remain unchanged. Picker cancellation stays silent; picker failure keeps its safe inline error.

### `ChatMessageBubble`

`ChatMessageBubble` owns role-specific surfaces, alignment, foregrounds, and the assistant identity label. It derives label visibility from the existing `IsUser` property, avoiding a storage/schema change.

### `ChatPage`

`ChatPage` owns rail width, shell geometry, transcript spacing, and the session header. The preview controller supplies honest preview text. No runtime detail is displayed unless that value comes from a real runtime contract.

### App identity

The window and manifest own the product name. Package assets own square Windows icon variants. Branding changes do not alter onboarding or navigation behavior.

## Accessibility and adaptive behavior

- Normal-sized text targets at least 4.5:1 contrast in the light preview theme.
- Selected history text is white on Granite blue.
- Date headings use an accessible secondary foreground.
- Keyboard focus remains visible and distinct from hover/pressed/selected states.
- Attachment, Send, and Stop retain explicit automation names; attachment also receives a tooltip.
- Disabled Send remains discoverable through its automation name and native disabled state.
- Pointer targets remain at least 40 pixels; primary actions remain 44 pixels.
- The rail collapses before message content becomes cramped. Transcript item spacing remains at narrow widths.

## Deferred work

The following review findings are intentionally out of scope for this phase because they require new product or runtime contracts rather than presentation-only polish:

- real model/runtime/device/configuration/dependability status;
- loading, failed, context-full, and recovery state models;
- token, memory, and timing metrics;
- copy, regenerate, retry, and message action rows;
- rename, delete, clear, export, and search for conversations;
- transient-unsent conversation persistence changes;
- markdown, code-block, table, citation, and rich-output rendering;
- compact icon-only navigation and a pane toggle;
- a custom integrated title bar.

These items should be designed and tested as later phases rather than represented with placeholder controls or fabricated values.

## Verification

Implementation follows test-driven development.

- Source visual contracts assert shell dimensions, semantic-resource usage, rail alignment, assistant identity, composer geometry, glyphs, accessible names, manifest branding, and honest preview copy.
- Packaged WinUI tests assert selected/history contrast, navigation states, transcript separation, assistant-label visibility, prompt vertical centering, trimmed-empty Send disabling, generation-state transitions, and attachment tooltip/flyout behavior.
- Branding tests verify generated assets remain traceable to the approved source SVG and package entries resolve to real files.
- Existing persistence, streaming, Stop, attachments, responsive behavior, keyboard, and focus tests remain green.
- Completion requires the focused packaged UI suite, the full GGUF verification script, a clean Release build, `git diff --check`, and a clean branch worktree.

## Acceptance criteria

The phase is complete when:

1. The restrained panel and 256-pixel rail match approved option B.
2. Navigation, history, messages, Stop, and Send use one coherent Granite-blue system.
3. Selected chats and date headings are readable.
4. Narrow transcript turns retain visible separation.
5. The compact composer centers one-line text and disables empty submissions.
6. Attachment and Send commands communicate their real actions through Fluent glyphs, tooltips, and automation names.
7. The app and Windows package identify themselves as Granite Edge AI with the approved compact icon.
8. The preview header and response remain truthful about the absence of a loaded production model.
9. No deferred runtime or conversation feature is simulated.
10. All required verification gates pass.
