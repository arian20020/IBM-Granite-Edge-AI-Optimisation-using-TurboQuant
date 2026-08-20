# Licence Review Notes

**Document ID:** NOTE-LIC-001  
**Version:** 1.1
**Status:** Initial engineering review complete — final release-package review pending  
**Owner / reviewer:** Arian B  
**Review date:** 2026-08-21
**Related register:** [Licence Register](Licence-Register.md)  
**Related review:** `RV-007`

## 1. Purpose and boundary

These notes record the source-based engineering review used to update the Licence Register.

This is not legal advice. The review answers a practical project question: whether the currently identified component may be used, modified or redistributed for the stated project purpose, and what must still be checked before it is included in a release package.

A permissive source licence does not automatically approve every binary, model conversion, NuGet dependency, native runtime, dataset or asset that may be distributed with the application.

## 2. Decision rules

| Decision | Meaning in this project |
|---|---|
| Approved | The identified component and stated use have a clear licence basis. Required notices still apply. |
| Restricted | Use is allowed only under the recorded conditions; final packaging or dependency review remains. |
| Pending | The exact component, revision, licence or provenance has not yet been fixed. It must not be bundled. |
| Rejected | The proposed use is not permitted or the terms are unacceptable. |

## 3. Source review

### 3.1 IBM Granite 4.1 models

Official model cards reviewed:

- `ibm-granite/granite-4.1-3b` — <https://huggingface.co/ibm-granite/granite-4.1-3b>
- `ibm-granite/granite-4.1-8b` — <https://huggingface.co/ibm-granite/granite-4.1-8b>

Both official model cards identify the licence as Apache-2.0. This provides a clear basis for use, modification and redistribution subject to Apache-2.0 conditions.

The current project must still record:

- the exact model repository and revision;
- each exact downloaded or converted filename;
- SHA-256 hashes;
- whether the original weights, a GGUF conversion or an OpenVINO IR artefact will be distributed;
- the licence and provenance of any community conversion repository.

**Decision:** official Granite 4.1 3B/8B use and modification are permitted; final model-artifact bundling remains Restricted until exact provenance and packaging are recorded.

### 3.2 llama.cpp

Source reviewed:

- <https://github.com/ggml-org/llama.cpp/blob/master/LICENSE>

The MIT licence permits use, copying, modification, publication, distribution, sublicensing and sale, provided the copyright and permission notice is retained.

The project pins tag `b9870`, commit `2d973636e292ee6f75fadcf08d29cb33511f509f`.

**Decision:** source/build use, modification and redistribution are Approved for the pinned source. The final binary package must still include the MIT notice and any notices belonging to compiled optional dependencies.

### 3.3 OpenVINO Toolkit

Source reviewed:

- <https://github.com/openvinotoolkit/openvino/blob/master/LICENSE>

OpenVINO is Apache-2.0 licensed. The licence permits use, modification and distribution in source or object form. Redistribution requires the licence, preservation of applicable notices, prominent notices on modified files and relevant NOTICE content where supplied.

**Decision:** development and modification are permitted. Final bundling remains Restricted until the exact release, included DLLs, NOTICE and third-party notices are captured.

### 3.4 OpenVINO GenAI

Source reviewed:

- <https://github.com/openvinotoolkit/openvino.genai/blob/master/LICENSE>

OpenVINO GenAI is Apache-2.0 licensed. The same Apache redistribution duties apply.

**Decision:** development and modification are permitted. Final bundling remains Restricted until the exact GenAI, OpenVINO Runtime, tokenizer and native dependency versions are captured.

### 3.5 Microsoft Windows App SDK

Sources reviewed:

- package: <https://www.nuget.org/packages/Microsoft.WindowsAppSDK/2.2.0>
- source repository: <https://github.com/microsoft/WindowsAppSDK>

The project pins `Microsoft.WindowsAppSDK` version `2.2.0`. The public source repository uses MIT, while the actual NuGet package and its component dependencies must be reviewed as the package used by the application.

**Decision:** application development use is permitted. Final redistribution is Restricted to the files permitted by the exact package terms and requires a package/dependency notice inventory.

### 3.6 Microsoft Windows SDK Build Tools

Source reviewed:

- <https://www.nuget.org/packages/Microsoft.Windows.SDK.BuildTools>

The project pins version `10.0.28000.2270`. This is a build dependency and should not be copied into the application package merely because it was used to compile the project.

**Decision:** Restricted to build-time use unless exact package review later identifies redistributable files. The final package must be inspected to confirm the SDK tools were not bundled accidentally.

### 3.7 .NET 8

Sources reviewed:

- runtime licence: <https://github.com/dotnet/runtime/blob/main/LICENSE.TXT>
- SDK licence: <https://github.com/dotnet/sdk/blob/main/LICENSE.TXT>
- runtime third-party notices: <https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT>

The main repositories use MIT. A framework-dependent deployment and a self-contained deployment have different distributed-file inventories.

