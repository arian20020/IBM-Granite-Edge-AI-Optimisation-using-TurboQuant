# UCL Cross-Route Native Validation Design

## Status

Approved by the user on 27 August 2026.

## Objective

Continue the application from the current cross-route integration baseline on the UCL Intel laptop and establish, with native evidence, that the GGUF and OpenVINO routes are functional, consistent where they share product behaviour, and safely different only where their runtime formats require it. Route-local defects may be repaired by the route owner. Shared defects are repaired only by the integration owner.

## Authoritative baseline

The source branch is `feature/cross-route-optimisation-integration-v1`. Before worker creation, the coordinator must push the branch and record its exact remote tip. Every worker must fetch that branch and verify the exact commit supplied in its prompt before creating an isolated worktree.

The baseline already contains the following histories:

- OpenVINO optimisation adapter and its V2.1 build-identity correction;
- GGUF production chat runtime and quantisation route;
- cross-route compatibility and optimisation contracts;
- canonical cross-route optimisation UI;
- model-import picker and drag-and-drop integration;
- production Hardware Inspection and Intel integration history;
- shared onboarding, compatibility, optimisation and chat journey integration;
- optimisation centring, active-stage progress animation, chat-shell removal and Enter/Shift+Enter behaviour through commit `192ca6f5`.

The final baseline commit will additionally include this design and its execution handoff documentation. Workers must use the final pushed remote tip, not the earlier `192ca6f5` commit by itself.

## Worker topology

### UCL-C0 integration owner

UCL-C0 owns the final integration branch, the worker schedule, shared-file decisions, final native validation and final handoff. It creates or verifies isolated worktrees for UCL-O1 and UCL-G1, imports their verified commits sequentially, and resolves shared defects without duplicating route code.

UCL-C0 exclusively owns:

- `IBM Granite with TurboQuant (Intel)/MainWindow.xaml*`;
- `IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj` and packaging targets;
- `Features/Onboarding/**`;
- `Features/ModelImport/**` except a route-local adapter explicitly delegated by C0;
- `Features/ModelHardwareCompatibility/**`;
- `Features/ModelOptimization/**`;
- shared themes, resources, navigation and route-neutral contracts;
- solution-level and cross-route tests;
- final package registration and final end-to-end run.

### UCL-O1 OpenVINO route owner

UCL-O1 owns native OpenVINO inspection, conversion, compatibility input, optimisation execution and prompt-route validation. It may change only OpenVINO-owned production code and route-local tests. It must use the existing official OpenVINO worker staging package and verify the exact manifest digest before execution. It must pass the vendor build identity through unchanged.

Its normal ownership is limited to:

- `Features/OpenVinoRoute/**`;
- `infrastructure/GraniteEdgeAI.OpenVino.WorkerClient/**`;
- `shared/GraniteEdgeAI.OpenVino.Contracts/**` when the existing contract is being correctly implemented rather than widened;
- OpenVINO-specific tests and handoff documentation.

Any required change outside this boundary is reported to C0 as a minimal proposed patch with evidence. O1 does not edit the shared file itself.

### UCL-G1 GGUF route owner

UCL-G1 owns native GGUF inspection/runtime, verified quantisation execution, GGUF capability validation and GGUF prompt-route validation. It may change only GGUF-owned production code and route-local tests. It must retain the exact pinned worker, quantiser and runtime closure verification and must not infer settings absent from the confirmed execution plan.

Its normal ownership is limited to:

- `Features/GgufRuntime/**`;
- GGUF-specific execution adapters that do not own shared optimisation presentation;
- `infrastructure/GraniteEdgeAI.GgufRuntime.*` and `infrastructure/GraniteEdgeAI.GgufQuantization.*`;
- `runtime/GraniteEdgeAI.GgufRuntime.*` and `runtime/GraniteEdgeAI.GgufQuantization.*`;
- `shared/GraniteEdgeAI.GgufRuntime.*` and `shared/GraniteEdgeAI.GgufQuantization.*` when implementing existing contracts;
- `workers/GraniteEdgeAI.GgufRuntime.Worker/**` and the verified GGUF quantisation worker;
- GGUF-specific tests and handoff documentation.

Any required change outside this boundary is reported to C0 as a minimal proposed patch with evidence. G1 does not edit the shared file itself.

## Parallelism and Intel-laptop scheduling

O1 and G1 may inspect code, write route-local tests, build route-local projects and prepare candidate fixes in parallel in separate worktrees. The following operations are exclusive and scheduled by C0:

- launching the desktop application;
- model inspection using a native worker;
- Hardware Inspection;
- compatibility decisions based on live available memory;
- conversion or quantisation;
- optimised-model validation;
- native prompt generation;
- package registration or replacement;
- full solution or package builds that write shared repository output directories.

Only one exclusive operation may run at a time. Workers use the VS Code integrated terminal and must not create recurring PowerShell windows or uncontrolled background processes. Every launched app or worker process must have a known owner and a cleanup step.

## Common end-to-end journey

Both route owners validate the same product journey:

