# Workbook 05 Route B Repair Review Checklist

This checklist is the review gate for the R7 disposition and any later bounded repair implementation.

## R7 disposition review

- [x] The exact experimental commit is `1827f6458d049de11c1a8203c793af67c99935dc`.
- [x] Source provenance and recursive submodules passed the Phase 1 artifact validator.
- [x] QJL and PolarQuant selection, write and read paths are present in source.
- [x] Source presence is not described as executable support.
- [x] `RB-SRC-001` remains visible.
- [x] The QJL `not yet supported` contradiction remains visible.
- [x] The upstream zero-test and asymmetric-test defects remain visible.
- [x] Route B is classified as `Blocked — fixable source/build exposure`.
- [x] No algorithm, OpenVINO source or application source is changed by R7.
- [ ] Project-owner approval is recorded at Checkpoint B0.

## Later repair implementation review

- [ ] Repair work starts from the exact pinned experimental commit in a separate external worktree.
- [ ] Every defect has failing reproduction evidence before the correction.
- [ ] CMake source lists use explicit accumulation and generated metadata proves full target membership.
- [ ] A non-empty f32 baseline is discovered and executed.
- [ ] The corrected asymmetric test requests and observes the intended K/V modes.
- [ ] QJL and PolarQuant test names are discovered and executed with non-zero counts.
- [ ] Requested and verified K/V codecs are recorded separately.
- [ ] Packed K and V record bytes are measured and reconciled.
- [ ] No fallback is hidden.
- [ ] No algorithm changes are mixed into the repair.
- [ ] The evidence bundle passes independent hosted validation.
- [ ] No build output, executable, source tree, model, archive or secret enters this repository.
- [ ] A fresh user decision is obtained before Route B proceeds to model testing.
