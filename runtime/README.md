# Model Inspection runtimes

This root owns production runtime adapters used by the protected Model
Inspection worker. Runtime projects stay independent of the worker protocol and
application contracts; the worker maps project-owned runtime results at its own
boundary.

- `GraniteEdgeAI.ModelInspection.LlamaSharp` is the pinned Windows x64,
  CPU-only LLamaSharp 0.27.0 VocabOnly runtime.

This root does not authorize GPU backends, inference, context creation,
benchmarking, or model classification.