1. Import through the file picker.
2. Import through Explorer drag-and-drop where the route supports that source shape.
3. Model Inspection with truthful progress and a terminal route decision.
4. Hardware Inspection using verified `llmfit` and packaged llama.cpp probe components.
5. Model/hardware compatibility using current hardware facts and current available-memory evidence.
6. A direct-run decision when the current model fits safely, while still offering optional optimisation.
7. A quantisation- or conversion-required decision when a safe alternative exists but the current representation does not fit.
8. An honest unsupported decision when no admitted configuration fits.
9. Selection from the shared optimisation preference scale using the canonical names and order.
10. Seven-stage optimisation progress with exactly one animated active stage.
11. Terminal success, controlled failure, cancellation and retry behaviour.
12. The post-optimisation choice between using the model in the in-product chat and exporting the resulting model to the user's computer when that capability is implemented.
13. Chat without the onboarding step indicator, with Enter to send and Shift+Enter for a newline.

If a planned post-optimisation capability is not yet implemented, the worker records the exact missing seam and tests the current honest disabled or unavailable state. It must not fabricate completion.

## Cross-route consistency contract

The routes must share:

- the established light visual language, content width and centring rules;
- model-import card behaviour and path privacy;
- onboarding stage meanings;
- Hardware Inspection presentation and hardware-facts semantics;
- compatibility result categories and available-memory meaning;
- optimisation preference names, order and slider semantics;
- progress stage count, state meanings and active animation;
- terminal action hierarchy, cancellation and retry semantics;
- shared chat transition and keyboard interaction;
- accessibility, 200% text, High Contrast and safe diagnostic requirements.

The routes may differ only in route-specific facts such as package shape, runtime identity, conversion versus quantisation, admissible execution configurations, output artifact structure and runtime-specific evidence. Those differences must remain below the shared presentation boundary unless the user needs to understand them.

## Native evidence matrix

Each route produces evidence for:

- exact branch, base and tested commit;
- Windows, CPU, GPU, installed memory and available-memory summary without host identity or private paths;
- verified worker/manifest identities and digests;
- picker and drag/drop results;
- model-inspection terminal result;
- Hardware Inspection terminal result and safe support codes;
- compatibility candidates, selected candidate and memory components;
- direct-fit, optimisation-required and unsupported scenarios where feasible with approved fixtures or models;
- optimisation preference mapping;
- all seven progress transitions;
- resulting artifact identity without exposing its private path;
- chat startup and one bounded local prompt;
- Enter and Shift+Enter behaviour;
- cancellation, cleanup and absence of orphan processes;
- build and test commands with discovered, passed, failed and skipped counts;
- screenshots of shared checkpoints at the same window size and Windows scaling.

Raw model paths, user names, host names, unrestricted provider output and credentials are never placed in commits, logs, screenshots, prompts or handoffs.

## Failure and repair rules

Workers diagnose before editing and add a failing regression test where practical. They may repair route-local defects and commit focused changes. They must not:

- weaken hashes, manifests, path checks or fail-closed decisions;
- replace real evidence with fixtures while claiming native success;
- invent a configuration value missing from an authoritative plan;
- widen a shared contract without C0 approval;
- edit shared XAML to make only one route pass;
- duplicate compatibility, optimisation, navigation or presentation logic;
- suppress a failing test or convert a failure into an unexplained skip;
- expose local paths or raw provider payloads.

When a defect crosses ownership boundaries, the route worker stops that repair, records reproduction evidence and sends C0 the smallest route-neutral correction proposal. Route-local work that is independent of the blocked correction may continue.

## Integration sequence

1. C0 publishes and pins the authoritative baseline.
2. O1 and G1 create isolated branches and pass preflight.
3. O1 and G1 perform route-local preparation in parallel.
4. C0 schedules GGUF and OpenVINO native runs serially.
5. Each route owner commits and pushes only verified route-local changes.
6. C0 reviews and integrates one route commit at a time.
7. C0 implements any accepted shared corrections with cross-route regression tests.
8. C0 builds the final application with every required trusted payload supplied.
9. C0 runs the final GGUF and OpenVINO journeys serially from the same integrated commit.
10. C0 compares screenshots, behaviour, evidence and cleanup outcomes and produces one final handoff.

## Completion criteria

The wave is complete only when:

- both routes are tested from the same final integrated commit on the UCL Intel laptop;
- all trusted payloads required by the tested route are present and verified;
- shared screens and interactions are demonstrably consistent;
- route-specific differences are intentional and documented;
- Hardware Inspection provides usable hardware evidence or an accurately diagnosed external blocker;
- compatibility uses current, non-zero memory evidence when Windows reports usable memory;
- admitted direct-run and optimisation-required decisions are exercised;
- implemented optimisation execution reaches a truthful terminal result;
- implemented chat starts and accepts the defined keyboard gestures;
- cancellation and cleanup leave no orphan app, worker, converter, quantiser or runtime processes;
- relevant builds and tests pass with non-zero discovery, with every remaining skip or failure explicitly explained;
- the final branch and handoff contain no private paths, model data, credentials or raw hardware-provider output.
