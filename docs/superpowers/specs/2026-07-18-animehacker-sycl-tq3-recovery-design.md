# animehacker SYCL TQ3 Recovery Design

## Goal

Recover and formally execute WB-03 tests AH-09 and AH-10 using a genuine Intel SYCL partial-offload route, preserve complete measurement and quality evidence for every runnable row, and update the controlled workbook without weakening the laptop memory-safety gate.

## Root cause and recovered reference

The July 8 AH-09 result was genuine. Its retained command and output prove that commit `5bc5ed3bdc25003aa9f07422753a7b8d4f9190fc` generated valid multi-token output with Level Zero, `-ngl 1 -sm none -mg 0`, and host-resident TQ3 K/V cache. The old and current SYCL binaries both reproduce that result in CLI mode, including with the current Granite 3B Q4_K_M weights.

The failed formal retest changed several variables at once: OpenCL replaced Level Zero, the harness forced output-tensor placement, flash attention was explicitly disabled, and the server collector sent a raw completion prompt without Granite role tokens. A controlled server pilot using the proven Level Zero placement still returned immediate EOS with the raw prompt. Applying Granite's embedded chat template to the same server command produced 128 valid tokens. Therefore the empty one-token response was a prompt-protocol defect, not proof that the SYCL TQ3 cache kernel was unusable. The separate forced flash-off/tensor-placement abort is a rejected configuration and is not required by the recovered route.

AH-10 did not pass in the earlier campaign; it was explicitly skipped. It must be treated as a new, memory-guarded investigation rather than described as a recovered prior success.

## Selected implementation

1. Add a small, deterministic Granite prompt-formatting helper that emits the embedded role-token form:
   `<|start_of_role|>user<|end_of_role|>{prompt}<|end_of_text|>\n<|start_of_role|>assistant<|end_of_role|>`.
2. Apply that formatter to SYCL TQ3 server measurements so the HTTP completion path matches the CLI's model protocol.
3. Restore the proven partial-offload flags for SYCL TQ3: `-ngl 1 -sm none -mg 0` under `ONEAPI_DEVICE_SELECTOR=level_zero:0`.
4. Do not force `output.*=SYCL0`, do not add the explicit flash-off flag, and do not claim that the TQ3 cache itself resides on the GPU. Genuine GPU proof comes from the one-layer SYCL allocation and measured process GPU utilization; cache proof remains the host TQ3 allocation.
5. Preserve the existing oneAPI environment setup and crash-safe evidence layout.

## Test-first implementation

Before changing production code, add regression tests proving that:

- SYCL TQ3 commands request exactly one layer plus `-sm none -mg 0`;
- SYCL TQ3 commands omit forced output-tensor placement and explicit flash-off;
- the SYCL TQ3 request prompt includes the exact Granite user and assistant role tokens;
- CPU and non-TQ3 command behavior is not unintentionally changed.

The new tests must be observed failing for the missing recovery behavior before the minimal implementation is added. Targeted tests and the complete testing-script suite must pass afterward.

## Formal execution

AH-09 will run a fresh pilot, one warm-up, and three formal samples. Each accepted sample must contain valid multi-token output plus peak working set, private bytes, minimum available RAM, KV allocation, TTFT, prompt/decode throughput, CPU mean/median/peak, GPU mean/median/peak, and peak process GPU memory. P1-P6 quality will be rerun and scored under the existing harsh format-neutral rubric.

AH-10 will first run a pilot with the existing 2 GiB emergency available-RAM floor. If the pilot remains above the floor and returns valid multi-token output, it will proceed through warm-up, three formal samples, and P1-P6 quality. If it crosses the floor, the process tree will be stopped and the row will remain safety-classified from measured evidence. The floor will not be bypassed or lowered.

## Workbook and evidence control

Accepted results will replace the current AH-09/AH-10 capability wording and update runtime, utilization, resource, quality, failure-resolution, final-decision, revision-register, manifest, Markdown, and generated DOCX fields. Rejected diagnostics remain preserved and clearly labelled. Final reconciliation must verify three samples for every runnable row, response hashes, no blank cells or literal `N/A`, synchronized revision history, DOCX structural integrity, and zero unresolved test failures.

## Success criteria

- AH-09 completes with valid SYCL partial-offload TQ3 evidence and all required metrics.
- AH-10 either completes to the same standard or has a fresh, terminal safety classification at the unchanged memory floor.
- No sentinel throughput or empty-EOS result is accepted.
- All repository and workbook validation gates pass before completion is claimed.
