# Application test evidence

This folder supports the report's software testing appendix. It contains selected results, not every test run. It does not contain models or application builds.

## Results and screenshots

| ID | Evidence | What it shows |
| --- | --- | --- |
| E1 | [Hosted test results](results/GraniteEdgeAI.UnitTests.redacted.trx) | All 1,877 selected cases passed, with no failures, skips or aborted cases. |
| E2 | [Local GGUF checks](results/gguf-release-and-negative.redacted.trx) | Three cases passed: released Q8 and both TurboQuant choices; missing or invalid quantizer blocks persistent conversion; runtime-only choices remain available without that quantizer. |
| E3 | [Test scope and remaining issues](../CI-Test-Scope.md) | Which checks were excluded or deferred, and which issues remain. |
| E4 | [OpenVINO failure](screenshots/openvino-failure.png) and [later configuration screen](screenshots/restored-openvino-configure.png) | The earlier runtime failure and a later screen showing Balanced INT4/U4 with Start optimisation available. This does not prove a full optimisation and chat journey or correct slider behaviour. |
| E5 | [Hardware failure](screenshots/hardware-failure.png) and [later GGUF result](screenshots/restored-gguf-compatibility.png) | The earlier tool-verification failure and a later memory restriction. This does not independently check the memory estimate. |
| E6 | [Live inspection journeys](results/inspection-routes.redacted.trx) | Three automated journeys passed, with no failures or skips: GGUF inspection through compatibility, OpenVINO inspection through enabled configuration, and recovery from an invalid GGUF to a valid import and compatibility result. |

E1 is from [GitHub Actions run 34804762103](https://github.com/arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant/actions/runs/34804762103), on 14 September 2026, at commit `73684f838a1dc6cf08cb2633f8c7b87379388564`.

E2 started on 14 September 2026 at 04:37:53 +01:00. Its result file does not establish the exact source revision. Do not add E1 and E2 into one total: their selected checks can overlap.

E4 and E5 are from later interactive checks of the restored local package. They are separate from E1. Complete package and model hashes were not recorded with the screenshots. A final-screen image does not prove every step that led to it.

The developer also reported completing inspection, optimisation, export and chat with Wi-Fi disconnected. No separate recording or complete model and build details were supplied for that observation. It is not an automated end-to-end result.

E6 ran from 07:16:29 to 07:19:06 UTC on 14 September 2026. It used public UI Automation against the existing installed app, not mocked inspection results. The inputs were Granite 4.1 3B Q4_K_M GGUF, Granite 4.1 3B Raw OpenVINO, and the invalid-magic GGUF fixture. Private input manifests recorded file hashes and sizes. The test also checked that the launched executable path matched its candidate manifest. Its SHA-256 was `1A101404C469BA0C036C399AAE2BE647DF6B3CB834AD5E094510F4C00673A430`. The manifest labelled the candidate with revision `90f93a33b3648f7ab0d2aab85251317ffe08c247`; this run did not rebuild the app or independently prove its source-to-binary provenance. All 61 recorded top-level installed application files were unchanged after testing, and no tracked application source was changed.

E6 covers inspection journeys only, not optimisation execution, export, chat or all slider choices. Earlier missing-input and helper-failure attempts are not counted as passes. Screen captures from this run were not retained here because other windows could cover the app. Do not describe E6 as screenshot-based visual testing.

## Privacy and unchanged results

The `.redacted.trx` files are edited copies, not the original logs. User and computer names, run labels, and path fields were replaced. Output logs and file-attachment references were removed because they may contain local details. Example paths inside test names remain where they identify the tested input.

Test IDs, test names, outcomes, start and end times, durations and summary counts were checked against the originals and kept unchanged. Removing logs means these copies do not contain all diagnostic detail. The original files remain outside this folder. The screenshots were reviewed and copied without changes.

Original SHA-256 values:

- E1: `83ED044C7B268963AFAF3E3462BF5D2EEBBC48CB3E72597CAA523C5FEAE6C327`
- E2: `60221A22EA393FA5B4F93D78101543EBBB053125DB95FC1662B0639085D56209`
- E6: `4DFD8104A0A0D1DC2CA67722E256B60014F9B7D12CFA4F2D77ABEDF6DC4C4CB8`

[SHA256SUMS.txt](SHA256SUMS.txt) lists the hashes of the seven supplied evidence files. These differ from the original TRX hashes because the copies were redacted.

## Report references

Use `docs/testing/application-verification` as the evidence folder in the appendix. E1 and E2 now use the filenames ending in `.redacted.trx` under `results/`. The four images are under `screenshots/`. E3 remains at `docs/testing/CI-Test-Scope.md`; it is linked here rather than copied.

Use this wording in the appendix:

> The supporting folder `docs/testing/application-verification` contains redacted test results, screenshots and SHA-256 checksums. Personal and machine details were removed from the result copies. Test names, timings and outcomes were kept unchanged. The screenshots are unchanged.

These files support the recorded checks only. They do not establish full requirement coverage, a complete final-package end-to-end pass, or resolution of the issues listed in E3.
