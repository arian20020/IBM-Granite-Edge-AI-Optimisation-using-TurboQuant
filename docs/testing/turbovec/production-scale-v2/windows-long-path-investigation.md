# Windows long-path investigation

Campaign: `turbovec-production-scale-final-evaluation-v2`

Pinned TurboVec commit: `ccab9f325e6ce2a270a87daf01ae4e443bcf2d49`

Classification: **reproducible host/path-policy compatibility limitation**

## Reproduction

The exact upstream case was run with Python 3.12.10 and the installed TurboVec 1.0.0 candidate:

```text
turbovec-python/tests/test_persist.py::test_atomic_save_round_trips_a_long_sidecar_name
```

It was collected and executed, then failed with exit code 1. `atomic_save` called the native index write for a long temporary `.tvim` name, but the expected temporary file was absent when Python opened it for the durability step. The observable exception was `FileNotFoundError` at `_persist.py:404`.

The failure was reproduced from both the normal external source checkout and a short checkout root. The failing generated path still exceeded the conventional Windows path limit because pytest's temporary directory plus the intentionally long filename produced a path of roughly 265 characters. Therefore shortening the checkout alone does not remove the condition.

The final targeted stdout log is 2,035 bytes with SHA-256 `c57ec2cbf2631d0579fd29556c789ff1196f1bc98791452a744dbbc4cd2be6f0`. Its empty stderr log has SHA-256 `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`. Both are preserved under `C:\R4-TV1-assets`.

## Host policy

`HKLM\SYSTEM\CurrentControlSet\Control\FileSystem\LongPathsEnabled` was `0`. No registry, security, group-policy, or system setting was changed. The campaign uses short experiment-owned artifact filenames, so this upstream edge case does not invalidate retrieval measurements; it remains an explicit portability limitation and an upstream-suite failure.

## Interpretation

The evidence does not establish whether the best product fix belongs in TurboVec's native writer, its Python persistence wrapper, or documented Windows prerequisites. It does establish that the pinned candidate does not pass its own long-sidecar test under the recorded default host policy. This result must stay separate from retrieval-quality and speed claims and must not be silently converted to a pass or skip.
