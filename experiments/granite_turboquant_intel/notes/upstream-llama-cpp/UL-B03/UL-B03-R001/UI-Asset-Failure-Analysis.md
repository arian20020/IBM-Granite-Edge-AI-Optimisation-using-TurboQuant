# UL-B03-R001 UI Asset Failure

| Field | Value |
|---|---|
| Failure ID | FAIL-UL-B03-R001-UI-ASSET |
| Test ID | UL-B03 |
| Run ID | UL-B03-R001 |
| Stage | Release compilation |
| Result | Failed |
| Product inference executed | No |
| Root cause | Prebuilt UI provisioning remained enabled |
| Missing asset | `loading.html` |
| Retest configuration | `UL-B02-R002` |
| Retest build | `UL-B03-R002` |

## Explanation

The configuration disabled `LLAMA_BUILD_UI` but did not disable
`LLAMA_USE_PREBUILT_UI`. The pinned llama.cpp build therefore attempted to
download and embed prebuilt server UI assets. The downloaded asset set lacked
the required `loading.html` file, so `llama-ui-embed` returned exit code 1.

The CPU runtime itself was not tested and no model was loaded. The corrected
configuration disables both local UI construction and prebuilt UI provisioning
while keeping `llama-server` enabled.