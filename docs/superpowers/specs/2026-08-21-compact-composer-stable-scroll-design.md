# Compact Composer and Stable Transcript Design

## Goal

Make the chat composer materially smaller vertically and prevent the conversation area from flashing white when a prompt is sent or a response streams.

## Scope

- Reduce the empty composer from approximately 52 pixels to approximately 44 pixels tall.
- Keep the attachment, send, and stop controls centered inside the compact surface.
- Preserve multiline growth, attachments, Enter-to-send, Shift+Enter, and Stop behavior.
- Keep existing transcript bubbles mounted while generation begins and while assistant text changes.
- Replace repeated `ListView.ScrollIntoView` calls with a coalesced bottom-scroll that runs only when following is requested and the transcript actually overflows.
- Preserve the existing rule that users who scroll away from the bottom are not pulled back during streaming.

## Design

### Composer geometry

The composer surface will use a 36-pixel single-line control row with 3 pixels of vertical surface padding and a 1-pixel border, producing an approximately 44-pixel outer height. The prompt remains vertically centered for one line and grows upward to the existing multiline maximum. Horizontal spacing will be tightened only as needed to keep the controls balanced.

### Stable transcript rendering

`ChatPage` will continue updating message bubbles in place by stable message ID. It will stop calling `ListView.ScrollIntoView` directly for every synchronized update. Instead, it will request one deferred bottom-scroll through the UI dispatcher, coalescing additional requests while one is pending. The deferred callback will use the transcript's existing `ScrollViewer` and scroll only when overflow exists.

The empty state and transcript visibility will change only when the selected conversation truly has zero messages. Sending and streaming within the same conversation will not reset or hide the transcript.

### Failure and lifecycle behavior

A rejected dispatcher enqueue clears the pending-scroll flag so a later request can retry. A callback that runs after the page is unloaded becomes harmless. No animation or opacity mask will be used to conceal layout churn.

## Testing

- A packaged UI test will require the empty composer to measure at approximately 44 pixels.
- Existing multiline, attachment, keyboard, and generating-state tests will remain green.
- Transcript tests will verify that synchronization keeps existing bubble instances and does not hide a non-empty transcript.
- Source contracts will reject direct per-update `TranscriptList.ScrollIntoView` usage.
- A focused packaged test will verify coalescing, retry, and lifecycle behavior for deferred scrolling.
- The full GGUF verification script and Release build will run before completion.

## Out of Scope

- Replacing `ListView` with another transcript control.
- Adding masking animations or crossfades.
- Changing message styling, persistence format, inference behavior, or navigation.
