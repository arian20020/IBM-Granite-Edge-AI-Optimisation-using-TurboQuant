#if MODEL_INSPECTION_FIXTURE_GALLERY
using GraniteEdgeAI.ModelInspection.Fixtures;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Gallery;

internal sealed class ModelInspectionFixtureGalleryLoadException : Exception
{
    internal ModelInspectionFixtureGalleryLoadException(
        string diagnostic,
        Exception? innerException = null)
        : base("The packaged fixture catalogue is unavailable.", innerException)
    {
        Diagnostic = diagnostic;
    }

    internal string Diagnostic { get; }
}

internal sealed class ModelInspectionFixturePackageResourceReader :
    IModelInspectionFixturePackageResourceReader
{
    internal const string PackageRoot =
        "ms-appx:///Fixtures/";
    internal const int MaximumDocumentBytes = 256 * 1024;

    private static readonly HashSet<string> AllowedUris = new(
        ModelInspectionFixturePackageLoader.AllFileNames.Select(
            fileName => PackageRoot + fileName),
        StringComparer.Ordinal);

    public async Task<ReadOnlyMemory<byte>> ReadAsync(
        Uri packageUri,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packageUri);
        string original = packageUri.OriginalString;
        if (!AllowedUris.Contains(original))
        {
            throw new InvalidOperationException(
                "The fixture package URI is not canonical or allowlisted.");
        }

        byte[] bytes = (await FileIO.ReadBufferAsync(
                    await StorageFile
                        .GetFileFromApplicationUriAsync(packageUri)
                        .AsTask(cancellationToken))
                .AsTask(cancellationToken))
            .ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        if (bytes.Length is 0 or > MaximumDocumentBytes)
        {
            throw new InvalidOperationException(
                "The fixture package resource length is outside its boundary.");
        }

        return bytes;
    }
}

internal sealed class ModelInspectionFixturePackageLoader
{
    internal const string SchemaFileName =
        "model-inspection-fixture.schema.json";
    internal const string PolicyFileName =
        "model-inspection-fixture-coverage-policy.json";

    private readonly IModelInspectionFixturePackageResourceReader reader;
    private readonly Action beforeCoveragePublication;
    private readonly SemaphoreSlim loadGate = new(1, 1);
    private ValidatedModelInspectionFixtureCoverageCatalogue?
        coverageCatalogue;

    internal ModelInspectionFixturePackageLoader()
        : this(new ModelInspectionFixturePackageResourceReader())
    {
    }

    internal ModelInspectionFixturePackageLoader(
        IModelInspectionFixturePackageResourceReader reader)
        : this(reader, static () => { })
    {
    }

    internal ModelInspectionFixturePackageLoader(
        IModelInspectionFixturePackageResourceReader reader,
        Action beforeCoveragePublication)
    {
        this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
        this.beforeCoveragePublication = beforeCoveragePublication ??
            throw new ArgumentNullException(nameof(beforeCoveragePublication));
    }

    internal ValidatedModelInspectionFixtureCoverageCatalogue?
        CoverageCatalogue => Volatile.Read(ref coverageCatalogue);

