# GGUF runtime process integration

Normal test runs use deterministic local process fixtures and require no model,
runtime download, network access, or listener.

The optional controlled smoke reads the path in
`GRANITE_GGUF_RUNTIME_TEST_CONFIG`. Copy
`tests/TestFixtures/GGUF/controlled-inference-model.example.json` outside the
repository, replace its example paths and zero hashes with approved local
values, and run:

```powershell
$env:GRANITE_GGUF_RUNTIME_TEST_CONFIG = 'C:\approved\controlled-runtime.json'
dotnet run --project .\tests\IntegrationTests\GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests\GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests.csproj -- --filter 'FullyQualifiedName~GgufRealModelSmokeTests' --progress off
Remove-Item Env:GRANITE_GGUF_RUNTIME_TEST_CONFIG
```

Without the variable, the test skips with `controlled GGUF runtime/model not
configured`. A configured run must load, stream two turns, stop a third turn,
close, and confirm the model hash is unchanged.
