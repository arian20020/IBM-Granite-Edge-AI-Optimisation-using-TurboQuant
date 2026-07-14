# TurboQuant implementation landscape

> **Document status:** Detailed curated research
> **Version:** 3.0
> **Last updated:** 14 July 2026

## Overview

The TurboQuant paper explains an algorithm, but a paper is not automatically a production-ready llama.cpp or OpenVINO implementation. The project therefore reviewed a group of public repositories to find out what had actually been implemented, which parts were demonstrations, which routes were CUDA-focused, and which ideas could be reused for a Windows and Intel implementation.

## What must be checked in every repository

1. **Upstream base:** Which llama.cpp revision or other framework was forked?
2. **Algorithm coverage:** Does the repository implement the MSE stage, the QJL residual stage, both stages, or only use the TurboQuant name?
3. **Compression target:** Are weights, keys, values or all of them compressed?
4. **Bit widths:** Which low-bit formats are implemented?
5. **Execution device:** Is the code tied to CUDA, or is there a CPU path that can be ported?
6. **Model support:** Which architectures were actually run?
7. **Build evidence:** Are there reproducible instructions, pinned dependencies and tests?
8. **Quality evidence:** Are perplexity, task scores or prompt outputs compared with a baseline?
9. **Performance evidence:** Are memory, latency and throughput measured correctly?
10. **Licence and maintainability:** Can code legally and safely be adapted?

## Reviewed repositories

The detailed reviews are stored in [`../06-repository-reviews`](../06-repository-reviews/README.md):

- [AmesianX/TurboQuant](../06-repository-reviews/amesianx-turboquant.md)
- [animehacker](../06-repository-reviews/animehacker.md)
- [AtomicBot AI](../06-repository-reviews/atomicbot-ai.md)
- [atomicmilkshake](../06-repository-reviews/atomicmilkshake.md)
- [BeeLlama](../06-repository-reviews/beellama.md)
- [spiritbuun](../06-repository-reviews/spiritbuun.md)
- [thepradip](../06-repository-reviews/thepradip.md)
- [TheTom](../06-repository-reviews/thetom.md)
- [TiredOfEverything](../06-repository-reviews/tiredofeverything.md)
- [unixsysdev](../06-repository-reviews/unixsysdev.md)

## How to use the reviews

The reviews are not a ranking based only on README claims. They are an engineering screening layer. A repository should move into testing only when its code path, build requirements and claimed algorithm can be identified. The common test sequence in [`../07-evaluation-and-decisions/01-common-test-sequence.md`](../07-evaluation-and-decisions/01-common-test-sequence.md) must then be applied.

## Main conclusion

No repository should be merged directly into the application. First reproduce it in an isolated experiment folder, compare it with a pinned upstream baseline, identify every changed file, and test the exact model/hardware route. The useful result may be an algorithm, kernel, test harness or code pattern rather than the whole fork.

## Sources used

- `SRC-PAPER-TURBOQUANT-2025`
- `SRC-PAPER-QJL-2024`
- `SRC-REPO-AMESIANX`
- `SRC-REPO-ANIMEHACKER`
- `SRC-REPO-ATOMICBOT`
- `SRC-REPO-ATOMICMILKSHAKE`
- `SRC-REPO-BEELLAMA`
- `SRC-REPO-SPIRITBUUN`
- `SRC-REPO-THEPRADIP`
- `SRC-REPO-THETOM`
- `SRC-REPO-TIREDOFEVERYTHING`
- `SRC-REPO-UNIXSYSDEV`
