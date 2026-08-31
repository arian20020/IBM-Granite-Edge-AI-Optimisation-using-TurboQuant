---
name: frontend-release-gate
description: Final read-only release gate for Granite WinUI frontend campaigns. Requires current-revision scope, behaviour, build, accessibility, visual and performance evidence.
---

# Frontend Release Gate

Return exactly `READY TO MERGE` or `NOT READY`.

`READY TO MERGE` requires:

- no P0 or unknown change;
- every P1/P2 change justified;
- action parity and backend fixture outcomes unchanged;
- package, project, worker and public-contract surfaces unchanged;
- restore/build and relevant tests passing;
- no runtime XAML failure;
- accessibility audit passing;
- current-revision visual matrix complete;
- no BLOCKER or HIGH mismatch;
- no material UI-thread, startup, virtualization or XAML-loading regression;
- evidence manifests agree with the final commit.

Never infer one gate from another. Build success is not visual acceptance, and screenshot similarity is not behavioural equivalence.
