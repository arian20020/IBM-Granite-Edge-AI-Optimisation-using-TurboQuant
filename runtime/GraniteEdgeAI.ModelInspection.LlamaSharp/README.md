# GraniteEdgeAI.ModelInspection.LlamaSharp

This production library owns the exact LLamaSharp 0.27.0 and
LLamaSharp.Backend.Cpu 0.27.0 dependency pair for the first Windows x64
CPU/VocabOnly inspection path.

Its public `IVocabOnlyModelProbe` boundary accepts a caller-observed file length
and exact UTC last-write time. `VocabOnlyModelProbe` verifies that identity
before hashing or configuring native code, then returns project-owned records;
no LLamaSharp or native handle type crosses the boundary. Package-validation
and genuine native-load fractions are the only progress facts exposed.

The protected worker owns conversion to worker protocol evidence. This project
therefore has no dependency on the Worker or Contracts assemblies. The
feasibility CLI consumes this same library rather than carrying a second probe
implementation.

Out of scope are GPU backends, inference, context creation, benchmarking, model
classification, and application composition.
