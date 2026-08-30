namespace GraniteEdgeAI.CrossFeature.IntegrationTests;

[TestClass]
public sealed class C0ProposalAuthorityIntegrityTests
{
    private const string ProposalName = "C0-R4.1-CROSS-FEATURE-SEAMS.patch";

    private static readonly string[] ReviewedHashes =
    [
        "e60b313fc0ce2a0a2c3903f4399ac61fe1048c38f219bcbd9f7a708e168b8ead",
        "bb684177d546a2c6d3aec9cc591fc8fa75d9c16a27e798cbb2400fe52d9b7e29",
        "c698c78e895740f0e707eb7f8e92894f83f6d5b3f2f2f0b446dfe9635fa0063e",
        "69857575412143ea74d66e4d54ad70ec420d42452eadfff4a7cc043fe445ed4c",
        "a009111abf2865b7aad1e66326a6c772cddc29bccd22898f470292068b27bb59"
    ];

    [TestMethod]
    public void C0ProposalMigratesF1CatalogIntoOneNonForgeableAuthority()
    {
        string patch = File.ReadAllText(ProposalPath());
        string additions = AddedLines(patch);
        string catalogPatch = FilePatch(patch,
            "shared/GraniteEdgeAI.ModelDownload.Authority/PinnedGraniteModelCatalog.cs");
        string catalogAdditions = AddedLines(catalogPatch);

        Assert.IsFalse(additions.Contains(
                "public sealed record PinnedModelArtifact(", StringComparison.Ordinal),
            "A caller-constructible requested tuple can authorize itself.");
        Assert.IsFalse(additions.Contains(
                "public ModelDownloadCatalogEntry(", StringComparison.Ordinal),
            "Ordinary callers must not construct authoritative catalog entries.");
        StringAssert.Contains(patch,
            "a/shared/GraniteEdgeAI.ModelDownload.Authority/PinnedGraniteModelCatalog.cs");
        StringAssert.Contains(patch,
            "a/IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/PinnedGraniteModelCatalog.cs");
        StringAssert.Contains(additions,
            "shared\\GraniteEdgeAI.ModelDownload.Authority\\GraniteEdgeAI.ModelDownload.Authority.csproj");

        foreach (string hash in ReviewedHashes)
        {
            Assert.AreEqual(1, CountOrdinal(catalogAdditions, hash),
                $"Reviewed pin {hash} must have one canonical added definition.");
        }
    }

    [TestMethod]
    public void C0ProposalCarriesExecutableAdversarialAuthorityTests()
    {
        string additions = AddedLines(File.ReadAllText(ProposalPath()));
        string[] requiredTests =
        [
            "CanonicalCatalogMapsAllFivePreferenceBandsToReviewedPins",
            "CanonicalCatalogRejectsUnknownPreferenceBand",
            "CanonicalAuthorityConstructorsAreNotPublic",
            "CanonicalCatalogRejectsSelfConsistentNonCatalogTuple",
            "CanonicalVerifierRejectsEachObservedIdentityMutation",
            "CanonicalVerifierRejectsPathLikeFilename",
            "CanonicalVerifierRejectsStaleCancelledAndMismatchedPublicationAuthority"
        ];

        foreach (string requiredTest in requiredTests)
        {
            StringAssert.Contains(additions, requiredTest,
                $"The apply-built proposal must execute {requiredTest}.");
        }
        StringAssert.Contains(additions, "CanonicalDownloadVerifier.IsExactMatch(");
        StringAssert.Contains(additions, "OptimizationPreferenceBand band",
            "Selection must begin with a closed preference band.");
    }

    private static string ProposalPath()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "tests", "IntegrationTests",
                "GraniteEdgeAI.CrossFeature.IntegrationTests", ProposalName);
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException("T1 C0 proposal was not found.", ProposalName);
    }

    private static string AddedLines(string patch) => string.Join('\n',
        patch.Split('\n').Where(line => line.StartsWith('+')
            && !line.StartsWith("+++", StringComparison.Ordinal)));

    private static string FilePatch(string patch, string path)
    {
        string marker = $" b/{path}";
        int destination = patch.IndexOf(marker, StringComparison.Ordinal);
        int start = destination < 0 ? -1 : patch.LastIndexOf(
            "diff --git ", destination, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, $"Proposal does not add the expected file: {path}");
        int end = patch.IndexOf("\ndiff --git ", start + marker.Length,
            StringComparison.Ordinal);
        return end < 0 ? patch[start..] : patch[start..end];
    }

    private static int CountOrdinal(string value, string token)
    {
        int count = 0;
        int offset = 0;
        while ((offset = value.IndexOf(token, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += token.Length;
        }
        return count;
    }
}
