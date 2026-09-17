# OpenVINO startup diagnostic

This separate diagnostic uses the existing protected startup implementation and
the pinned official runtime manifest for Granite 1.0.3.0. It does not change the
installed application, load a model, install certificates or relax verification.
It creates and cleans up the same private temporary environment as worker startup.

On the laptop where OpenVINO inspection fails:

1. Extract the complete `Granite-OpenVINO-Diagnostic.zip` into a new folder.
2. Open ordinary Windows PowerShell as the account that runs Granite.
3. Run the following, replacing the path with the extracted folder:

```powershell
Set-Location 'C:\Users\YOUR-NAME\Downloads\Granite-OpenVINO-Diagnostic'
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File .\Run-Probe.ps1
```

If Windows or an organisation policy blocks the tool, record the message; do not
disable protection or change WindowsApps permissions. The unsigned diagnostic
does not need administrator rights, but its execution can be subject to policy.

Send the generated `OpenVino-startup-*.txt` file to Arian. It may contain local
paths and Windows exception details, but no prompts or model contents.

## Interpretation

`HELLO VERIFIED` and `CLEANUP VERIFIED` mean protected startup worked in this
standalone host. They do not prove in-app inspection works: package identity and
process context differ. A failure identifies a narrower check to investigate;
first-chance exceptions can also be caught normally and are not automatically
the cause. Exit 0 indicates this probe passed; exit 1 indicates failure; exit 2
indicates missing/incorrect arguments.

The executable includes its .NET runtime. Do not copy its DLLs into the app or
runtime folder. This is diagnostic evidence collection, not a replacement app.
