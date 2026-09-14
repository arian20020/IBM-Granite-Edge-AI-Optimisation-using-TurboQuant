# Download an IBM Granite model

Start with the installed Granite app. Downloading a model does not install the app or its runtimes.

## Easiest option: download inside Granite

1. Open the app using [Run the app](Run-the-App.md).
2. On the first page, find the recommended IBM Granite 4.0 H Micro model.
3. Select **Balanced**. Check that the card shows **Q4_K_M**, about **1.94 GB**.
4. Choose **Download selected model**. Stay online until download and verification finish.
5. Wait for model inspection to open. After a successful inspection, choose **Check hardware fit** if offered.
6. Read the fit result. Continue through the available configuration and optimisation actions, then choose **Chat with this model** when offered.
7. Try: “Explain what a computer processor does in two short sentences.”

This is a first-run example, not a new recorded test result. A memory block is possible on a busy computer. Close memory-heavy apps and use the offered recovery; do not bypass the checks. Download time depends on your connection.

The app checks the downloaded size and SHA-256 against its pinned catalogue. You do not need Git, Python or another model runner for this route.

| Download preference | Model file format | Download size, approximately |
| --- | --- | --- |
| Maximum efficiency | Q2_K | 1.23 GB |
| Efficient | Q3_K_M | 1.56 GB |
| Balanced | Q4_K_M | 1.94 GB |
| High capability | Q5_K_M | 2.27 GB |
| Maximum capability | Q8_0 | 3.40 GB |

These are five alternative downloads, not five files you must obtain. Sizes are download sizes, not peak RAM or total disk requirements. Later optimisation choices are separate from this download slider.

## Manual GGUF download

Use this option if you want to keep the original download in a folder you choose.

1. Open the [pinned IBM model files](https://huggingface.co/ibm-granite/granite-4.0-h-micro-GGUF/tree/51ce07a9c9cfa971ca359d9625836bf8a4a1b61f).
2. Select **granite-4.0-h-micro-Q4_K_M.gguf** and use the file's download control. Download only this file, not the whole repository. Read the model card and licence before use or redistribution.
3. Wait until the browser finishes. Do not import a partial download or a small text pointer in place of the model.
4. In PowerShell, enter your actual file path when prompted:

```powershell
$modelFile = Read-Host 'Paste the full path to the downloaded GGUF file, without surrounding quotes'
$modelInfo = Get-Item -LiteralPath $modelFile -ErrorAction Stop
if ($modelInfo.Length -ne 1942564512) { throw 'The file size does not match the pinned model.' }
$modelHash = (Get-FileHash -LiteralPath $modelFile -Algorithm SHA256 -ErrorAction Stop).Hash
if ($modelHash -ne 'c698c78e895740f0e707eb7f8e92894f83f6d5b3f2f2f0b446dfe9635fa0063e') {
    throw 'The checksum does not match. Do not use this file as the pinned model.'
}
'Size and checksum match the pinned Q4_K_M model.'
```

5. In Granite, choose **Choose model**, select the GGUF option, and select that file.
6. Wait for the quick scan, then choose **Continue to model inspection**.

The expected size and checksum come from the [application catalogue](../../shared/GraniteEdgeAI.ModelDownload.Authority/PinnedGraniteModelCatalog.cs). Matching them identifies the file; the app must still inspect it and check hardware fit.

## OpenVINO models

The built-in download catalogue supplies GGUF files, not OpenVINO packages. A GGUF file cannot be turned into an OpenVINO package by renaming it.

For the recorded Raw 3B demonstration, obtain the complete tested OpenVINO package from the maintainer. A verified public download for that exact converted package is not documented here. Do not substitute an arbitrary model and assume it reproduces the result.

Keep the model weights, configuration and tokenizer files together. Choose **Choose model**, select the OpenVINO option, then select the package folder rather than an individual XML file. Let the quick scan and inspection decide whether it is complete and supported.

Converting a model yourself is a separate advanced workflow, not a required beginner installation step. Ask for the matching package source and file checksums before using it for a reproducibility check.

## Offline use and problems

Download the model and prepare the complete app installation before disconnecting. A local GGUF or OpenVINO import does not itself need a new model download. Offline operation still depends on having all required local runtimes.

If verification fails, keep the error message and try the same pinned download again. Do not edit checksums or rename another model to get past verification. See [troubleshooting](Known-Limitations.md#troubleshooting).
