# Chat Modern Visual Polish Design

## Status

Approved design direction: **Calm Fluent**.

This iteration refines the existing WinUI chat preview. It does not change chat persistence, GGUF prompting, attachment indexing behavior, or runtime architecture.

## Goals

- Make the composer feel like one smooth, modern control instead of a bordered control nested inside another bordered control.
- Keep one-line prompt text vertically centered while preserving correct multiline growth.
- Make every transcript message clearly readable in the light theme.
- Make sidebar actions and chat-history entries feel like calm navigation rows rather than a stack of conventional buttons.
- Replace the attachment menu with a polished, accessible **Add files** flyout.
- Show the full Granite Edge AI lockup in the sidebar and retain the monogram in the empty conversation state.

## Visual Direction

The interface will use a restrained Fluent visual language: quiet neutral surfaces, consistent rounded corners, subtle hover and pressed states, visible keyboard focus, and blue reserved for selection, focus, and the send action. Gradients and strong outlines will not be used for ordinary navigation actions.

### Composer

The outer composer is the only visible container. It remains a rounded capsule with a cool neutral one-pixel border and a restrained shadow separating it from the conversation surface.

The prompt editor inside it will use a dedicated WinUI style/template with:

- transparent fill in normal, pointer-over, focused, and disabled visual states;
- no inner border, underline, or focus rectangle;
- dark body text and a muted placeholder;
- no extra vertical padding from the stock `TextBox` template;
- a minimum single-line height that centers text optically and geometrically;
- top alignment only after the content actually wraps or contains a newline;
- the existing bounded multiline height and scrolling behavior.

Keyboard focus will be communicated by the outer composer border changing to the brand-blue focus color. This avoids a second rectangle while preserving a clear focus indication.

The attachment and send controls remain compact square buttons within the capsule. They share the same corner geometry and alignment. The attachment control is a quiet neutral icon button; the send control is the primary blue action.

### Transcript

Dark navy message surfaces will be removed from the light theme.

- Assistant messages use a near-white cool-gray surface with dark neutral text.
- User messages use a pale brand-blue surface with dark navy text.
- Both use soft corners, comfortable internal padding, and no heavy outline.
- Secondary and status text remains muted but must still be readable against its surface.
- Bubble width and left/right alignment remain unchanged unless a small spacing correction is required for consistency.

The empty state continues to show the Granite Edge AI monogram and its start-conversation message.

### Sidebar Actions and History

`New Chat`, `Import Model`, `Settings`, and chat-history entries will use one navigation-row language:

- transparent in the resting state;
- a subtle cool-blue tint on pointer hover;
- a slightly stronger tint when pressed;
- a pale-blue selected state with a narrow blue selection indicator;
- no persistent card outline for unselected rows;
- consistent row height, corner radius, horizontal inset, and text alignment.

`New Chat` remains visually prominent through placement, a leading plus icon, and semibold text—not through a filled gradient. `Import Model` and `Settings` use the same calm row treatment. Chat-history date headings stay aligned to the `CHATS` heading margin, and history text uses a higher-contrast neutral color than the current presentation.

All interactive rows remain keyboard-focusable and expose visible focus visuals.

### Add Files Flyout

The plus control will open a small custom `Flyout`, because the content has richer presentation than a plain command menu. The flyout will:

- use the visible action name **Add files** everywhere, including accessibility metadata;
- contain one clear file-import row with an attachment/file icon and the `Add files` label;
- show the concise supporting line `Text or Markdown · Not indexed` so the existing metadata-only boundary remains honest;
- use a rounded light surface, restrained shadow, comfortable padding, and subtle hover/pressed states;
- open above the composer when space allows and dismiss through standard WinUI behavior;
- preserve the existing file-picker command and metadata-only attachment flow.

The old phrase `Add knowledge files` will be removed from visible and accessibility strings.

### Branding

The top-left sidebar will visibly render the complete artwork from `docs/Logo/granite-edge-ai-lockup.svg`, including the **Granite Edge AI** wordmark. The empty-state center will continue to use `docs/Logo/granite-edge-ai-icon.svg`.

The current lockup contains SVG text that is not reliably rendered by the WinUI image pipeline. Implementation will produce a WinUI-safe packaged derivative from the repository source artwork, with the wordmark converted to vector outlines while preserving the supplied logo's appearance. It will not invent a replacement mark or alter the source-of-truth logo design.

## Component Boundaries

- `ChatComposer` owns composer layout, prompt visual states, focus presentation, multiline alignment, and the Add files flyout.
- `ChatPage` owns transcript bubble resources, sidebar navigation/history styling, and branding placement.
- Shared theme resources may hold repeated colors, corner radii, and state brushes when doing so reduces duplicated XAML without widening the scope.
- Existing view-model commands, history storage, prompt submission, and attachment metadata models remain unchanged.

## Interaction and Accessibility

- Enter/Shift+Enter behavior, streaming/stop behavior, and attachment selection remain unchanged.
- Pointer, keyboard, and screen-reader users receive the same available actions.
- Hover is supplementary; every action has an understandable resting label or icon with an accessible name.
- Focus indication remains clearly visible even though resting borders are reduced.
- Light-theme text and meaningful controls target WCAG AA contrast.
- Motion is limited to native state transitions; no decorative animation is required.

## Error Handling

File-picker cancellation remains silent. Existing attachment validation and error presentation remain authoritative. A failure to load the lockup must not prevent the page from loading; the packaged asset and a UI contract test are the primary safeguards.

## Testing and Acceptance

Automated coverage will verify:

- the prompt editor has no visible inner fill/border across its main states;
- one-line prompt text is vertically centered, and multiline content switches to top alignment and returns correctly when shortened;
- the outer composer exposes the focused visual state;
- attachment UI and accessibility text say `Add files`, and the flyout invokes the existing picker command;
- assistant and user bubble resources use light surfaces with readable dark text;
- sidebar rows are transparent at rest and have explicit hover, pressed, selected, and focus states;
- the full lockup and center monogram are packaged from the intended repository assets;
- existing history, attachment, send, stop, and GGUF tests continue to pass.

Packaged WinUI verification will include the rendered composer alignment, opening and dismissing the flyout, readable transcript bubbles, visible sidebar hover/selection/focus states, and the full sidebar wordmark. A manual preview check will compare the result with the approved screenshots and Calm Fluent direction.

## Out of Scope

- Adding RAG or indexing attached files.
- Changing supported file types or attachment persistence.
- Changing chat history semantics or storage.
- Changing GGUF or TurboVec runtime behavior.
- Adding dark-theme support in this polish pass.
- Redesigning unrelated model-import or settings screens.

## Completion Criteria

The work is complete when the light-theme preview shows a visibly complete sidebar lockup, readable pale transcript bubbles, ghost-style navigation rows, a single-surface composer with correctly centered text, and a polished Add files flyout; focused UI tests and the relevant GGUF verification pass without regressions.
