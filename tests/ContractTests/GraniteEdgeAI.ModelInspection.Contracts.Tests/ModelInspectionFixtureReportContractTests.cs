using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using GraniteEdgeAI.ModelInspection.Fixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.Contracts.Tests;

[TestClass]
[TestCategory("Architecture")]
[TestCategory("Contract")]
public sealed class ModelInspectionFixtureReportContractTests
{
    private const string ReportOutputPathEnvironment =
        "MODEL_INSPECTION_FIXTURE_REPORT_OUTPUT_PATH";
    private const string ReportFileName =
        "Model-Inspection-Fixture-Catalog.md";
    private const int FixtureCount = 50;
    private const int ReportLineCount = 60;
    private const string ReportTitle =
        "# Model Inspection Fixture Catalog";
    private const string ReportIntroduction =
        "This report is generated deterministically from the validated Model " +
        "Inspection fixture catalogue and separately verified external-evidence " +
        "joins.";
    private const string ReportScope =
        "All gallery rows describe synthetic data; separately verified " +
        "real-worker evidence is identified only in the final column. " +
        "Verified(Task7) means Debug/x64 loaded-tree semantic/render " +
        "coverage, not strict approved-Figma-PNG, actual OS High " +
        "Contrast/200% text-scale, or Narrator evidence. Verified(Task9) " +
        "means declared synthetic interaction/lifetime coverage, not " +
        "real-worker execution.";
    private const string Nonclaim =
        "ReadyWithWarnings, ConversionRequired, IncompletePackage, Unsupported, " +
        "Invalid, and operational-failure variants are not " +
        "real-worker-classified fixtures.";
    private const string Header =
        "| ID | Filename | Target condition | Category | Coverage tags | " +
        "Initial screen | Expected screen | Interactions | Presets | " +
        "Semantic/render | Lifetime | Provenance | Real-worker evidence |";
    private const string Separator =
        "|---|---|---|---|---|---|---|---|---|---|---|---|---|";
    private const string SyntheticProvenance =
        "synthetic deterministic fixture";
    private const string RealWorkerProvenance =
        "N-001 real-worker coverage";
    private const string SemanticRenderStatus = "Verified(Task7)";
    private const string LifetimeStatus = "Verified(Task9)";
    private const string N001Source =
        "tests/TestFixtures/GGUF/N-001-vocab-only-spm.gguf";
    private const string N001Journey =
        "PackagedN001_PageJourneyCompletesAllFiveStagesAsReady";

    [TestMethod]
    public void GeneratedCatalogueReportIsCheckedIn()
    {
        Assert.IsTrue(
            File.Exists(ReportPath()),
            "The generated fixture catalogue report is absent.");
    }

    [TestMethod]
    public void GeneratedCatalogueReportByteMatchesCheckedInUtf8LfArtifact()
    {
        byte[] generated = Generate();
        AssertUtf8LfShape(generated);

        string? requestedOutputPath = Environment.GetEnvironmentVariable(
            ReportOutputPathEnvironment);
        if (requestedOutputPath is not null)
        {
            string outputPath = RequireExternalGeneratorOutputPath(
                requestedOutputPath);
            AssertReportStructure(generated);
            string expectedSha256 = Convert.ToHexString(SHA256.HashData(generated));

            using (var stream = new FileStream(
                       outputPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None))
            {
                stream.Write(generated);
                stream.Flush(flushToDisk: true);
            }

            byte[] written = File.ReadAllBytes(outputPath);
            CollectionAssert.AreEqual(generated, written);
            Assert.AreEqual(
                expectedSha256,
                Convert.ToHexString(SHA256.HashData(written)));
            AssertUtf8LfShape(written);
            AssertReportStructure(written);
            return;
        }

        byte[] checkedIn = File.ReadAllBytes(ReportPath());
        CollectionAssert.AreEqual(checkedIn, generated);
    }

    [TestMethod]
    public void ReportHasExactHeaderAndFiftyAscendingUniqueRows()
    {
        AssertReportStructure(Generate());
    }

