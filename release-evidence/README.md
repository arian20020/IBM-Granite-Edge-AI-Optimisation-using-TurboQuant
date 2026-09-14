# Release handover

This page connects the tested application, guides and remaining release checks. It is not a public installer download.

## Recorded verification

[Application evidence E1–E6](../docs/testing/application-verification/README.md) separates hosted tests, local checks, screenshots and automated inspection journeys.

E6 tested an existing installation. Its recorded candidate revision was `90f93a33b3648f7ab0d2aab85251317ffe08c247`. The run did not rebuild it or independently prove source-to-binary provenance. Its executable SHA-256 was:

```text
1A101404C469BA0C036C399AAE2BE647DF6B3CB834AD5E094510F4C00673A430
```

This is an executable hash, **not an installer checksum**. All three E6 inspection journeys passed with no failures or skips. Private model manifests and raw diagnostics remain outside the public evidence folder.

## Handover status

Use the [package handover record](Package-Handover.md) for the missing installer and build details. The owner must supply and verify them before this can become an installation release.

| Item | Status / next step |
| --- | --- |
| Guides | Available in [manuals](../docs/manuals/README.md). |
| Test evidence | Indexed in E1–E6; confirm these files are committed in the final submission checkout. |
| Known issues | [CI scope](../docs/testing/CI-Test-Scope.md) and [user limitations](../docs/manuals/Known-Limitations.md). |
| Final installer | No verified public download or installer checksum is established here. Supply the package, dependency/signing details and its hash together. |
| Clean install/uninstall | Not confirmed. Compilation does not prove installation. |
| Final release tag | Not assigned by this documentation update. Select after reviewing the final changes. |
| Demo | [Scenario](demo/README.md) provided; a versioned video is not recorded here. |
| Independent reproduction | Not confirmed for the application. Experiment reproduction is documented separately. |
| Report | See [source arrangement](../report/README.md). |

Use the [release checklist](Release-Checklist.md) to record evidence, not assumptions. The existing subfolder instructions are not completed test receipts.

Do not publish private local paths, prompts, model weights, credentials or signing keys. Link to safe evidence instead of copying a whole development directory.
