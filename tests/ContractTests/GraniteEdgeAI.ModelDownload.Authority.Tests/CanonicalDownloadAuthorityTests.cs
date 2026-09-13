using System.Reflection;
using System.Runtime.CompilerServices;
using GraniteEdgeAI.Features.ModelImport.ModelDownload;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

namespace GraniteEdgeAI.ModelDownload.Authority.Tests;

[TestClass]
public sealed class CanonicalDownloadAuthorityTests
{
    private static readonly string[] ExpectedFriendAssemblies =
    [
        "GraniteEdgeAI.ModelDownload.Authority.Tests",
        "GraniteEdgeAI.UnitTests"
    ];

    [TestMethod]
    public void CanonicalCatalogMapsAllFivePreferenceBandsToReviewedPins()
    {
        (OptimizationPreferenceBand Band, string File, long Length, string Hash)[] expected =
        [
            (OptimizationPreferenceBand.MaximumEfficiency, "granite-4.0-h-micro-Q2_K.gguf", 1_226_247_840, "e60b313fc0ce2a0a2c3903f4399ac61fe1048c38f219bcbd9f7a708e168b8ead"),
            (OptimizationPreferenceBand.Efficient, "granite-4.0-h-micro-Q3_K_M.gguf", 1_555_472_032, "bb684177d546a2c6d3aec9cc591fc8fa75d9c16a27e798cbb2400fe52d9b7e29"),
            (OptimizationPreferenceBand.Balanced, "granite-4.0-h-micro-Q4_K_M.gguf", 1_942_564_512, "c698c78e895740f0e707eb7f8e92894f83f6d5b3f2f2f0b446dfe9635fa0063e"),
            (OptimizationPreferenceBand.HighCapability, "granite-4.0-h-micro-Q5_K_M.gguf", 2_273_455_776, "69857575412143ea74d66e4d54ad70ec420d42452eadfff4a7cc043fe445ed4c"),
            (OptimizationPreferenceBand.MaximumCapability, "granite-4.0-h-micro-Q8_0.gguf", 3_397_676_704, "a009111abf2865b7aad1e66326a6c772cddc29bccd22898f470292068b27bb59")
        ];

        Assert.AreEqual(5, PinnedGraniteModelCatalog.Entries.Count);
        foreach (var item in expected)
        {
            ModelDownloadCatalogEntry entry =
                PinnedGraniteModelCatalog.ForPreferenceBand(item.Band);
            Assert.AreEqual("ibm-granite/granite-4.0-h-micro-GGUF", entry.RepositoryId);
            Assert.AreEqual("51ce07a9c9cfa971ca359d9625836bf8a4a1b61f", entry.Revision);
            Assert.AreEqual(item.File, entry.FileName);
            Assert.AreEqual(entry.ExpectedByteLength, item.Length);
            Assert.AreEqual(entry.ExpectedSha256, item.Hash);
        }
    }

