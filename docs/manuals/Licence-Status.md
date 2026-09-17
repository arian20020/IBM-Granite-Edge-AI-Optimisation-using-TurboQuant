# Licence status

This page separates the project's own work, third-party software and model files. They do not all have one licence.

For installation commands, use [Run the app](Run-the-App.md) and [Granite-Start-Here.md](Granite-Start-Here.md). The current setup downloads Granite 4.1 3B Q4_K_M GGUF and a separately prepared raw OpenVINO ZIP; the Granite 4.0 H Micro catalogue listed below is an optional in-app alternative. Review the [IBM Granite 4.1 GGUF model page](https://huggingface.co/ibm-granite/granite-4.1-3b-GGUF) and [upstream 4.1 3B model page](https://huggingface.co/ibm-granite/granite-4.1-3b) for their terms and notices. The OneDrive OpenVINO ZIP is not presented as an official IBM-prepared download. Download links, checksums and a self-signed installer do not themselves grant redistribution rights.

## The project's own code and documentation

No root project licence is supplied in this checkout. A licence for the original work still needs the rights holder's decision. This page does not make that decision or grant extra permission.

The root-licence decision is deferred. The supplied MSc project guide, section 2.4.1 (page 8), says external/IXN projects can have project-specific ownership agreements. No such agreement has been verified here, so neither student ownership nor permission to apply Apache-2.0 is assumed. Existing third-party notices remain unchanged.

An examiner can read the public repository. GitHub's terms also allow viewing and forking through its service, but public visibility is not a general open-source licence. See [GitHub's explanation](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/licensing-a-repository). Confirm any further evaluation or redistribution permission with the owner or the applicable submission terms.

## Existing software and model records

| Component | Recorded licence | Where to check |
| --- | --- | --- |
| LLamaSharp | MIT | [Retained licence](../../third-party/licenses/LICENSE.LLamaSharp.txt). |
| llama.cpp | MIT | [Retained licence](../../third-party/licenses/LICENSE.llama.cpp.txt). Forks and added components need their own review. |
| Official OpenVINO Runtime, GenAI and Tokenizers | Apache-2.0 plus bundled third-party notices | [Archive notice inventory](../../third-party/openvino-official/LICENSES.md). |
| OpenVINO TurboQuant implementation | Apache-2.0 plus runtime/GenAI notices | [Pinned source and notice record](../../third-party/openvino-turboquant/LICENSES.md). |
| Embedded Python and converter packages | Python uses PSF-2.0; wheel dependencies have separate terms | [Converter record](../../third-party/openvino-converter/LICENSES.md) and its wheel manifest. |
| Recommended Granite 4.0 H Micro GGUF downloads | IBM's model page declares Apache-2.0 | [Pinned IBM model page](https://huggingface.co/ibm-granite/granite-4.0-h-micro-GGUF/tree/51ce07a9c9cfa971ca359d9625836bf8a4a1b61f). This is not a licence for every possible imported model. |

This is a summary of existing records, not a complete audit of a final installer. Keep the full required licence texts, copyright notices and applicable third-party notices with redistributed components. Do not replace them with this table.

The broader [licence register](../risks/Licence-Register.md) records project review items. The [experiment licence page](../testing/final-results/LICENSES.md) has separate unresolved evidence-package questions. Known upstream licences do not automatically resolve those questions.

## Before a release is distributed

Before distributing a release, the owner needs to confirm:

1. The licence for the project's own code and documentation.
2. The terms for each bundled runtime and dependency.
3. The terms for models supplied separately.

For the exact release, check that the required notices are actually present in the package, not only in this repository. Record any unresolved component before distribution. A source licence review alone is not proof that the final package meets every requirement.
