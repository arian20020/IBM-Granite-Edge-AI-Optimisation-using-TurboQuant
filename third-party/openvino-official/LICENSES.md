# Official OpenVINO Windows closure licenses

The three pinned archives are redistribution inputs. Their lock files record
the archive bytes and every archive entry. Do not redistribute an extracted
closure without carrying its matching upstream notices.

| Closure | License records in the verified archive | Required disposition |
| --- | --- | --- |
| OpenVINO Runtime | `docs/licensing/LICENSE`, `docs/licensing/*third-party-programs.txt`, `runtime/3rdparty/tbb/TBB-LICENSE` | Apache-2.0 license and applicable notices travel with the redistributed closure. |
| OpenVINO GenAI | `docs/licensing/LICENSE`, `docs/licensing/LICENSE-GENAI`, `docs/licensing/*third-party-programs*.txt`, `docs/openvino_tokenizers/LICENSE`, `runtime/3rdparty/tbb/TBB-LICENSE` | Apache-2.0 license and applicable notices travel with the redistributed closure. |
| OpenVINO Tokenizers | `docs/openvino_tokenizers/LICENSE`, `docs/openvino_tokenizers/third-party-programs.txt` | Apache-2.0 license and applicable notices travel with the redistributed closure. |

The Runtime and GenAI archives also contain the nlohmann-json sample sources;
their included `LICENSE.MIT` applies when those sample sources are redistributed.
The checked archive inventories are the authority for whether a notice is in a
specific closure.
