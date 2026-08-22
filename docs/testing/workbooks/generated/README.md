# Generated controlled workbooks

Generate all thirteen controlled Word workbooks from canonical Markdown with:

```powershell
python .\scripts\testing\Generate-Controlled-Workbooks.py
```

Generate only the repository-safe post-C set, WB-07 through WB-13, with:

```powershell
python .\scripts\testing\Generate-Controlled-Workbooks.py --post-c-only
```

The generator normalises DOCX member order, ZIP timestamps and document metadata. The post-C gate generates the seven new documents twice and requires byte-identical SHA-256 results. These DOCX files are editable working/reporting artefacts; the Markdown templates, CSV controls and JSON pack manifest remain the Git-reviewable sources of truth.
