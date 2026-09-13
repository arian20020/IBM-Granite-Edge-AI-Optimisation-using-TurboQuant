using GraniteEdgeAI.HardwareInspection.LlamaCppProbe;

return await LlamaCppProbeApplication.RunAsync(
    args,
    Console.OpenStandardOutput(),
    new LLamaSharpNativeCapabilityApi(new LLamaSharpNativeInterop())).ConfigureAwait(false);
