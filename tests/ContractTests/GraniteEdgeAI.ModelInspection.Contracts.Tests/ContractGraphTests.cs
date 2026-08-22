using System.Reflection;
using GraniteEdgeAI.ModelInspection.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.Contracts.Tests;

/// <summary>
/// Verifies that the shared worker contract assembly remains independent from
/// WinUI, native runtimes and executable implementations.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public sealed class ContractGraphTests
{
    /// <summary>
    /// The shared assembly may use framework libraries only. Pulling a UI,
    /// native runtime or worker implementation into this project would destroy
    /// the stable process boundary before the worker is even implemented.
    /// </summary>
    [TestMethod]
    public void ContractAssembly_ReferencesNoForbiddenImplementationAssembly()
    {
        Assembly assembly = typeof(WorkerProtocol).Assembly;

        string[] references = assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.IsFalse(
            references.Any(IsForbiddenReference),
            "The contract assembly referenced an implementation-specific assembly: " +
            string.Join(", ", references.Where(IsForbiddenReference)));
    }

    private static bool IsForbiddenReference(string assemblyName)
    {
        return assemblyName.Contains(
                   "Microsoft.UI.Xaml",
                   StringComparison.OrdinalIgnoreCase) ||
               assemblyName.Contains(
                   "LLamaSharp",
                   StringComparison.OrdinalIgnoreCase) ||
               assemblyName.Contains(
                   "TurboQuant",
                   StringComparison.OrdinalIgnoreCase) ||
               assemblyName.Contains(
                   "GraniteEdgeAI.ModelInspection.Worker",
                   StringComparison.OrdinalIgnoreCase);
    }
}
