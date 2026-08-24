using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Capabilities;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.Capabilities;

[TestClass]
public sealed class SupportMatrixResolverTests
{
    [TestMethod]
    [DataRow(
        nameof(SupportLevel.DeclaredSupported),
        nameof(InstallationState.InstalledAndVerified),
        nameof(SupportAvailability.Available))]
    [DataRow(
        nameof(SupportLevel.DeclaredSupported),
        nameof(InstallationState.NotInstalled),
        nameof(SupportAvailability.Unavailable))]
    [DataRow(
        nameof(SupportLevel.Experimental),
        nameof(InstallationState.VerifiedAndOptedIn),
        nameof(SupportAvailability.ExperimentalAvailable))]
    public void Resolve_AppliesTheThreeDeclaredRules(
        string level,
        string installation,
        string expected)
    {
        SupportAvailability resolved = SupportMatrixResolver.Resolve(
            Enum.Parse<SupportLevel>(level),
            Enum.Parse<InstallationState>(installation));

        Assert.AreEqual(expected, resolved.ToString());
    }

    [TestMethod]
    public void Resolve_TreatsEveryOtherPairingAsUnsupported()
    {
        // Exhaustive sweep. The fourth rule is "unknown or ambiguous is
        // unsupported", so a pairing nobody thought about must land there rather
        // than on an available value. A new enum member fails this test.
        (SupportLevel Level, InstallationState Installation)[] declared =
        [
            (SupportLevel.DeclaredSupported, InstallationState.InstalledAndVerified),
            (SupportLevel.DeclaredSupported, InstallationState.NotInstalled),
            (SupportLevel.Experimental, InstallationState.VerifiedAndOptedIn)
        ];

        int visited = 0;

        foreach (SupportLevel level in Enum.GetValues<SupportLevel>())
        {
            foreach (InstallationState installation in Enum.GetValues<InstallationState>())
            {
                visited++;
                SupportAvailability resolved =
                    SupportMatrixResolver.Resolve(level, installation);

                if (declared.Contains((level, installation)))
                {
                    Assert.AreNotEqual(
                        SupportAvailability.Unsupported,
                        resolved,
                        $"{level} with {installation} is a declared rule.");
                    continue;
                }

                Assert.AreEqual(
                    SupportAvailability.Unsupported,
                    resolved,
                    $"{level} with {installation} is undeclared and must be unsupported.");
            }
        }

        Assert.AreEqual(
            12,
            visited,
            "An enum member was added; extend the declared-rule table above.");
    }

    [TestMethod]
    public void Resolve_DoesNotAdmitAnExperimentalRouteWithoutOptIn()
    {
        // Installed and verified is not enough for an experimental route: it may
        // produce wrong output rather than merely failing, so the user must have
        // said yes to it explicitly.
        Assert.AreEqual(
            nameof(SupportAvailability.Unsupported),
            SupportMatrixResolver.Resolve(
                SupportLevel.Experimental,
                InstallationState.InstalledAndVerified).ToString());
    }

    [TestMethod]
    public void ProvisionalV1_CarriesProvisionalProvenanceAndAStableVersion()
    {
        SupportMatrix matrix = SupportMatrix.ProvisionalV1();

        Assert.AreEqual(
            nameof(PolicyProvenance.Provisional), matrix.Provenance.ToString());
        Assert.AreEqual("support-matrix-v1", matrix.MatrixVersion);
    }

    [TestMethod]
    public void ProvisionalV1_EntryIdsAreUnique()
    {
        // A candidate carries its entry id as provenance, so a duplicate id makes
        // two different configurations indistinguishable after the fact.
        IReadOnlyList<CompatibilitySupportEntry> entries = SupportMatrix.ProvisionalV1().Entries;

        Assert.AreEqual(
            entries.Count,
            entries.Select(entry => entry.EntryId).Distinct().Count());
    }

    [TestMethod]
    public void ProvisionalV1_ContainsTheImportedCpuBaselineShape()
    {
        // Without an entry admitting the as-imported configuration on CPU, no
        // baseline candidate can ever be generated on a machine with no GPU.
        Assert.IsTrue(
            SupportMatrix.ProvisionalV1().Entries.Any(entry =>
                entry.Device == DeviceRouteId.Cpu
                && entry.Weights == GgufWeightFormat.Imported
                && entry.Level == SupportLevel.DeclaredSupported));
    }

    [TestMethod]
    public void Absent_HasNoEntriesAndAbsentProvenance()
    {
        SupportMatrix matrix = SupportMatrix.Absent();

        Assert.AreEqual(nameof(PolicyProvenance.Absent), matrix.Provenance.ToString());
        Assert.AreEqual(0, matrix.Entries.Count);
    }

    [TestMethod]
    public void FromEntries_RejectsDuplicateEntryIds()
    {
        CompatibilitySupportEntry entry = CompatibilitySupportEntry.Create(
            "duplicate",
            RuntimeRouteId.LlamaCpp,
            CompatibilityBackend.Cpu,
            DeviceRouteId.Cpu,
            GpuOffloadLevel.None,
            GgufWeightFormat.Imported,
            GgufKvCacheFormat.F16,
            1024,
            32768,
            SupportLevel.DeclaredSupported,
            requiresEvidence: false);

        Assert.ThrowsExactly<ArgumentException>(
            () => SupportMatrix.FromEntries(
                "v-test", PolicyProvenance.Provisional, [entry, entry]));
    }

    [TestMethod]
    public void FromEntries_CopiesEntriesSoLaterMutationCannotChangeTheMatrix()
    {
        List<CompatibilitySupportEntry> entries = [];

        SupportMatrix matrix = SupportMatrix.FromEntries(
            "v-test", PolicyProvenance.Provisional, entries);

        entries.Add(CompatibilitySupportEntry.Create(
            "added-later",
            RuntimeRouteId.LlamaCpp,
            CompatibilityBackend.Cpu,
            DeviceRouteId.Cpu,
            GpuOffloadLevel.None,
            GgufWeightFormat.Imported,
            GgufKvCacheFormat.F16,
            1024,
            32768,
            SupportLevel.DeclaredSupported,
            requiresEvidence: false));

        Assert.AreEqual(0, matrix.Entries.Count);
    }
}
