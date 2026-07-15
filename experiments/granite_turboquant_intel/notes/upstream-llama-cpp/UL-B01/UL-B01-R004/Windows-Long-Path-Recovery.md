# UL-B01-R004 Windows Long-Path Recovery

| Field | Value |
|---|---|
| Test ID | UL-B01 |
| Run ID | UL-B01-R004 |
| Issue | One tracked source file was absent |
| Git diagnostic | Filename too long |
| Root cause | Excessively long Windows absolute path |
| Original source location | `C:\Users\Student\experiments\granite_turboquant_intel\repositories\upstream-llama.cpp-b9870-formal-R004` |
| Final source location | `C:\Users\Student\gtq-src\llama-b9870-R004` |
| Git long-path setting | `core.longpaths=true` |
| Restored file | `tools/ui/src/lib/components/app/chat/ChatAttachments/ChatAttachmentsPreview/ChatAttachmentsPreviewCurrentItem/ChatAttachmentsPreviewCurrentItemUnavailable.svelte` |
| Final tag | `b9870` |
| Final commit | `2d973636e292ee6f75fadcf08d29cb33511f509f` |
| Official remote | `https://github.com/ggml-org/llama.cpp.git` |
| Final working tree | Clean |
| Result | Passed after bounded recovery |

## Boundary

This recovery repaired only the exact source checkout. No compilation, model
download, model loading, inference, performance measurement or quality
evaluation was performed.