using System.Text.Json;

namespace GraniteEdgeAI.HardwareInspection.LlamaCppProbe;

internal static class LlamaCppProbeProtocol
{
    internal const string ProbeIdentity = "granite-edge-hardware-llamacpp-capabilities/1";

    internal static byte[] CreateIdentity() => CreateJson(static writer =>
    {
        writer.WriteNumber("schemaVersion", 1);
        writer.WriteString("probeIdentity", ProbeIdentity);
        writer.WriteString("managedPackage", "LLamaSharp");
        writer.WriteString("managedVersion", "0.27.0");
        writer.WriteString("backendPackage", "LLamaSharp.Backend.Cpu");
        writer.WriteString("backendVersion", "0.27.0");
        writer.WriteString("llamaSharpCommit", "7cbbc45e421d55794d5050d126e0b96511007007");
        writer.WriteString("mappedLlamaCppCommit", "3f7c29d318e317b63f54c558bc69803963d7d88c");
        writer.WriteString("runtimeIdentifier", "win-x64");
    });

    internal static byte[] CreateCapabilities(IReadOnlyList<LlamaCppNativeDevice> devices)
    {
        ArgumentNullException.ThrowIfNull(devices);
        return CreateJson(writer =>
        {
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteString("probeIdentity", ProbeIdentity);
            writer.WriteStartArray("backends");
            writer.WriteStringValue("cpu");
            writer.WriteEndArray();
            writer.WriteStartArray("devices");
            foreach (LlamaCppNativeDevice device in devices)
            {
                writer.WriteStartObject();
                writer.WriteNumber("ordinal", device.Ordinal);
                writer.WriteString("bufferType", device.BufferType);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        });
    }

    private static byte[] CreateJson(Action<Utf8JsonWriter> writeProperties)
    {
        using var buffer = new MemoryStream(capacity: 4096);
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writeProperties(writer);
            writer.WriteEndObject();
            writer.Flush();
        }

        buffer.WriteByte(0x0a);
        return buffer.ToArray();
    }
}