    [TestMethod]
    public void ReportProjectsEveryRequiredFixtureFieldWithoutInputDerivedCopy()
    {
        ValidatedModelInspectionFixtureCoverageCatalogue validated =
            ModelInspectionFixtureCatalogueContractTests
                .LoadValidatedCoverageCatalogue();
        ModelInspectionFixtureCatalogue catalogue = validated.Catalogue;
        string[][] rows = Lines(ModelInspectionFixtureReportGenerator.GenerateUtf8(
                validated,
                ModelInspectionFixtureCatalogueContractTests
                    .VerifyExternalEvidenceJoin()))[10..]
            .Select(ParseRow)
            .ToArray();

        Assert.HasCount(FixtureCount, rows);
        for (int index = 0; index < FixtureCount; index++)
        {
            ValidatedModelInspectionFixture fixture = catalogue.Fixtures[index];
            ModelInspectionFixturePolicyEntry policy =
                catalogue.Policy.Value.Fixtures[index];
            string[] row = rows[index];

            Assert.AreEqual(13, row.Length, fixture.Id);
            Assert.AreEqual(fixture.Id, row[0], fixture.Id);
            Assert.AreEqual(fixture.FileName, row[1], fixture.Id);
            Assert.AreEqual(fixture.TargetCondition, row[2], fixture.Id);
            Assert.AreEqual(fixture.Category.ToString(), row[3], fixture.Id);
            Assert.AreEqual(
                string.Join("; ", policy.RequiredCoverageTags),
                row[4],
                fixture.Id);
            Assert.AreEqual("InspectionProgress", row[5], fixture.Id);
            Assert.AreEqual(
                fixture.Expected.Figma.State.ToString(),
                row[6],
                fixture.Id);
            Assert.AreEqual(
                InteractionProjection(fixture.Interactions),
                row[7],
                fixture.Id);
            Assert.AreEqual(
                string.Join("; ", fixture.Presets),
                row[8],
                fixture.Id);
            Assert.AreEqual(SemanticRenderStatus, row[9], fixture.Id);
            Assert.AreEqual(LifetimeStatus, row[10], fixture.Id);
            Assert.AreEqual(SyntheticProvenance, row[11], fixture.Id);
            Assert.AreEqual(
                fixture.Id is "MI-002" or "MI-003"
                    ? $"{RealWorkerProvenance}; {N001Source}; {N001Journey}"
                    : "None",
                row[12],
                fixture.Id);
        }
    }

    [TestMethod]
    public void ReportTruthfullySeparatesSyntheticFixturesFromN001Evidence()
    {
        string report = Encoding.UTF8.GetString(Generate());
        string[][] rows = Lines(Encoding.UTF8.GetBytes(report))[10..]
            .Select(ParseRow)
            .ToArray();
        string withoutApprovedSource = report.Replace(
            N001Source,
            string.Empty,
            StringComparison.Ordinal);

        Assert.AreEqual(FixtureCount, Count(report, SyntheticProvenance));
        Assert.AreEqual(2, Count(report, RealWorkerProvenance));
        Assert.AreEqual(2, Count(report, N001Source));
        Assert.AreEqual(2, Count(report, N001Journey));
        Assert.AreEqual(1, Count(report, Nonclaim));
        Assert.IsFalse(report.Contains(
            "Pending(Task",
            StringComparison.Ordinal));
        Assert.IsFalse(withoutApprovedSource.Contains("tests/", StringComparison.Ordinal));
        for (int index = 1; index <= FixtureCount; index++)
        {
            string id = $"MI-{index:000}";
            string[] row = rows.Single(candidate => candidate[0] == id);
            Assert.AreEqual(
                id is "MI-002" or "MI-003" ? RealWorkerProvenance : "None",
                id is "MI-002" or "MI-003"
                    ? row[12].Split(';', StringSplitOptions.TrimEntries)[0]
                    : row[12],
                id);
        }
    }