**Decision:** development use is permitted. Self-contained packaging remains Restricted until the exact runtime patch and third-party notices are captured.

### 3.8 AtomicBot TurboQuant fork

Sources reviewed:

- repository: <https://github.com/AtomicBot-ai/atomic-llama-cpp-turboquant>
- pinned branch: `feature/turboquant-kv-cache`
- pinned commit: `b0e900a28ee4172bbb97df0d1ea1c78e86bc0ac6`
- repository licence: MIT

The MIT licence permits use, modification and redistribution with notice preservation.

**Decision:** source research, build, modification and testing are permitted. Final binary packaging remains Restricted until inherited llama.cpp notices and all selected backend/native dependencies are recorded.

### 3.9 animehacker TurboQuant fork

Sources reviewed:

- repository: <https://github.com/animehacker/llama-turboquant>
- pinned branch: `main`
- pinned commit: `5bc5ed3bdc25003aa9f07422753a7b8d4f9190fc`
- repository licence: MIT

The repository describes a Stage-1/PolarQuant-style implementation and explicitly states that QJL Stage 2 is not included. This is a technical claim boundary, not a licence restriction.

**Decision:** source research, build, modification and testing are permitted. Final binary packaging remains Restricted until inherited notices and any SYCL/oneAPI/native dependencies are reviewed.

### 3.10 TurboVec

Source and controlled artefact reviewed:

- repository: <https://github.com/RyanCodrai/turbovec>;
- release: `v1.0.0`;
- commit: `ccab9f325e6ce2a270a87daf01ae4e443bcf2d49`;
- Windows x64 wheel SHA-256: `cd855e0b318a57dc57c733f9a62ae98de5192f4f6c2c760e305523e8ceb1b090`;
- upstream code licence: MIT.

The bounded review established a clear licence basis for the exact TurboVec code and wheel used by the offline command-line demonstrator. The MIT notice and copyright must be preserved. The wheel is obtained through a separate controlled acquisition and is not bundled into the WinUI application or a release package.

The embedding model and Python environment require separate decisions. `BAAI/bge-small-en-v1.5` is recorded under its own MIT model licence and identity manifest; FastEmbed, NumPy, ONNX Runtime and their dependencies remain subject to their own package terms. TurboVec's MIT licence does not approve those separate artefacts.

**Decision:** Restricted. Research, controlled acquisition and use of this exact identity are permitted for the **Command-line demonstrator only** role. No application integration or application/release redistribution is approved by this bounded review. Any bundling, dependency change or redistribution proposal requires a final packaging and notice review. See [ADR-TurboVec](../architecture/decisions/ADR-TurboVec.md) and the [controlled evidence record](../evidence/turbovec/README.md).

### 3.11 Evaluation prompts and data

Project-authored synthetic prompts and documents may be used by the project. External benchmarks, documents and datasets remain subject to their own licences or terms.

**Decision:** Restricted to project-authored or clearly licensed material with recorded provenance. Sensitive or unclear material must be excluded.

### 3.12 Icons, images and fonts

The WinUI project contains application image assets, but a complete provenance manifest has not yet been recorded.

**Decision:** Pending until each asset is identified as project-created, template-provided or clearly licensed for application distribution.

### 3.13 Papers and textbooks

Research sources may be read, analysed, cited and quoted within lawful and academic limits. Access to a publication does not permit republication of its full text.

**Decision:** Restricted to research, citation, paraphrase and limited lawful quotation. Do not place full copyrighted books or papers in the public repository or release package unless an open licence permits it.

### 3.14 Project source code licence

No root `LICENSE` file was present when this review was completed.

Without an explicit licence, external users do not automatically receive broad permission to copy, modify or redistribute the project’s own source code.

**Decision:** Pending owner decision. Select and add a project licence only after considering UCL, IBM/Intel partner, third-party code and submission requirements.

## 4. Release-package actions

Before release packaging:

1. freeze the exact Granite source repositories, revisions, filenames and hashes;
2. review any community GGUF repository separately;
3. pin OpenVINO, OpenVINO GenAI and tokenizer versions;
4. export the final NuGet and native dependency inventories;
5. identify every DLL, executable, model and asset included in the release;
6. prepare `THIRD-PARTY-NOTICES.md` or an equivalent notice bundle;
7. confirm that Windows SDK build tools are not shipped accidentally;
8. decide whether .NET is framework-dependent or self-contained;
9. complete asset and evaluation-data provenance;
10. decide the licence for the project’s own source code;
11. if TurboVec is ever proposed for application or release distribution, review the exact wheel, dependency inventory, model artefact and required notices for that package.

## 5. Current overall conclusion

The project has a sound licence basis for research, development and modification of the main open-source runtimes, reviewed TurboQuant forks and the exact TurboVec v1.0.0 command-line research dependency.

The project is **not yet approved to distribute one final bundle containing every dependency and model artefact**. That decision remains gated on the exact release inventory, notices, model provenance and project-source licence decision.