    internal async Task<ValidatedModelInspectionFixtureCoverageCatalogue>
        LoadAsync(CancellationToken cancellationToken = default)
    {
        ValidatedModelInspectionFixtureCoverageCatalogue? current =
            CoverageCatalogue;
        if (current is not null)
        {
            return current;
        }

        await loadGate.WaitAsync(cancellationToken);
        try
        {
            current = CoverageCatalogue;
            if (current is not null)
            {
                return current;
            }

            try
            {
                ModelInspectionFixtureDocumentSource schemaSource =
                    await ReadSourceAsync(
                        SchemaFileName,
                        cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                VerifiedModelInspectionFixtureSchema schema =
                    ModelInspectionFixtureCatalogue.VerifySchema(schemaSource);

                ModelInspectionFixtureDocumentSource policySource =
                    await ReadSourceAsync(
                        PolicyFileName,
                        cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                ValidatedModelInspectionFixtureCoveragePolicy policy =
                    ModelInspectionFixtureCatalogue.LoadPolicy(
                        policySource,
                        schema);
                RequireExactPolicyManifest(policy);

                var descriptors = new List<
                    ModelInspectionFixtureDocumentSource>(
                    DescriptorFileNames.Count);
                foreach (string fileName in policy.Value.Fixtures.Select(
                             entry => entry.FileName))
                {
                    ModelInspectionFixtureDocumentSource descriptor =
                        await ReadSourceAsync(
                        fileName,
                        cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    descriptors.Add(descriptor);
                }

                ModelInspectionFixtureCatalogue catalogue =
                    ModelInspectionFixtureCatalogue.LoadDescriptors(
                        descriptors,
                        policy,
                        schema);
                ValidatedModelInspectionFixtureCoverageCatalogue validated =
                    ModelInspectionFixtureCoverageValidator.Validate(catalogue);
                cancellationToken.ThrowIfCancellationRequested();
                beforeCoveragePublication();
                cancellationToken.ThrowIfCancellationRequested();
                Volatile.Write(ref coverageCatalogue, validated);
                return validated;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ModelInspectionFixtureGalleryLoadException)
            {
                throw;
            }
            catch (ModelInspectionFixtureValidationException error)
            {
                throw new ModelInspectionFixtureGalleryLoadException(
                    $"{error.FileName}|{error.JsonPath}|{error.RuleCode}",
                    error);
            }
            catch (Exception error)
            {
                throw new ModelInspectionFixtureGalleryLoadException(
                    $"{PolicyFileName}|$|catalogue.unavailable",
                    error);
            }
        }
        finally
        {
            loadGate.Release();
        }
    }

    internal ValidatedModelInspectionFixture RevalidateDescriptor(
        string fileName,
        ReadOnlyMemory<byte> rawUtf8)
    {
        ValidatedModelInspectionFixtureCoverageCatalogue loaded =
            CoverageCatalogue ?? throw new InvalidOperationException(
                "The fixture catalogue has not been validated.");
        if (!DescriptorFileNames.Contains(fileName, StringComparer.Ordinal))
        {
            throw new ModelInspectionFixtureGalleryLoadException(
                "<invalid-filename>|$|revalidation.boundary");
        }

        if (rawUtf8.Length is 0 or >
            ModelInspectionFixturePackageResourceReader.MaximumDocumentBytes)
        {
            throw new ModelInspectionFixtureGalleryLoadException(
                $"{fileName}|$|revalidation.boundary");
        }

        try
        {
            ModelInspectionFixtureCatalogue catalogue = loaded.Catalogue;
            return ModelInspectionFixtureCatalogue.RevalidateDescriptor(
                new ModelInspectionFixtureDocumentSource(fileName, rawUtf8),
                catalogue.Policy,
                catalogue.Schema,
                catalogue.Index);
        }
        catch (ModelInspectionFixtureValidationException error)
        {
            throw new ModelInspectionFixtureGalleryLoadException(
                $"{error.FileName}|{error.JsonPath}|{error.RuleCode}",
                error);
        }
    }

    private async Task<ModelInspectionFixtureDocumentSource> ReadSourceAsync(
        string fileName,
        CancellationToken cancellationToken)
    {
        ReadOnlyMemory<byte> bytes;
        try
        {
            bytes = await reader.ReadAsync(
                new Uri(
                    ModelInspectionFixturePackageResourceReader.PackageRoot +
                    fileName,
                    UriKind.Absolute),
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception error)
        {
            throw new ModelInspectionFixtureGalleryLoadException(
                $"{fileName}|$|package.read",
                error);
        }
        if (bytes.Length is 0 or >
            ModelInspectionFixturePackageResourceReader.MaximumDocumentBytes)
        {
            throw new ModelInspectionFixtureGalleryLoadException(
                $"{fileName}|$|package.length");
        }

        return new ModelInspectionFixtureDocumentSource(
            fileName,
            bytes.ToArray());
    }

    private static void RequireExactPolicyManifest(
        ValidatedModelInspectionFixtureCoveragePolicy policy)
    {
        string[] policyFiles = policy.Value.Fixtures
            .Select(entry => entry.FileName)
            .ToArray();
        if (!policyFiles.SequenceEqual(
                DescriptorFileNames,
                StringComparer.Ordinal))
        {
            throw new ModelInspectionFixtureGalleryLoadException(
                $"{PolicyFileName}|$.fixtures|package.manifest");
        }
    }

    private static readonly ImmutableArray<string> DescriptorFileNamesValue =
        ImmutableArray.Create(
        "MI-001-inspection-progress-initial.fixture.json",
        "MI-002-ready-clean-compatible-model-collapsed.fixture.json",
        "MI-003-ready-clean-compatible-model-expanded.fixture.json",
        "MI-004-ready-with-warnings-chat-template-missing-collapsed.fixture.json",
        "MI-005-ready-with-warnings-chat-template-missing-expanded.fixture.json",
        "MI-006-conversion-required-verified-incompatible-route-collapsed.fixture.json",
        "MI-007-conversion-required-verified-incompatible-route-expanded.fixture.json",
        "MI-008-incomplete-package-missing-package-member.fixture.json",
        "MI-009-unsupported-model-architecture.fixture.json",
        "MI-010-invalid-cross-source-evidence-contradiction-collapsed.fixture.json",
        "MI-011-invalid-cross-source-evidence-contradiction-expanded.fixture.json",
        "MI-012-cancelled-cooperative-user-cancellation.fixture.json",
        "MI-013-operational-failure-worker-start-failure.fixture.json",
        "MI-014-progress-check-model-package-active-fractionless.fixture.json",
        "MI-015-progress-check-model-package-completed.fixture.json",
        "MI-016-progress-read-model-configuration-active.fixture.json",
        "MI-017-progress-read-model-configuration-completed.fixture.json",
        "MI-018-progress-validate-tokenizer-chat-setup-active.fixture.json",
        "MI-019-progress-validate-tokenizer-chat-setup-completed.fixture.json",
        "MI-020-progress-validate-model-structure-active.fixture.json",
        "MI-021-progress-validate-model-structure-completed.fixture.json",
        "MI-022-progress-confirm-runtime-compatibility-active.fixture.json",
        "MI-023-progress-confirm-runtime-compatibility-completed.fixture.json",
        "MI-024-progress-cancel-requested.fixture.json",
        "MI-025-progress-chat-setup-warning.fixture.json",
        "MI-026-progress-model-structure-failed.fixture.json",
        "MI-027-progress-runtime-compatibility-cancelled.fixture.json",
        "MI-028-progress-read-model-configuration-active-bounded-fraction.fixture.json",
        "MI-029-cancellation-requested-cooperative-cancelled.fixture.json",
        "MI-030-cancellation-forced-operational-failure.fixture.json",
        "MI-031-retry-after-cancellation.fixture.json",
        "MI-032-retry-after-operational-failure.fixture.json",
        "MI-033-retry-stale-progress-rejected.fixture.json",
        "MI-034-retry-stale-result-rejected.fixture.json",
        "MI-035-retry-stale-motion-completion-rejected.fixture.json",
        "MI-036-retry-stale-announcement-rejected.fixture.json",
        "MI-037-choose-another-page-retired.fixture.json",
        "MI-038-gallery-switch-old-session-retired.fixture.json",
        "MI-039-operational-failure-worker-timeout.fixture.json",
        "MI-040-operational-failure-worker-crash-early-exit.fixture.json",
        "MI-041-operational-failure-malformed-worker-response.fixture.json",
        "MI-042-operational-failure-cancellation-unconfirmed.fixture.json",
        "MI-043-ready-model-name-maximum-collapsed.fixture.json",
        "MI-044-ready-missing-optional-metadata-not-reported-collapsed.fixture.json",
        "MI-045-ready-check-rows-current-maximum-expanded.fixture.json",
        "MI-046-ready-with-warnings-finding-rows-current-maximum-expanded.fixture.json",
        "MI-047-invalid-report-rows-current-maximum-expanded.fixture.json",
        "MI-048-progress-detail-copy-maximum.fixture.json",
        "MI-049-operational-failure-detail-copy-maximum.fixture.json",
        "MI-050-progress-starting-secure-inspection.fixture.json");

    private static readonly ImmutableArray<string> AllFileNamesValue =
        [SchemaFileName, PolicyFileName, .. DescriptorFileNamesValue];

    internal static IReadOnlyList<string> DescriptorFileNames =>
        DescriptorFileNamesValue;

    internal static IReadOnlyList<string> AllFileNames => AllFileNamesValue;
}
#endif
