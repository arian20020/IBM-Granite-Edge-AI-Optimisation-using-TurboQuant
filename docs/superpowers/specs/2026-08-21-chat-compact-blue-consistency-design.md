# Chat Compact Blue Consistency Design

## Goal

Refine the existing Granite Edge AI chat preview so selected navigation, message roles, sidebar alignment, responsive transcript spacing, and the composer all use one compact Granite-blue visual language.

## Scope

This pass changes presentation only. Chat persistence, model selection, streaming, stopping, attachment selection, privacy behavior, and command-line/runtime behavior remain unchanged.

## Approved Visual Treatment

### Selected history

- A selected conversation uses the established Granite primary blue (`#2563EB`) as its rounded row surface.
- Its title uses white text for readable contrast.
- The selection state remains visually distinct from hover and pressed states.
- Unselected history rows retain the quiet transparent/hover treatment.

### Sidebar actions

- New Chat and Import Model retain their leading icons and ghost-navigation interaction.
- Their content is left-aligned instead of centered.
- Their content begins near the same rail margin as the `CHATS` heading, using compact internal padding rather than the current centered layout.
- Settings follows the same alignment system.

### Message roles

- User messages use the primary Granite blue with white text.
- Assistant messages use a pale blue surface with dark-blue text.
- Explicit black message/action surfaces are removed from the chat view; the Stop action uses the existing Granite primary button palette.
- Both roles remain visually distinct and meet readable foreground/background contrast.

### Transcript spacing and responsive behavior

- Each transcript item receives 12 pixels of bottom separation.
- Spacing remains present at narrow conversation widths so adjacent user and assistant turns never touch.
- Bubbles continue wrapping within the available transcript width and preserve left/right role alignment.

### Compact composer

- The empty single-line composer is reduced to a compact, text-fitted height.
- Outer vertical padding changes from 10 to 6 pixels.
- The prompt row minimum height changes from 52 to 44 pixels.
- The prompt and attachment controls use compact 40-pixel heights while Send/Stop remain accessible at 44 pixels.
- Placeholder and entered text remain vertically centered for a single line.
- Multiline input and attachment chips continue to grow the composer vertically when needed.

## Accessibility

- Selected history uses white text over primary blue.
- Keyboard focus visuals remain independent from selected, hover, and pressed states.
- Existing automation names and tab order remain unchanged.
- Control targets remain at least 40 pixels, with primary Send/Stop actions remaining 44 pixels.

## Verification

- Theme contracts assert the selected, user, assistant, and Stop surfaces use the approved blue palette.
- Packaged WinUI tests assert selected history foreground/background, left-aligned sidebar actions, transcript item spacing, compact composer dimensions, and single-line vertical centering.
- Existing multiline, attachment, focus, accessibility, persistence, and responsive tests remain green.
- The full GGUF verification script and packaged GGUF UI suite run before completion.
