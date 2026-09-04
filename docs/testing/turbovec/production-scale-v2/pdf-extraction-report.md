# PDF extraction verification

Campaign: `turbovec-production-scale-final-evaluation-v2`

Experiment: `EXP-TV-COMP-001`

Disposition of this check: **PASS**

## What was checked

The experiment-owned PdfPig spike was tested independently of the application. The matrix covers ordinary text, an empty page followed by text, a genuine image-only PDF, a malformed PDF, an input-hash mismatch, cancellation, page/byte/text limits, a zero-page PDF, a Unicode filename and Unicode text, ordered multi-page headings/table-like text/repeated passages, a 120-page document, and an authentically encrypted PDF.

Each successful extracted page now carries its one-based page number, Unicode text, Unicode-scalar count, and SHA-256 of the UTF-8 text. Failure cases return explicit codes rather than being reported as successful extraction.

## Result

The final run used the generated Microsoft Testing Platform executable directly because `dotnet test` on this host previously returned an unsuccessful zero-discovery bridge result even though the executable discovered the tests correctly.

| Field | Result |
|---|---:|
| Discovered | 10 |
| Executed | 10 |
| Passed | 10 |
| Failed | 0 |
| Skipped | 0 |
| Runner exit code | 0 |

External append-only evidence directory: `C:\R4-TV1-assets\pdf-campaign-v2-final-20260904`

| Evidence | Bytes | SHA-256 |
|---|---:|---|
| TRX | 14,608 | `7dc03044d31429ea7b24436fcfe4aba7fed99443f98f513b46357ccc84f36186` |
| Build log | 496 | `b9f2f6689e05963b28147cbce95f2640505bedef99150cd986e40cf3f0e3a55b` |
| Test log | 420 | `870f259b483673bf10c9618366c95aea465480f0a45c68ae15deb843104d59c2` |

The encrypted fixture is a real encrypted PDF (890 bytes), not a text marker simulation. Its SHA-256 is `1d0014c0d72cab2ef97cf50623eabf7fc0b6aa06a37275c27958c5cc354050a0`. The image-only fixture contains an image XObject and no text operators.

## Boundary

This proves deterministic extraction and failure classification for the controlled fixture matrix. It does not claim OCR support, visual-layout reconstruction, semantic chunk quality, arbitrary-PDF compatibility, or production integration.