    [TestMethod]
    public void CanonicalCatalogRejectsUnknownPreferenceBand() =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            PinnedGraniteModelCatalog.ForPreferenceBand((OptimizationPreferenceBand)999));

    [TestMethod]
    public void CanonicalAuthorityConstructorsAreNotPublic()
    {
        Assert.AreEqual(0, typeof(ModelDownloadCatalogEntry).GetConstructors(
            BindingFlags.Public | BindingFlags.Instance).Length);
        Assert.IsFalse(typeof(DownloadedArtifactEvidence).IsPublic);
        Assert.IsFalse(typeof(DownloadVerificationContext).IsPublic);
        Assert.IsFalse(typeof(CanonicalDownloadVerifier).IsPublic);
    }

    [TestMethod]
    public void CanonicalAuthorityFriendsOnlyExplicitTestAssemblies()
    {
        string[] friends = typeof(PinnedGraniteModelCatalog).Assembly
            .GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(attribute => attribute.AssemblyName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(ExpectedFriendAssemblies, friends);
    }

    [TestMethod]
    public void CanonicalVerifierAcceptsExactCompletedDownloaderEvidence()
    {
        DownloadVerificationContext expected = Context(OptimizationPreferenceBand.Balanced);
        Assert.IsTrue(CanonicalDownloadVerifier.IsExactMatch(expected, ExactEvidence(expected)));
    }

    [TestMethod]
    public void LegacyF1OwnerFixtureConstructorRemainsTestOnlyAndBandBound()
    {
        var entry = new ModelDownloadCatalogEntry(
            "fixture", "Balanced", "Q4_K_M", 40, 60, false,
            "fixture/repository", "fixture-revision", "fixture.gguf", 1,
            new string('a', 64));

        Assert.AreEqual(OptimizationPreferenceBand.Balanced, entry.Band);
        Assert.AreEqual(0, typeof(ModelDownloadCatalogEntry).GetConstructors(
            BindingFlags.Public | BindingFlags.Instance).Length);
    }

    [TestMethod]
    public void CanonicalCatalogRejectsSelfConsistentNonCatalogTuple()
    {
        var expected = Context((OptimizationPreferenceBand)999);
        var observed = new DownloadedArtifactEvidence(
            DownloadCompletionStatus.Completed,
            (OptimizationPreferenceBand)999,
            "attacker/repository", "attacker-revision", "attacker.gguf", 123,
            new string('a', 64), expected.OperationId, expected.Generation,
            expected.PublicationIdentity, expected.InspectionRequestId,
            expected.LifecycleGeneration);

        Assert.IsFalse(CanonicalDownloadVerifier.IsExactMatch(expected, observed));
    }

    [TestMethod]
    [DataRow("band")]
    [DataRow("repository")]
    [DataRow("revision")]
    [DataRow("filename")]
    [DataRow("length")]
    [DataRow("hash")]
    [DataRow("operation")]
    [DataRow("generation")]
    [DataRow("publication")]
    [DataRow("inspection")]
    [DataRow("lifecycle")]
    public void CanonicalVerifierRejectsEachObservedIdentityMutation(string field)
    {
        DownloadVerificationContext expected = Context(OptimizationPreferenceBand.Balanced);
        DownloadedArtifactEvidence observed = ExactEvidence(expected);
        observed = field switch
        {
            "band" => observed with { Band = OptimizationPreferenceBand.Efficient },
            "repository" => observed with { RepositoryId = observed.RepositoryId + "-other" },
            "revision" => observed with { Revision = observed.Revision + "0" },
            "filename" => observed with { FileName = "other.gguf" },
            "length" => observed with { ObservedByteLength = observed.ObservedByteLength + 1 },
            "hash" => observed with { ObservedSha256 = new string('f', 64) },
            "operation" => observed with { OperationId = Guid.NewGuid() },
            "generation" => observed with { Generation = observed.Generation + 1 },
            "publication" => observed with { PublicationIdentity = "other-publication" },
            "inspection" => observed with { InspectionRequestId = Guid.NewGuid() },
            "lifecycle" => observed with { LifecycleGeneration = observed.LifecycleGeneration + 1 },
            _ => throw new InvalidOperationException(field)
        };

        Assert.IsFalse(CanonicalDownloadVerifier.IsExactMatch(expected, observed), field);
    }

    [TestMethod]
    [DataRow("../private/model.gguf")]
    [DataRow("C:\\private\\model.gguf")]
    [DataRow("folder/model.gguf")]
    public void CanonicalVerifierRejectsPathLikeFilename(string fileName)
    {
        DownloadVerificationContext expected = Context(OptimizationPreferenceBand.Balanced);
        DownloadedArtifactEvidence observed = ExactEvidence(expected) with { FileName = fileName };
        Assert.IsFalse(CanonicalDownloadVerifier.IsExactMatch(expected, observed));
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(1)]
    public void CanonicalVerifierRejectsStaleCancelledAndMismatchedPublicationAuthority(
        int statusValue)
    {
        DownloadVerificationContext expected = Context(OptimizationPreferenceBand.Balanced);
        DownloadedArtifactEvidence observed = ExactEvidence(expected) with
        {
            Status = (DownloadCompletionStatus)statusValue
        };
        Assert.IsFalse(CanonicalDownloadVerifier.IsExactMatch(expected, observed));
    }

    [TestMethod]
    [DataRow("operation")]
    [DataRow("generation")]
    [DataRow("publication")]
    [DataRow("inspection")]
    [DataRow("lifecycle")]
    public void CanonicalVerifierRejectsSelfConsistentEmptyAuthorityBindings(string field)
    {
        DownloadVerificationContext expected = Context(OptimizationPreferenceBand.Balanced);
        expected = field switch
        {
            "operation" => expected with { OperationId = Guid.Empty },
            "generation" => expected with { Generation = -1 },
            "publication" => expected with { PublicationIdentity = string.Empty },
            "inspection" => expected with { InspectionRequestId = Guid.Empty },
            "lifecycle" => expected with { LifecycleGeneration = -1 },
            _ => throw new InvalidOperationException(field)
        };
        DownloadedArtifactEvidence observed = ExactEvidence(expected);

        Assert.IsFalse(CanonicalDownloadVerifier.IsExactMatch(expected, observed), field);
    }

    private static DownloadVerificationContext Context(OptimizationPreferenceBand band) =>
        new(band, Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), 7,
            "publication-7", Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), 11);

    private static DownloadedArtifactEvidence ExactEvidence(DownloadVerificationContext context)
    {
        ModelDownloadCatalogEntry entry =
            PinnedGraniteModelCatalog.ForPreferenceBand(context.Band);
        return new(DownloadCompletionStatus.Completed, context.Band,
            entry.RepositoryId, entry.Revision, entry.FileName,
            entry.ExpectedByteLength, entry.ExpectedSha256, context.OperationId,
            context.Generation, context.PublicationIdentity,
            context.InspectionRequestId, context.LifecycleGeneration);
    }
}
