using System.Security;
using GraniteEdgeAI.HardwareInspection.Foundation.Validation;
using Microsoft.Win32;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Windows;

internal interface IProcessorNameSource
{
    bool TryGetName(out string? value);
}

internal interface IProcessorNameRegistry
{
    object? ReadProcessorName();
}

internal sealed class RegistryProcessorNameSource : IProcessorNameSource
{
    private readonly IProcessorNameRegistry _registry;

    internal RegistryProcessorNameSource()
        : this(new LocalMachineProcessorNameRegistry())
    {
    }

    internal RegistryProcessorNameSource(IProcessorNameRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public bool TryGetName(out string? value)
    {
        value = null;
        object? observed;
        try
        {
            observed = _registry.ReadProcessorName();
        }
        catch (Exception exception) when (exception is
            IOException or
            SecurityException or
            UnauthorizedAccessException or
            PlatformNotSupportedException)
        {
            return false;
        }

        if (observed is not string name || !HardwareText.IsSafe(name, 256))
        {
            return false;
        }

        value = name;
        return true;
    }

    private sealed class LocalMachineProcessorNameRegistry : IProcessorNameRegistry
    {
        private const string ProcessorKey =
            @"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0";

        public object? ReadProcessorName() =>
            Registry.GetValue(ProcessorKey, "ProcessorNameString", defaultValue: null);
    }
}