    [TestMethod]
    public void ReportRejectsEveryNonExactExternalEvidenceJoinWithSafeDiagnostics()
    {
        ValidatedModelInspectionFixtureCoverageCatalogue catalogue =
            ModelInspectionFixtureCatalogueContractTests
                .LoadValidatedCoverageCatalogue();
        const string unsafeValue = "C:\\Users\\private\\secret.gguf";
        IReadOnlyList<IReadOnlyList<VerifiedModelInspectionFixtureExternalEvidence>>
            invalidJoins =
            [
                [],
                [new("MI-002", "N-001")],
                [new("MI-002", "N-001"), new("MI-003", "different")],
                [new("MI-002", "N-001"), new("MI-004", "N-001")],
                [new("MI-002", "N-001"), new("MI-003", "N-001"),
                    new("MI-004", "N-001")],
                [new("MI-002", "N-001"), new("MI-002", "N-001")],
                [new("MI-002", "N-001"), null!],
                [new(unsafeValue, unsafeValue), new("MI-003", "N-001")]
            ];

        foreach (IReadOnlyList<VerifiedModelInspectionFixtureExternalEvidence> join
                 in invalidJoins)
        {
            ModelInspectionFixtureValidationException exception =
                Assert.ThrowsExactly<ModelInspectionFixtureValidationException>(() =>
                    ModelInspectionFixtureReportGenerator.GenerateUtf8(
                        catalogue,
                        join));
            Assert.AreEqual(
                "model-inspection-fixture-coverage-policy.json",
                exception.FileName);
            Assert.AreEqual("$.externalEvidenceLinks", exception.JsonPath);
            Assert.AreEqual("report.external-evidence", exception.RuleCode);
            Assert.IsFalse(exception.Message.Contains(
                unsafeValue,
                StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public void ReorderedVerifiedEvidenceJoinProducesIdenticalBytes()
    {
        ValidatedModelInspectionFixtureCoverageCatalogue catalogue =
            ModelInspectionFixtureCatalogueContractTests
                .LoadValidatedCoverageCatalogue();
        byte[] canonical = ModelInspectionFixtureReportGenerator.GenerateUtf8(
            catalogue,
            [new("MI-002", "N-001"), new("MI-003", "N-001")]);
        byte[] reordered = ModelInspectionFixtureReportGenerator.GenerateUtf8(
            catalogue,
            [new("MI-003", "N-001"), new("MI-002", "N-001")]);

        CollectionAssert.AreEqual(canonical, reordered);
    }

    [TestMethod]
    public void ReportGeneratorRequiresTheCoverageValidatedBrand()
    {
        MethodInfo[] generators = typeof(ModelInspectionFixtureReportGenerator)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == nameof(
                ModelInspectionFixtureReportGenerator.GenerateUtf8))
            .ToArray();

        Assert.HasCount(1, generators);
        ParameterInfo[] parameters = generators[0].GetParameters();
        Assert.HasCount(2, parameters);
        Assert.AreEqual(
            typeof(ValidatedModelInspectionFixtureCoverageCatalogue),
            parameters[0].ParameterType);
        Assert.AreEqual(
            typeof(IReadOnlyList<VerifiedModelInspectionFixtureExternalEvidence>),
            parameters[1].ParameterType);
        Assert.HasCount(
            0,
            typeof(ValidatedModelInspectionFixtureCoverageCatalogue)
                .GetConstructors(BindingFlags.Public | BindingFlags.Instance));
    }

    [TestMethod]
    public void CanonicalCatalogueAndReportBytesAreCultureAndIdentityWordInvariant()
    {
        ValidatedModelInspectionFixtureCoverageCatalogue baselineCatalogue =
            ModelInspectionFixtureCatalogueContractTests
                .LoadValidatedCoverageCatalogue();
        byte[][] baselineSources = CanonicalSourceBytes(baselineCatalogue);
        byte[] baselineReport = ModelInspectionFixtureReportGenerator.GenerateUtf8(
            baselineCatalogue,
            ModelInspectionFixtureCatalogueContractTests
                .VerifyExternalEvidenceJoin());

        foreach (string simulatedIdentity in new[] { "model", "ready", "MI" })
        {
            Assert.IsFalse(
                ContainsExplicitIdentityMarker(simulatedIdentity),
                simulatedIdentity);

            ValidatedModelInspectionFixtureCoverageCatalogue reloaded =
                ModelInspectionFixtureCatalogueContractTests
                    .LoadValidatedCoverageCatalogue();
            byte[][] reloadedSources = CanonicalSourceBytes(reloaded);
            Assert.AreEqual(baselineSources.Length, reloadedSources.Length);
            for (int index = 0; index < baselineSources.Length; index++)
            {
                CollectionAssert.AreEqual(
                    baselineSources[index],
                    reloadedSources[index],
                    $"{simulatedIdentity}:{index}");
            }

            CollectionAssert.AreEqual(
                baselineReport,
                ModelInspectionFixtureReportGenerator.GenerateUtf8(
                    reloaded,
                    ModelInspectionFixtureCatalogueContractTests
                        .VerifyExternalEvidenceJoin()),
                simulatedIdentity);
        }

        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("tr-TR");
            byte[] turkish = Generate();

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
            byte[] french = Generate();

            CollectionAssert.AreEqual(turkish, french);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [TestMethod]
    public void ReportContainsNoPrivateRawOrInputDerivedMaterial()
    {
        ValidatedModelInspectionFixtureCoverageCatalogue catalogue =
            ModelInspectionFixtureCatalogueContractTests
                .LoadValidatedCoverageCatalogue();
        string report = Encoding.UTF8.GetString(
            ModelInspectionFixtureReportGenerator.GenerateUtf8(
                catalogue,
                ModelInspectionFixtureCatalogueContractTests
                    .VerifyExternalEvidenceJoin()));
        string withoutApprovedSource = report.Replace(
            N001Source,
            string.Empty,
            StringComparison.Ordinal);

        Assert.IsFalse(report.Contains(
            ModelInspectionFixtureCatalogueContractTests.FindRepositoryRoot(),
            StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(ContainsExplicitIdentityMarker(report));
        Assert.IsFalse(report.Contains('\\'));
        Assert.IsFalse(report.Contains("://", StringComparison.Ordinal));
        Assert.IsFalse(report.Contains("sha256", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(report.Contains("$schema", StringComparison.Ordinal));
        Assert.IsFalse(report.Contains("schemaVersion", StringComparison.Ordinal));
        Assert.IsFalse(report.Contains("copyKey", StringComparison.Ordinal));
        Assert.IsFalse(report.Contains("defaultText", StringComparison.Ordinal));
        Assert.IsFalse(report.Contains("displayName", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(report.Contains(
            "displayFileName",
            StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(report.Contains("evidenceProfile", StringComparison.Ordinal));
        Assert.IsFalse(report.Contains('{'));
        Assert.IsFalse(report.Contains('}'));
        Assert.IsFalse(report.Contains('"'));
        Assert.IsFalse(Regex.IsMatch(
            report,
            @"\b[0-9A-Fa-f]{64}\b",
            RegexOptions.CultureInvariant));
        Assert.IsFalse(Regex.IsMatch(
            report,
            @"\b\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}",
            RegexOptions.CultureInvariant));
        Assert.IsFalse(Regex.IsMatch(
            withoutApprovedSource,
            @"(?:[A-Za-z]:/|//|\b(?:tests|docs|shared)/)",
            RegexOptions.CultureInvariant));

        foreach (ValidatedModelInspectionFixture fixture in
                 catalogue.Catalogue.Fixtures)
        {
            Assert.IsFalse(report.Contains(
                fixture.Input.Request.DisplayName,
                StringComparison.Ordinal));
            Assert.IsFalse(report.Contains(
                fixture.Input.Request.DisplayFileName,
                StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public void NullArgumentsAreRejectedBeforeReportGeneration()
    {
        ValidatedModelInspectionFixtureCoverageCatalogue catalogue =
            ModelInspectionFixtureCatalogueContractTests
                .LoadValidatedCoverageCatalogue();

        Assert.ThrowsExactly<ArgumentNullException>(() =>
            ModelInspectionFixtureReportGenerator.GenerateUtf8(
                null!,
                ModelInspectionFixtureCatalogueContractTests
                    .VerifyExternalEvidenceJoin()));
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            ModelInspectionFixtureReportGenerator.GenerateUtf8(
                catalogue,
                null!));
    }

    private static byte[] Generate() =>
        ModelInspectionFixtureReportGenerator.GenerateUtf8(
            ModelInspectionFixtureCatalogueContractTests
                .LoadValidatedCoverageCatalogue(),
            ModelInspectionFixtureCatalogueContractTests
                .VerifyExternalEvidenceJoin());

    private static string ReportPath() => Path.Combine(
        ModelInspectionFixtureCatalogueContractTests.FindRepositoryRoot(),
        "docs",
        "evidence",
        "testing",
        ReportFileName);

    private static string[] Lines(byte[] utf8)
    {
        AssertUtf8LfShape(utf8);
        string text = new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true).GetString(utf8);
        return text[..^1].Split('\n');
    }

    private static void AssertUtf8LfShape(byte[] utf8)
    {
        Assert.IsFalse(utf8.AsSpan().StartsWith(Encoding.UTF8.Preamble));
        Assert.IsFalse(utf8.Contains((byte)'\r'));
        Assert.IsTrue(utf8.Length > 0);
        Assert.AreEqual((byte)'\n', utf8[^1]);
        Assert.IsTrue(utf8.Length == 1 || utf8[^2] != (byte)'\n');
        _ = new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true).GetString(utf8);
    }

    private static void AssertReportStructure(byte[] utf8)
    {
        string[] lines = Lines(utf8);
        CollectionAssert.AreEqual(
            new[]
            {
                ReportTitle,
                string.Empty,
                ReportIntroduction,
                string.Empty,
                ReportScope,
                string.Empty,
                Nonclaim,
                string.Empty,
                Header,
                Separator
            },
            lines[..10]);
        Assert.AreEqual(ReportLineCount, lines.Length);

        string[][] rows = lines[10..].Select(ParseRow).ToArray();
        Assert.HasCount(FixtureCount, rows);
        Assert.IsTrue(rows.All(row => row.Length == 13));
        string[] expectedIds = Enumerable.Range(1, FixtureCount)
            .Select(index => $"MI-{index:000}")
            .ToArray();
        CollectionAssert.AreEqual(expectedIds, rows.Select(row => row[0]).ToArray());
        Assert.AreEqual(
            FixtureCount,
            rows.Select(row => row[0]).Distinct(StringComparer.Ordinal).Count());
    }

    private static string RequireExternalGeneratorOutputPath(string requestedPath)
    {
        Assert.IsFalse(
            string.IsNullOrWhiteSpace(requestedPath),
            $"{ReportOutputPathEnvironment} cannot be blank.");
        Assert.IsTrue(
            Path.IsPathFullyQualified(requestedPath),
            "The report generator output path must be fully qualified.");

        string outputPath = Path.GetFullPath(requestedPath);
        Assert.AreEqual(
            ReportFileName,
            Path.GetFileName(outputPath),
            "The report generator output filename is not canonical.");
        string repositoryRoot = Path.GetFullPath(
                ModelInspectionFixtureCatalogueContractTests.FindRepositoryRoot())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string repositoryPrefix = repositoryRoot + Path.DirectorySeparatorChar;
        Assert.IsFalse(
            outputPath.Equals(repositoryRoot, StringComparison.OrdinalIgnoreCase) ||
            outputPath.StartsWith(
                repositoryPrefix,
                StringComparison.OrdinalIgnoreCase),
            "The report generator output path must be outside the repository.");

        string? parent = Path.GetDirectoryName(outputPath);
        Assert.IsNotNull(parent);
        Assert.IsTrue(
            Directory.Exists(parent),
            "The report generator output parent must already exist.");
        AssertPathHasNoReparsePoint(parent);
        Assert.IsFalse(
            Directory.EnumerateFileSystemEntries(parent).Any(path =>
                Path.GetFileName(path).Equals(
                    Path.GetFileName(outputPath),
                    StringComparison.OrdinalIgnoreCase)),
            "The report generator output path must be fresh.");
        return outputPath;
    }

    private static void AssertPathHasNoReparsePoint(string path)
    {
        DirectoryInfo? current = new(Path.GetFullPath(path));
        while (current is not null)
        {
            Assert.IsFalse(
                (current.Attributes & FileAttributes.ReparsePoint) != 0,
                "The report generator output path cannot traverse a reparse point.");
            current = current.Parent;
        }
    }

    private static string[] ParseRow(string line)
    {
        Assert.IsTrue(line.StartsWith("| ", StringComparison.Ordinal), line);
        Assert.IsTrue(line.EndsWith(" |", StringComparison.Ordinal), line);
        return line[2..^2].Split(" | ", StringSplitOptions.None);
    }

    private static string InteractionProjection(
        IReadOnlyList<ModelInspectionFixtureInteraction> interactions) =>
        string.Join("; ", interactions.Select(interaction =>
            $"{interaction.Id}:{interaction.Kind}:" +
            $"{interaction.SourceCheckpoint}->{interaction.Target}"));

    private static int Count(string source, string value)
    {
        int count = 0;
        int cursor = 0;
        while ((cursor = source.IndexOf(value, cursor, StringComparison.Ordinal)) >= 0)
        {
            count++;
            cursor += value.Length;
        }

        return count;
    }

    private static byte[][] CanonicalSourceBytes(
        ValidatedModelInspectionFixtureCoverageCatalogue catalogue) =>
    [
        catalogue.Catalogue.Schema.RawUtf8.ToArray(),
        catalogue.Catalogue.Policy.RawUtf8.ToArray(),
        .. catalogue.Catalogue.Fixtures.Select(fixture => fixture.RawUtf8.ToArray())
    ];

    private static bool ContainsExplicitIdentityMarker(string source)
    {
        Type? rules = typeof(ModelInspectionFixtureCatalogue).Assembly.GetType(
            "GraniteEdgeAI.ModelInspection.Fixtures.ModelInspectionFixturePrivacyRules",
            throwOnError: false,
            ignoreCase: false);
        Assert.IsNotNull(rules, "The deterministic fixture privacy rule seam is absent.");
        MethodInfo? method = rules.GetMethod(
            "ContainsExplicitIdentityMarker",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(method, "The deterministic identity-marker rule is absent.");
        return (bool)method.Invoke(null, [source])!;
    }
}
