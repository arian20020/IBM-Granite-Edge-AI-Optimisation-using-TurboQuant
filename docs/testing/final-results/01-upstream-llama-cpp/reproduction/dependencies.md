# Dependencies

- Portable interpreter: `.tools/python311-portable/python.exe` (validated with Python 3.11.9).
- Pinned reporting packages: `scripts/testing/requirements.txt`.
- Owned Word exporter: `scripts/testing/Export-Final-Results-Pdf.ps1` with a 180-second bound.
- Normalizer/finalizer: `scripts/testing/reporting/llama_adapter.py`.
- Focused validation: `scripts/testing/tests/test_final_results_upstream_llama.py`.
- Microsoft Word is required only for the DOCX-to-PDF export step.
