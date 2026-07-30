# OpenVINO Quality Capture Adapter Design

## Decision

WB-04 quality capture uses one fresh governed worker process for each accepted generating configuration. The worker constructs exactly one OpenVINO GenAI LLMPipeline and makes seven serial calls: P1, P2, P3, P4, P5, P6 turn 1, and P6 turn 2. It returns raw text and execution facts only. The controller owns identity validation, process governance, record publication, resume, and the bridge into blind adjudication.

This is separate from scripts/testing/official_openvino/measurement_worker.py. That worker owns the four-token runtime measurement workload; adding quality capture to it would combine performance and quality evidence.

## Accepted-campaign binding

Before a worker starts, the controller reads campaign-identity.json and measurement-summary.json from one campaign root. It recomputes the canonical identity through measure_official_openvino.build_campaign_identity and requires byte-for-byte equality with the persisted document and its campaign_identity_sha256.

The recomputation validates the selected spec and matrix case, artifact manifest/model root, build provenance, GenAI extension and DLL hashes, OpenVINO package/runtime-library identities, controller/runtime source identities, Python executable, requested device, properties, and runtime configuration. The measurement summary must be schema version 1, status measured, accepted true, sample_count 3, cleanup_process_count 0, and exactly match test ID, context, campaign identity hash, and runtime configuration hash.

Each capture record binds runtime_summary_sha256, runtime_config_sha256, campaign_identity_sha256, quality_worker_spec_sha256, worker_result_sha256, and guard_evidence_sha256. This binds output to the actual model/build/spec/configuration rather than a row label.

## Governed worker

quality_campaign.py obtains the worker environment from measure_official_openvino.build_worker_environment and invokes quality_worker.py through guarded_build.run_guarded_command with GuardLimits. The guard receives that environment as a typed mapping, launches the process with it, and records its canonical environment SHA-256 in execution evidence. This generic guard uses the existing Job Object process-tree ownership and cleanup evidence without requiring the measurement worker's metrics-marker schema. Its available-RAM floor is exactly 2_048 * 1024 * 1024 bytes. Below-floor pre-launch, in-run, or post-run RAM, timeout, failed worker, fallback, or nonzero cleanup is not an accepted capture.

There is one worker process tree at a time and one worker per accepted configuration. No worker starts for expected-rejection, failed, fallback, incomplete, or unaccepted-runtime rows.

## Worker contract

quality_worker.py consumes a canonical official-openvino-wb04-quality-worker-spec/v1 document with only the already-bound model path, device, OpenVINO properties, and frozen prompt contract. It has no rubric, score, rank, private label, codec, or performance target.

It creates one LLMPipeline and one GenerationConfig with:

- max_new_tokens = 256
- do_sample = False
- rng_seed = 42
- apply_chat_template = False

P1 to P5 use exact frozen raw prompts. P6 turn 1 uses the frozen turn-1 prompt. P6 turn 2 is constructed only after turn 1 completes:

    User: the stripped frozen P6 turn-1 prompt
    Assistant: the actual unmodified P6 turn-1 output
    User: the stripped frozen P6 turn-2 prompt

The worker emits every raw turn output, output SHA-256, partial output/failure details when a turn fails, and a canonical worker-result SHA-256. Its CLI receives both --spec and a controller-assigned --result path and atomically creates that result document. It does not evaluate deterministic gates or rubric dimensions.

## Publication and resume

run_official_openvino_quality.py remains the owner of frozen prompt/rubric loading and response-record validation. The campaign adapter turns validated worker outcomes into existing openvino-quality-response-record documents, adds the worker/guard identity fields above, and publishes a capture summary only after all P1-P6 records validate.

Evidence is create-only. Resume validates every persisted campaign identity, summary hash, worker-spec hash, guard-evidence hash, response hash, record hash, and capture summary before any process is started. Missing, duplicate, altered, wrong-campaign, wrong-model, wrong-build, wrong-config, nonzero-cleanup, or incomplete evidence is rejected. A failed turn is preserved as captured-with-failures and never converted into a complete or scored pass.

## Blind-adjudication adapter

adjudicate_official_openvino_quality.py gains a collector for validated capture summaries and records. It projects only opaque blind label, frozen prompt contract, raw output/turn evidence, hashes, and deterministic-gate result into the existing openvino-quality-blind-scoring-input schema. Campaign, model, build, test ID, precision, codec, memory, speed, and device fields do not enter that object.

The reviewer receives the scoring input without the blind map. The hash-bound score sheet is frozen before the map is supplied to final adjudication. Existing objective caps, reviewer/notes requirements, and identical-content equality checks remain controlling.

## P4 caveat

P4 is frozen and describes an upstream llama.cpp/Q4 baseline. It must not be changed or treated as proof of the OpenVINO configuration. For WB-04 it is a route-independent fact-retention regression task. Campaign identity and accepted runtime evidence establish actual model/build/device/configuration.
