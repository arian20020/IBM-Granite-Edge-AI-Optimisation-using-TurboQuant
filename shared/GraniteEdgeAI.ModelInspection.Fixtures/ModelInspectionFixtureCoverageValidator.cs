using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace GraniteEdgeAI.ModelInspection.Fixtures;

public sealed class ValidatedModelInspectionFixtureCoverageCatalogue
{
    internal ValidatedModelInspectionFixtureCoverageCatalogue(
        ModelInspectionFixtureCatalogue catalogue)
    {
        Catalogue = catalogue;
    }

    public ModelInspectionFixtureCatalogue Catalogue { get; }
}

public static class ModelInspectionFixtureCoverageValidator
{
    private const string AuthoritativeSchemaSemanticSha256 =
        "98259F0485986FC474A188479329FBF28C62CA4DD054152CCDF4AA70B1336444";
    private const string AuthoritativeCopyRegistrySha256 =
        "21CB6FFC2902B522378F5D94FEB330C389A8A066CA596C82DF786D988FEEC16F";

    private const string ExpectedCoverageTagText = """
        figma.inspection-progress,progress.initial
        figma.ready-collapsed,outcome.ready
        figma.ready-expanded,outcome.ready
        figma.ready-with-warnings-collapsed,outcome.ready-with-warnings
        figma.ready-with-warnings-expanded,outcome.ready-with-warnings
        figma.conversion-required-collapsed,outcome.conversion-required
        figma.conversion-required-expanded,outcome.conversion-required
        figma.incomplete-package,outcome.incomplete-package
        figma.unsupported,outcome.unsupported
        figma.invalid-collapsed,outcome.invalid
        figma.invalid-expanded,outcome.invalid
        figma.cancelled,terminal.cancelled,lifecycle.cancellation-requested,lifecycle.cooperative-cancellation
        figma.operational-failure,failure.worker-start-failure
        figma.inspection-progress,progress.check-model-package.active
        figma.inspection-progress,progress.check-model-package.completed
        figma.inspection-progress,progress.read-model-configuration.active
        figma.inspection-progress,progress.read-model-configuration.completed
        figma.inspection-progress,progress.validate-tokenizer-and-chat-setup.active
        figma.inspection-progress,progress.validate-tokenizer-and-chat-setup.completed
        figma.inspection-progress,progress.validate-model-structure.active
        figma.inspection-progress,progress.validate-model-structure.completed
        figma.inspection-progress,progress.confirm-core-runtime-compatibility.active
        figma.inspection-progress,progress.confirm-core-runtime-compatibility.completed
        figma.inspection-progress,progress.check-model-package.active,lifecycle.cancellation-requested
        figma.inspection-progress,progress.validate-tokenizer-and-chat-setup.warning,outcome.ready-with-warnings
        figma.inspection-progress,progress.validate-model-structure.failed,outcome.invalid
        figma.inspection-progress,progress.confirm-core-runtime-compatibility.cancelled,terminal.cancelled
        figma.inspection-progress,progress.read-model-configuration.active,progress.bounded-fraction
        figma.cancelled,terminal.cancelled,lifecycle.cancellation-requested,lifecycle.cooperative-cancellation
        figma.operational-failure,failure.cancellation-unconfirmed,lifecycle.cancellation-requested,lifecycle.forced-cancellation
        figma.ready-collapsed,outcome.ready,lifecycle.retry-after-cancellation
        figma.ready-collapsed,outcome.ready,failure.worker-start-failure,lifecycle.retry-after-operational-failure
        figma.ready-collapsed,outcome.ready,failure.worker-timeout,lifecycle.stale-progress-rejected
        figma.ready-collapsed,outcome.ready,failure.worker-start-failure,lifecycle.stale-result-rejected
        figma.ready-collapsed,outcome.ready,failure.worker-start-failure,lifecycle.stale-motion-rejected
        figma.ready-collapsed,outcome.ready,failure.worker-start-failure,lifecycle.stale-announcement-rejected
        figma.ready-collapsed,outcome.ready,lifecycle.choose-another-retired
        figma.ready-collapsed,outcome.ready,lifecycle.gallery-switch-retired
        figma.operational-failure,failure.worker-timeout
        figma.operational-failure,failure.worker-crash-early-exit
        figma.operational-failure,failure.malformed-worker-response
        figma.operational-failure,failure.cancellation-unconfirmed
        figma.ready-collapsed,outcome.ready,stress.maximum-model-name
        figma.ready-collapsed,outcome.ready,stress.missing-optional-metadata
        figma.ready-expanded,outcome.ready,stress.maximum-check-rows
        figma.ready-with-warnings-expanded,outcome.ready-with-warnings,stress.maximum-finding-rows
        figma.invalid-expanded,outcome.invalid,stress.maximum-report-rows
        figma.inspection-progress,progress.read-model-configuration.active,stress.maximum-detail-copy
        figma.operational-failure,failure.worker-start-failure,stress.maximum-detail-copy
        """;

    private const string ExpectedDescriptorSemanticHashText = """
        FA972B7878ED58D13A73D1723F555EE66C9B1FD8F69F614FA5153BCEDED01958
        CFC396535F5E7B6B1CB033E52B0DF2FDB6515002527C3D3E90153B46799B634F
        4C84B2E3945FE4B2003560E98CA33874AF17749682EB2D12780378785A0682D3
        A2046B39C66238C9EDB11B202F7E63A814CA05E1FD89EF82FC7B647773981129
        082A4AD81DD4030CC1E897F8A6766D4DC7B102C1F21B6D90E771D1C2674D27CC
        F1F1642431B6911E9A13FC210970DED2152546DF527380C0461EEFD80E465F75
        FD2E7FBE196EB398F8EE3576FF404C5DA56910473C73BFC55783A51B1DE40EB1
        D2A14246C618DB131E3CEEE18E3AE95F831AAB367D94F379BCFCF6A794767FB3
        F53AB3C54EE0D607D7FA3A84A24E17D5F97A50D0BC554019F5835E3B270AB524
        C7D4CBC521B8EF5349D6217A0B37EBB886D111E8970138F13FA427B6709DEAFE
        D09674F15D29147B5CC45B18A367888FB43E3891CB88A41DFC1FAD8F0E671CE7
        D63BA8C685EAEF94E9A9422748C31F204E87F60C521723BDB211DCE9CD54B038
        77AE8075770A6DD932AA4EC824AEA5CA4921E7E68B7EC35A8C4BE1AEF1BBDC8B
        F45FA147BE25C89CED81B4A9A0A452772DECA8BD58BD26C5DA355DBA7597AFAA
        1D1D3D087ECBFEDFB0DC80702055EFF6FC7EC6A4B477631B0457D05333AE9FC1
        032B3E1EAFC2B3AB3EC20E4DC4A5EE68137F6F7EF18834765FBDFF4C7C509A9D
        CD1A5AFA258EF76F3E1887BEB16D9E6C413360BE37F76F302F2DC9B63A41D857
        640119AB0B0E20F5D4C6E6D8B1FA5413E29674104EA0901F4F6E11F06D4CCF37
        FFD2E127F683CADC79D123413FA0C05AD8FB509906AE3DD9D61C58543511963C
        828F139BE5A17EAE2377ADA33ECD9A7487372ECA7CF7933116AA377765EB8757
        27D88A9D5CB49FAB11B334D973357B2E9252AA0FDD25762552B1F4CC82DF1C1C
        527CE8346A6672A7C21806BFF6527E301BE7C8EACCA1B8BBA7B4ADEC83F533FA
        1C5ADCDF0485DE8F907440AAF6B258DC5C5C7CF3661C569221F2A76CE4A73D30
        44BD65E7B499DF9935EED05381D7118B2DBA9DD675A7BA973D308015CA38556A
        C587BA56B9699A3810B00F03DB826B74A0D605CB311CE03FE4995869EDBB99F7
        1DCB489A6CA682D9E753D2C264EA19F4F898D0B52763574AF630EA93A623B6FC
        C4D15FEFFB0746A33EB0AC557395C1802E0186D95787CBECF8FD49E445E2608E
        F757C038E3FD385E201E293599F3E8B85F19B99F04A90227534134DBC87DC73D
        B20F3DE7E908713EC20F33650157456B5A2B9D49647E42765EBD637B2FBCB2F8
        CA987D00FBC979DDFC550EAB7D39CB15F7B4F1EF67353C211A6A6CE2496E92DC
        F6035E00791E19AEC70C1067C7759D1AE0F0F9DE378770403A8754EAC0715E13
        F770C33D174500FD66623EA092C0BFAF3E02487B7CC2C3645DFDA049A18A2B6C
        C5D4BF6D0941C1650135473AFEF43C2D68A8F5AD68C806F22795102311FEF7D1
        FA5B32D3A71C0D535AA2186081E878ED3B437B2FDCD0BC17DB29D8698B7FC160
        3C1D34D1FACC46B93B31BEA1F79F8F0C3655E4E82BCFE38FFDB5171E6A91CD3E
        446B656535D8867D603F1DBC86D64D98AA825AAE2C42243552953DA7BC894582
        037F8AF52B5E51EB471B429A38CE86C13964699362ACBEAD877DA1CC869F49FA
        65AF92F7410FDB5028032440FCB56D89D707936EF2FB943138FD7B4811214DDA
        052FD663D8228EC9C85A91D1C19496AF77AB2A9E4752208F3568A0F2D0B8882B
        4CE507F84CAD2A7BD13A64F9A214A5CBDBA4C65EFE86325279A171420A89B7FB
        A095EE02FF011D07D2AB2344F609D8DF498851D154C21757B81436BD20028CE4
        0005D1162B1AEE606707A2C26A650731C2E5DB33AC4A9EB6B6DF08C995E08D51
        012B823E534DDD35F23F242A9E40102558358F4420760AC6B23891F00160CEDE
        223DF8AC361A7AF0E62693FE907BB14F4B2231C4DD0C2E352515A4801DA7E705
        1929B9A09A7C1E6DA63C7AEC339E91D57607D4C2405244EBABDF8F527D64EF3D
        1AC14E6BA47EAA077B61F51CD766AFFDB80DA55B90F1DB4A3248D5B345E7BDCA
        BD6365F527724F5C84003A26F22365B277B0A05D6B74E1BF7CAFC2C17E761A63
        134C9D5D725300169558DC90782BB5809B9B80E066B24AD488071D0B745967B6
        532864901AF3B1D6996AA8802E989E28BFDC01B234D641CEEF14E4199AF31F61
        """;

    private static readonly ImmutableArray<string> ManifestFileNames =
    [
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
        "MI-049-operational-failure-detail-copy-maximum.fixture.json"
    ];

    private static readonly ImmutableArray<ImmutableArray<string>>
        ExpectedCoverageTags = ExpectedCoverageTagText
            .Split('\n', StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries)
            .Select(line => line.Split(',', StringSplitOptions.TrimEntries)
                .ToImmutableArray())
            .ToImmutableArray();

    private static readonly ImmutableArray<string> ExpectedDescriptorSemanticHashes =
        ExpectedDescriptorSemanticHashText
            .Split('\n', StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries)
            .ToImmutableArray();

    public static IReadOnlyList<string> GetMissingFixtureIds(
        IEnumerable<string> availableFileNames)
    {
        ArgumentNullException.ThrowIfNull(availableFileNames);
        HashSet<string> available = availableFileNames.ToHashSet(StringComparer.Ordinal);
        return ManifestFileNames
            .Where(fileName => !available.Contains(fileName))
            .Select(fileName => fileName[..6])
            .ToImmutableArray();
    }

    public static void ValidateSchemaContract(
        VerifiedModelInspectionFixtureSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        try
        {
            using JsonDocument document = JsonDocument.Parse(schema.RawUtf8.AsMemory());
            if (!SemanticHash(document.RootElement).Equals(
                    AuthoritativeSchemaSemanticSha256,
                    StringComparison.Ordinal))
            {
                throw SchemaFailure();
            }
        }
        catch (JsonException)
        {
            throw SchemaFailure();
        }
    }

    public static ValidatedModelInspectionFixtureCoverageCatalogue Validate(
        ModelInspectionFixtureCatalogue catalogue)
    {
        ArgumentNullException.ThrowIfNull(catalogue);
        ValidateSchemaContract(catalogue.Schema);
        string[] actual = catalogue.Fixtures
            .Select(fixture => fixture.FileName)
            .ToArray();
        if (!ManifestFileNames.SequenceEqual(actual, StringComparer.Ordinal))
        {
            throw CoverageFailure("$", "coverage.manifest");
        }


        ModelInspectionFixtureCoveragePolicy policy = catalogue.Policy.Value;
        if (policy.Fixtures.Count != ManifestFileNames.Length ||
            ExpectedCoverageTags.Length != ManifestFileNames.Length)
        {
            throw CoverageFailure("$.fixtures", "coverage.manifest");
        }

        for (int index = 0; index < ManifestFileNames.Length; index++)
        {
            ValidateFixtureOracle(catalogue, index);
        }

        ValidateAuthoritativePresetMatrix(policy);
        ValidatePolicyPairsAndExternalEvidence(policy);

        foreach (ValidatedModelInspectionFixture fixture in catalogue.Fixtures)
        {
            ReplayTrace replay = ReplayReleasedPrefix(fixture);
            ValidateReplayCoverageBinding(fixture, replay);
            ValidateLifecycleAndStressBinding(fixture, replay);
            ValidateInteractionTransitions(fixture);
            ValidatePresetExpectations(fixture, policy, replay);
            ValidateUiIdentityClosure(fixture);
        }

        ValidateDisclosurePairs(catalogue);
        ValidateCopyRegistryClosure(catalogue);
        ValidateDescriptorSemanticFingerprints(catalogue);

        return new ValidatedModelInspectionFixtureCoverageCatalogue(catalogue);
    }

    private static void ValidateDescriptorSemanticFingerprints(
        ModelInspectionFixtureCatalogue catalogue)
    {
        if (ExpectedDescriptorSemanticHashes.Length != ManifestFileNames.Length)
        {
            throw CoverageFailure("$.fixtures", "coverage.semantic-binding");
        }

        for (int index = 0; index < catalogue.Fixtures.Count; index++)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(
                    catalogue.Fixtures[index].RawUtf8.AsMemory());
                if (!SemanticHash(document.RootElement).Equals(
                        ExpectedDescriptorSemanticHashes[index],
                        StringComparison.Ordinal))
                {
                    throw CoverageFailure(
                        "$.fixtures",
                        "coverage.semantic-binding");
                }
            }
            catch (JsonException)
            {
                throw CoverageFailure(
                    "$.fixtures",
                    "coverage.semantic-binding");
            }
        }
    }

    private static void ValidateFixtureOracle(
        ModelInspectionFixtureCatalogue catalogue,
        int index)
    {
        int ordinal = index + 1;
        string expectedFileName = ManifestFileNames[index];
        string expectedId = expectedFileName[..6];
        string? expectedVariant = ExpectedVariant(ordinal);
        string expectedTarget = ExpectedTargetCondition(
            expectedFileName,
            expectedVariant);
        string? expectedPair = ExpectedPairedId(ordinal);
        ModelInspectionFixtureCategory expectedCategory = ExpectedCategory(ordinal);
        ModelInspectionFixtureFigmaState expectedState = ExpectedFigmaState(
            ExpectedCoverageTags[index][0]);
        ModelInspectionFixtureEvidenceProfile expectedEvidence =
            ExpectedEvidenceProfile(ordinal);
        ImmutableArray<ModelInspectionFixtureInteractionKind> expectedInteractions =
            ExpectedInteractionKinds(ordinal);
        ImmutableArray<string> expectedPresets = ExpectedPresetIds(ordinal);
        ModelInspectionFixturePolicyEntry policy = catalogue.Policy.Value.Fixtures[index];
        ValidatedModelInspectionFixture fixture = catalogue.Fixtures[index];

        if (!policy.Id.Equals(expectedId, StringComparison.Ordinal) ||
            !policy.FileName.Equals(expectedFileName, StringComparison.Ordinal) ||
            !policy.TargetCondition.Equals(expectedTarget, StringComparison.Ordinal) ||
            !EqualNullable(policy.Variant, expectedVariant) ||
            !EqualNullable(policy.PairedWithId, expectedPair) ||
            !fixture.Id.Equals(expectedId, StringComparison.Ordinal) ||
            !fixture.FileName.Equals(expectedFileName, StringComparison.Ordinal) ||
            !fixture.TargetCondition.Equals(expectedTarget, StringComparison.Ordinal) ||
            !EqualNullable(fixture.Variant, expectedVariant) ||
            !fixture.Schema.Equals(
                "model-inspection-fixture.schema.json",
                StringComparison.Ordinal) ||
            fixture.SchemaVersion != 1)
        {
            throw CoverageFailure("$.fixtures", "coverage.manifest");
        }

        if (fixture.Category != expectedCategory ||
            policy.CanonicalFigmaState != expectedState ||
            fixture.Input.Request.EvidenceProfile != expectedEvidence ||
            !fixture.Coverage.FigmaStates.SequenceEqual([expectedState]) ||
            fixture.Expected.Figma.State != ExpectedScreenState(expectedState) ||
            !policy.RequiredCoverageTags.SequenceEqual(
                ExpectedCoverageTags[index],
                StringComparer.Ordinal) ||
            !fixture.Coverage.Outcomes.SequenceEqual(
                ExpectedOutcomes(ExpectedCoverageTags[index])) ||
            !fixture.Coverage.LifecycleTags.SequenceEqual(
                ExpectedLifecycleTags(ExpectedCoverageTags[index])) ||
            !fixture.Coverage.FailureProfiles.SequenceEqual(
                ExpectedFailureProfiles(ExpectedCoverageTags[index])) ||
            !fixture.Coverage.StressTags.SequenceEqual(
                ExpectedStressTags(ExpectedCoverageTags[index])))
        {
            throw CoverageFailure("$.fixtures", "coverage.semantic-binding");
        }

        if (!policy.RequiredInteractions.SequenceEqual(expectedInteractions) ||
            !fixture.Coverage.Interactions.SequenceEqual(expectedInteractions))
        {
            throw CoverageFailure("$.fixtures", "coverage.interaction-kinds");
        }

        HashSet<ModelInspectionFixtureInteractionKind> declaredKinds = fixture.Interactions
            .Select(interaction => interaction.Kind)
            .ToHashSet();
        if (declaredKinds.Count != expectedInteractions.Length ||
            !declaredKinds.SetEquals(expectedInteractions))
        {
            throw CoverageFailure("$.fixtures", "coverage.interaction-kinds");
        }

        if (!policy.RequiredPresets.SequenceEqual(
                expectedPresets,
                StringComparer.Ordinal) ||
            !fixture.Presets.SequenceEqual(expectedPresets, StringComparer.Ordinal))
        {
            throw CoverageFailure("$.presets", "coverage.preset-matrix");
        }
    }

    private static bool EqualNullable(string? actual, string? expected) =>
        string.Equals(actual, expected, StringComparison.Ordinal);

    private static string ExpectedTargetCondition(
        string fileName,
        string? variant)
    {
        const string suffix = ".fixture.json";
        string target = fileName[7..^suffix.Length];
        return variant is null
            ? target
            : target[..^(variant.Length + 1)];
    }

    private static string? ExpectedVariant(int ordinal) => ordinal switch
    {
        2 or 4 or 6 or 10 or 43 or 44 => "collapsed",
        3 or 5 or 7 or 11 or 45 or 46 or 47 => "expanded",
        _ => null
    };

    private static string? ExpectedPairedId(int ordinal) => ordinal switch
    {
        2 => "MI-003",
        3 => "MI-002",
        4 => "MI-005",
        5 => "MI-004",
        6 => "MI-007",
        7 => "MI-006",
        10 => "MI-011",
        11 => "MI-010",
        _ => null
    };

    private static ModelInspectionFixtureCategory ExpectedCategory(int ordinal) =>
        ordinal switch
        {
            <= 13 => ModelInspectionFixtureCategory.Screen,
            <= 28 => ModelInspectionFixtureCategory.Progress,
            <= 38 => ModelInspectionFixtureCategory.Lifecycle,
            <= 42 => ModelInspectionFixtureCategory.Failure,
            _ => ModelInspectionFixtureCategory.Stress
        };

    private static ModelInspectionFixtureEvidenceProfile ExpectedEvidenceProfile(
        int ordinal) => ordinal switch
        {
            4 or 5 or 25 or 46 =>
                ModelInspectionFixtureEvidenceProfile.MissingChatTemplate,
            6 or 7 => ModelInspectionFixtureEvidenceProfile.VerifiedIncompatible,
            8 => ModelInspectionFixtureEvidenceProfile.MissingPackageMember,
            9 => ModelInspectionFixtureEvidenceProfile.UnsupportedArchitecture,
            10 or 11 or 26 or 47 =>
                ModelInspectionFixtureEvidenceProfile.CrossSourceContradiction,
            44 => ModelInspectionFixtureEvidenceProfile
                .CompatibleMissingOptionalMetadata,
            _ => ModelInspectionFixtureEvidenceProfile.Compatible
        };

    private static ImmutableArray<ModelInspectionFixtureInteractionKind>
        ExpectedInteractionKinds(int ordinal) => ordinal switch
        {
            1 => [ModelInspectionFixtureInteractionKind.Reset],
            2 or 4 or 6 or 10 or 37 or 38 or 43 or 44 =>
            [
                ModelInspectionFixtureInteractionKind.Expand,
                ModelInspectionFixtureInteractionKind.ChooseAnother,
                ModelInspectionFixtureInteractionKind.Reset
            ],
            3 or 5 or 7 or 11 or 45 or 46 or 47 =>
            [
                ModelInspectionFixtureInteractionKind.Expand,
                ModelInspectionFixtureInteractionKind.Collapse,
                ModelInspectionFixtureInteractionKind.ChooseAnother,
                ModelInspectionFixtureInteractionKind.Reset
            ],
            8 or 9 =>
            [
                ModelInspectionFixtureInteractionKind.ChooseAnother,
                ModelInspectionFixtureInteractionKind.Reset
            ],
            12 or 29 =>
            [
                ModelInspectionFixtureInteractionKind.Cancel,
                ModelInspectionFixtureInteractionKind.Restart,
                ModelInspectionFixtureInteractionKind.ChooseAnother,
                ModelInspectionFixtureInteractionKind.Reset
            ],
            13 or >= 39 and <= 42 or 49 =>
            [
                ModelInspectionFixtureInteractionKind.Retry,
                ModelInspectionFixtureInteractionKind.ChooseAnother,
                ModelInspectionFixtureInteractionKind.Reset
            ],
            >= 14 and <= 28 or 48 =>
            [
                ModelInspectionFixtureInteractionKind.Cancel,
                ModelInspectionFixtureInteractionKind.Reset
            ],
            30 =>
            [
                ModelInspectionFixtureInteractionKind.Cancel,
                ModelInspectionFixtureInteractionKind.Retry,
                ModelInspectionFixtureInteractionKind.ChooseAnother,
                ModelInspectionFixtureInteractionKind.Reset
            ],
            31 =>
            [
                ModelInspectionFixtureInteractionKind.Restart,
                ModelInspectionFixtureInteractionKind.Expand,
                ModelInspectionFixtureInteractionKind.ChooseAnother,
                ModelInspectionFixtureInteractionKind.Reset
            ],
            >= 32 and <= 36 =>
            [
                ModelInspectionFixtureInteractionKind.Retry,
                ModelInspectionFixtureInteractionKind.Expand,
                ModelInspectionFixtureInteractionKind.ChooseAnother,
                ModelInspectionFixtureInteractionKind.Reset
            ],
            _ => throw new InvalidOperationException("Invalid authoritative ordinal.")
        };

    private static ImmutableArray<string> ExpectedPresetIds(int ordinal)
    {
        if (ordinal == 3)
        {
            return ["P01", "P09"];
        }

        return ordinal is >= 43 and <= 49
            ? ["P01", $"P{ordinal - 41:00}"]
            : ["P01"];
    }

    private static ModelInspectionFixtureFigmaState ExpectedFigmaState(
        string tag) => tag switch
        {
            "figma.inspection-progress" =>
                ModelInspectionFixtureFigmaState.InspectionProgress,
            "figma.ready-collapsed" =>
                ModelInspectionFixtureFigmaState.ReadyCollapsed,
            "figma.ready-expanded" =>
                ModelInspectionFixtureFigmaState.ReadyExpanded,
            "figma.ready-with-warnings-collapsed" =>
                ModelInspectionFixtureFigmaState.ReadyWithWarningsCollapsed,
            "figma.ready-with-warnings-expanded" =>
                ModelInspectionFixtureFigmaState.ReadyWithWarningsExpanded,
            "figma.conversion-required-collapsed" =>
                ModelInspectionFixtureFigmaState.ConversionRequiredCollapsed,
            "figma.conversion-required-expanded" =>
                ModelInspectionFixtureFigmaState.ConversionRequiredExpanded,
            "figma.incomplete-package" =>
                ModelInspectionFixtureFigmaState.IncompletePackage,
            "figma.unsupported" => ModelInspectionFixtureFigmaState.Unsupported,
            "figma.invalid-collapsed" =>
                ModelInspectionFixtureFigmaState.InvalidCollapsed,
            "figma.invalid-expanded" =>
                ModelInspectionFixtureFigmaState.InvalidExpanded,
            "figma.cancelled" => ModelInspectionFixtureFigmaState.Cancelled,
            "figma.operational-failure" =>
                ModelInspectionFixtureFigmaState.OperationalFailure,
            _ => throw new InvalidOperationException("Invalid authoritative Figma tag.")
        };

    private static ModelInspectionExpectedFigmaState ExpectedScreenState(
        ModelInspectionFixtureFigmaState state) => state switch
        {
            ModelInspectionFixtureFigmaState.InspectionProgress =>
                ModelInspectionExpectedFigmaState.InspectionProgress,
            ModelInspectionFixtureFigmaState.ReadyCollapsed =>
                ModelInspectionExpectedFigmaState.ReadyCollapsed,
            ModelInspectionFixtureFigmaState.ReadyExpanded =>
                ModelInspectionExpectedFigmaState.ReadyExpanded,
            ModelInspectionFixtureFigmaState.ReadyWithWarningsCollapsed =>
                ModelInspectionExpectedFigmaState.ReadyWithWarningsCollapsed,
            ModelInspectionFixtureFigmaState.ReadyWithWarningsExpanded =>
                ModelInspectionExpectedFigmaState.ReadyWithWarningsExpanded,
            ModelInspectionFixtureFigmaState.ConversionRequiredCollapsed =>
                ModelInspectionExpectedFigmaState.ConversionRequiredCollapsed,
            ModelInspectionFixtureFigmaState.ConversionRequiredExpanded =>
                ModelInspectionExpectedFigmaState.ConversionRequiredExpanded,
            ModelInspectionFixtureFigmaState.IncompletePackage =>
                ModelInspectionExpectedFigmaState.IncompletePackage,
            ModelInspectionFixtureFigmaState.Unsupported =>
                ModelInspectionExpectedFigmaState.Unsupported,
            ModelInspectionFixtureFigmaState.InvalidCollapsed =>
                ModelInspectionExpectedFigmaState.InvalidCollapsed,
            ModelInspectionFixtureFigmaState.InvalidExpanded =>
                ModelInspectionExpectedFigmaState.InvalidExpanded,
            ModelInspectionFixtureFigmaState.Cancelled =>
                ModelInspectionExpectedFigmaState.Cancelled,
            ModelInspectionFixtureFigmaState.OperationalFailure =>
                ModelInspectionExpectedFigmaState.OperationalFailure,
            _ => throw new InvalidOperationException("Invalid authoritative Figma state.")
        };

    private static ImmutableArray<ModelInspectionFixtureOutcome> ExpectedOutcomes(
        IEnumerable<string> tags) => tags
        .Where(tag => tag.StartsWith("outcome.", StringComparison.Ordinal))
        .Select(tag => tag switch
        {
            "outcome.ready" => ModelInspectionFixtureOutcome.Ready,
            "outcome.ready-with-warnings" =>
                ModelInspectionFixtureOutcome.ReadyWithWarnings,
            "outcome.conversion-required" =>
                ModelInspectionFixtureOutcome.ConversionRequired,
            "outcome.incomplete-package" =>
                ModelInspectionFixtureOutcome.IncompletePackage,
            "outcome.unsupported" => ModelInspectionFixtureOutcome.Unsupported,
            "outcome.invalid" => ModelInspectionFixtureOutcome.Invalid,
            _ => throw new InvalidOperationException("Invalid authoritative outcome tag.")
        })
        .ToImmutableArray();

    private static ImmutableArray<ModelInspectionFixtureLifecycleTag>
        ExpectedLifecycleTags(IEnumerable<string> tags) => tags
        .Where(tag => tag.StartsWith("lifecycle.", StringComparison.Ordinal))
        .Select(tag => tag switch
        {
            "lifecycle.cancellation-requested" =>
                ModelInspectionFixtureLifecycleTag.CancellationRequested,
            "lifecycle.cooperative-cancellation" =>
                ModelInspectionFixtureLifecycleTag.CooperativeCancellation,
            "lifecycle.forced-cancellation" =>
                ModelInspectionFixtureLifecycleTag.ForcedCancellation,
            "lifecycle.retry-after-cancellation" =>
                ModelInspectionFixtureLifecycleTag.RetryAfterCancellation,
            "lifecycle.retry-after-operational-failure" =>
                ModelInspectionFixtureLifecycleTag.RetryAfterOperationalFailure,
            "lifecycle.stale-progress-rejected" =>
                ModelInspectionFixtureLifecycleTag.StaleProgressRejected,
            "lifecycle.stale-result-rejected" =>
                ModelInspectionFixtureLifecycleTag.StaleResultRejected,
            "lifecycle.stale-motion-rejected" =>
                ModelInspectionFixtureLifecycleTag.StaleMotionRejected,
            "lifecycle.stale-announcement-rejected" =>
                ModelInspectionFixtureLifecycleTag.StaleAnnouncementRejected,
            "lifecycle.choose-another-retired" =>
                ModelInspectionFixtureLifecycleTag.ChooseAnotherRetired,
            "lifecycle.gallery-switch-retired" =>
                ModelInspectionFixtureLifecycleTag.GallerySwitchRetired,
            _ => throw new InvalidOperationException("Invalid authoritative lifecycle tag.")
        })
        .ToImmutableArray();

    private static ImmutableArray<ModelInspectionFixtureFailureProfile>
        ExpectedFailureProfiles(IEnumerable<string> tags) => tags
        .Where(tag => tag.StartsWith("failure.", StringComparison.Ordinal))
        .Select(tag => tag switch
        {
            "failure.worker-start-failure" =>
                ModelInspectionFixtureFailureProfile.WorkerStartFailure,
            "failure.worker-timeout" =>
                ModelInspectionFixtureFailureProfile.WorkerTimeout,
            "failure.worker-crash-early-exit" =>
                ModelInspectionFixtureFailureProfile.WorkerCrashEarlyExit,
            "failure.malformed-worker-response" =>
                ModelInspectionFixtureFailureProfile.MalformedWorkerResponse,
            "failure.cancellation-unconfirmed" =>
                ModelInspectionFixtureFailureProfile.CancellationUnconfirmed,
            _ => throw new InvalidOperationException("Invalid authoritative failure tag.")
        })
        .ToImmutableArray();

    private static ImmutableArray<ModelInspectionFixtureStressTag> ExpectedStressTags(
        IEnumerable<string> tags) => tags
        .Where(tag => tag.StartsWith("stress.", StringComparison.Ordinal))
        .Select(tag => tag switch
        {
            "stress.maximum-model-name" =>
                ModelInspectionFixtureStressTag.MaximumModelName,
            "stress.missing-optional-metadata" =>
                ModelInspectionFixtureStressTag.MissingOptionalMetadata,
            "stress.maximum-check-rows" =>
                ModelInspectionFixtureStressTag.MaximumCheckRows,
            "stress.maximum-finding-rows" =>
                ModelInspectionFixtureStressTag.MaximumFindingRows,
            "stress.maximum-report-rows" =>
                ModelInspectionFixtureStressTag.MaximumReportRows,
            "stress.maximum-detail-copy" =>
                ModelInspectionFixtureStressTag.MaximumDetailCopy,
            _ => throw new InvalidOperationException("Invalid authoritative stress tag.")
        })
        .ToImmutableArray();

    private static void ValidateAuthoritativePresetMatrix(
        ModelInspectionFixtureCoveragePolicy policy)
    {
        (string Id, ModelInspectionFixtureWidthProfile Width,
            ModelInspectionFixtureResourceProfile Resources,
            ModelInspectionFixtureTextProfile Text,
            ModelInspectionFixtureMotionProfile Motion)[] expected =
        [
            ("P01", ModelInspectionFixtureWidthProfile.Desktop1440,
                ModelInspectionFixtureResourceProfile.Light,
                ModelInspectionFixtureTextProfile.Standard100,
                ModelInspectionFixtureMotionProfile.Normal),
            ("P02", ModelInspectionFixtureWidthProfile.Desktop1440,
                ModelInspectionFixtureResourceProfile.Dark,
                ModelInspectionFixtureTextProfile.Preview200,
                ModelInspectionFixtureMotionProfile.Reduced),
            ("P03", ModelInspectionFixtureWidthProfile.Desktop1440,
                ModelInspectionFixtureResourceProfile.HighContrastPreview,
                ModelInspectionFixtureTextProfile.Standard100,
                ModelInspectionFixtureMotionProfile.Reduced),
            ("P04", ModelInspectionFixtureWidthProfile.Medium600,
                ModelInspectionFixtureResourceProfile.Light,
                ModelInspectionFixtureTextProfile.Preview200,
                ModelInspectionFixtureMotionProfile.Normal),
            ("P05", ModelInspectionFixtureWidthProfile.Medium600,
                ModelInspectionFixtureResourceProfile.Dark,
                ModelInspectionFixtureTextProfile.Standard100,
                ModelInspectionFixtureMotionProfile.Reduced),
            ("P06", ModelInspectionFixtureWidthProfile.Medium600,
                ModelInspectionFixtureResourceProfile.HighContrastPreview,
                ModelInspectionFixtureTextProfile.Preview200,
                ModelInspectionFixtureMotionProfile.Reduced),
            ("P07", ModelInspectionFixtureWidthProfile.Narrow360,
                ModelInspectionFixtureResourceProfile.Light,
                ModelInspectionFixtureTextProfile.Standard100,
                ModelInspectionFixtureMotionProfile.Reduced),
            ("P08", ModelInspectionFixtureWidthProfile.Narrow360,
                ModelInspectionFixtureResourceProfile.Dark,
                ModelInspectionFixtureTextProfile.Preview200,
                ModelInspectionFixtureMotionProfile.Normal),
            ("P09", ModelInspectionFixtureWidthProfile.Narrow360,
                ModelInspectionFixtureResourceProfile.HighContrastPreview,
                ModelInspectionFixtureTextProfile.Standard100,
                ModelInspectionFixtureMotionProfile.Normal)
        ];
        if (policy.Presets.Count != expected.Length)
        {
            throw CoverageFailure("$.presets", "coverage.preset-matrix");
        }

        for (int index = 0; index < expected.Length; index++)
        {
            ModelInspectionFixturePreset actual = policy.Presets[index];
            var required = expected[index];
            if (!actual.Id.Equals(required.Id, StringComparison.Ordinal) ||
                actual.Width != required.Width ||
                actual.Resources != required.Resources ||
                actual.Text != required.Text ||
                actual.Motion != required.Motion)
            {
                throw CoverageFailure("$.presets", "coverage.preset-matrix");
            }
        }

        if (CountCrossDimensionPairs(policy.Presets) != 37)
        {
            throw CoverageFailure("$.presets", "coverage.preset-matrix");
        }
    }

    private static int CountCrossDimensionPairs(
        IReadOnlyList<ModelInspectionFixturePreset> presets)
    {
        HashSet<string> pairs = new(StringComparer.Ordinal);
        foreach (ModelInspectionFixturePreset preset in presets)
        {
            string[] values =
            [
                $"width:{preset.Width}",
                $"resources:{preset.Resources}",
                $"text:{preset.Text}",
                $"motion:{preset.Motion}"
            ];
            for (int left = 0; left < values.Length; left++)
            {
                for (int right = left + 1; right < values.Length; right++)
                {
                    pairs.Add($"{values[left]}|{values[right]}");
                }
            }
        }

        return pairs.Count;
    }

    private static void ValidatePolicyPairsAndExternalEvidence(
        ModelInspectionFixtureCoveragePolicy policy)
    {
        if (!policy.Schema.Equals(
                "model-inspection-fixture.schema.json",
                StringComparison.Ordinal) ||
            policy.SchemaVersion != 1 ||
            policy.DisclosurePairs.Count != 4 ||
            !MatchesDisclosurePair(policy.DisclosurePairs[0], "MI-002", "MI-003") ||
            !MatchesDisclosurePair(policy.DisclosurePairs[1], "MI-004", "MI-005") ||
            !MatchesDisclosurePair(policy.DisclosurePairs[2], "MI-006", "MI-007") ||
            !MatchesDisclosurePair(policy.DisclosurePairs[3], "MI-010", "MI-011"))
        {
            throw CoverageFailure("$.disclosurePairs", "coverage.transition");
        }

        if (policy.GallerySwitchPairs.Count != 1 ||
            !policy.GallerySwitchPairs[0].SourceId.Equals(
                "MI-038",
                StringComparison.Ordinal) ||
            !policy.GallerySwitchPairs[0].DestinationId.Equals(
                "MI-039",
                StringComparison.Ordinal))
        {
            throw CoverageFailure("$.gallerySwitchPairs", "coverage.transition");
        }

        if (policy.ExternalEvidenceLinks.Count != 2 ||
            !MatchesExternalEvidence(policy.ExternalEvidenceLinks[0], "MI-002") ||
            !MatchesExternalEvidence(policy.ExternalEvidenceLinks[1], "MI-003"))
        {
            throw CoverageFailure(
                "$.externalEvidenceLinks",
                "coverage.external-evidence");
        }
    }

    private static bool MatchesDisclosurePair(
        ModelInspectionFixtureDisclosurePair pair,
        string collapsed,
        string expanded) =>
        pair.CollapsedId.Equals(collapsed, StringComparison.Ordinal) &&
        pair.ExpandedId.Equals(expanded, StringComparison.Ordinal);

    private static bool MatchesExternalEvidence(
        ModelInspectionFixtureExternalEvidenceLink link,
        string fixtureId) =>
        link.FixtureId.Equals(fixtureId, StringComparison.Ordinal) &&
        link.EvidenceId.Equals("N-001", StringComparison.Ordinal) &&
        link.SourcePath.Equals(
            "tests/TestFixtures/GGUF/N-001-vocab-only-spm.gguf",
            StringComparison.Ordinal) &&
        link.JourneyTest.Equals(
            "PackagedN001_PageJourneyCompletesAllFiveStagesAsReady",
            StringComparison.Ordinal);

    private static ReplayTrace ReplayReleasedPrefix(
        ValidatedModelInspectionFixture fixture)
    {
        IReadOnlyList<ModelInspectionFixtureAttemptDescriptor> attempts =
            fixture.Input.Attempts;
        if ((fixture.Id.Equals("MI-001", StringComparison.Ordinal) &&
             attempts.Count != 0) ||
            (!fixture.Id.Equals("MI-001", StringComparison.Ordinal) &&
             attempts.Count == 0))
        {
            throw CoverageFailure("$.fixtures", "coverage.semantic-binding");
        }

        foreach (ModelInspectionFixtureAttemptDescriptor attempt in attempts)
        {
            int terminals = attempt.ServiceSteps.Count(step =>
                IsTerminal(step.Effect.Kind));
            if (terminals != 1)
            {
                throw CoverageFailure("$.fixtures", "coverage.semantic-binding");
            }
        }

        var reached = new List<ReachedEffect>();
        var interactions = new List<SetupInteractionEvent>();
        var staleReleases = new List<StaleReleaseEvent>();
        HashSet<int> obsoleteAttempts = [];
        int activeAttempt = attempts.Count == 0 ? 0 : 1;
        int nextServiceStep = 0;
        bool terminalReleased = false;
        bool observed = false;
        ModelInspectionFixtureServiceEffectDescriptor? currentEffect = null;

        ApplyAutomaticSteps(-1);
        for (int setupOrdinal = 0;
             setupOrdinal < fixture.Input.SetupSteps.Count;
             setupOrdinal++)
        {
            ModelInspectionFixtureSetupStepDescriptor setup =
                fixture.Input.SetupSteps[setupOrdinal];
            switch (setup.Kind)
            {
                case ModelInspectionFixtureSetupStepKind.ReleaseServiceCheckpoint:
                    if (activeAttempt <= 0 ||
                        setup.Attempt != activeAttempt ||
                        activeAttempt > attempts.Count)
                    {
                        throw CoverageFailure(
                            "$.fixtures",
                            "coverage.semantic-binding");
                    }

                    ModelInspectionFixtureAttemptDescriptor active =
                        attempts[activeAttempt - 1];
                    if (nextServiceStep >= active.ServiceSteps.Count)
                    {
                        throw CoverageFailure(
                            "$.fixtures",
                            "coverage.semantic-binding");
                    }

                    ModelInspectionFixtureServiceStepDescriptor service =
                        active.ServiceSteps[nextServiceStep];
                    if (service.Trigger.Kind !=
                            ModelInspectionFixtureServiceTriggerKind.Checkpoint ||
                        !string.Equals(
                            service.Trigger.Checkpoint,
                            setup.Checkpoint,
                            StringComparison.Ordinal) ||
                        terminalReleased)
                    {
                        throw CoverageFailure(
                            "$.fixtures",
                            "coverage.semantic-binding");
                    }

                    ApplyEffect(service.Effect, setupOrdinal);
                    nextServiceStep++;
                    ApplyAutomaticSteps(setupOrdinal);
                    break;

                case ModelInspectionFixtureSetupStepKind.InvokeRetry:
                case ModelInspectionFixtureSetupStepKind.InvokeRestart:
                    if (activeAttempt <= 0 || activeAttempt >= attempts.Count)
                    {
                        throw CoverageFailure(
                            "$.fixtures",
                            "coverage.semantic-binding");
                    }

                    interactions.Add(new SetupInteractionEvent(
                        setupOrdinal,
                        activeAttempt,
                        setup.Kind == ModelInspectionFixtureSetupStepKind.InvokeRetry
                            ? ModelInspectionFixtureInteractionKind.Retry
                            : ModelInspectionFixtureInteractionKind.Restart));
                    obsoleteAttempts.Add(activeAttempt);
                    activeAttempt++;
                    nextServiceStep = 0;
                    terminalReleased = false;
                    currentEffect = null;
                    ApplyAutomaticSteps(setupOrdinal);
                    break;

                case ModelInspectionFixtureSetupStepKind.InvokeDisclosure:
                    interactions.Add(new SetupInteractionEvent(
                        setupOrdinal,
                        activeAttempt,
                        ModelInspectionFixtureInteractionKind.Expand));
                    break;
                case ModelInspectionFixtureSetupStepKind.InvokeCancel:
                    interactions.Add(new SetupInteractionEvent(
                        setupOrdinal,
                        activeAttempt,
                        ModelInspectionFixtureInteractionKind.Cancel));
                    break;
                case ModelInspectionFixtureSetupStepKind.InvokeChooseAnother:
                    interactions.Add(new SetupInteractionEvent(
                        setupOrdinal,
                        activeAttempt,
                        ModelInspectionFixtureInteractionKind.ChooseAnother));
                    if (activeAttempt > 0)
                    {
                        obsoleteAttempts.Add(activeAttempt);
                        activeAttempt = 0;
                    }

                    break;

                case ModelInspectionFixtureSetupStepKind.ReleaseStaleProgress:
                case ModelInspectionFixtureSetupStepKind.SubmitStaleResultSnapshot:
                case ModelInspectionFixtureSetupStepKind.ReleaseStaleMotion:
                case ModelInspectionFixtureSetupStepKind.ReleaseStaleAnnouncement:
                    if (setup.Attempt is not { } staleAttempt ||
                        setup.Checkpoint is null)
                    {
                        throw CoverageFailure(
                            "$.fixtures",
                            "coverage.semantic-binding");
                    }

                    staleReleases.Add(new StaleReleaseEvent(
                        setupOrdinal,
                        staleAttempt,
                        setup.Checkpoint,
                        setup.Kind));
                    break;

                case ModelInspectionFixtureSetupStepKind.Observe:
                    observed = true;
                    setupOrdinal = fixture.Input.SetupSteps.Count;
                    break;

                default:
                    throw CoverageFailure(
                        "$.fixtures",
                        "coverage.semantic-binding");
            }
        }

        if (!observed)
        {
            throw CoverageFailure("$.fixtures", "coverage.semantic-binding");
        }

        return new ReplayTrace(
            reached.ToImmutableArray(),
            interactions.ToImmutableArray(),
            staleReleases.ToImmutableArray(),
            obsoleteAttempts.ToImmutableHashSet(),
            activeAttempt,
            nextServiceStep,
            currentEffect);

        void ApplyAutomaticSteps(int setupOrdinal)
        {
            if (activeAttempt <= 0)
            {
                return;
            }

            ModelInspectionFixtureAttemptDescriptor attempt =
                attempts[activeAttempt - 1];
            while (nextServiceStep < attempt.ServiceSteps.Count &&
                   attempt.ServiceSteps[nextServiceStep].Trigger.Kind ==
                       ModelInspectionFixtureServiceTriggerKind.Automatic)
            {
                if (terminalReleased)
                {
                    throw CoverageFailure(
                        "$.fixtures",
                        "coverage.semantic-binding");
                }

                ApplyEffect(
                    attempt.ServiceSteps[nextServiceStep].Effect,
                    setupOrdinal);
                nextServiceStep++;
            }
        }

        void ApplyEffect(
            ModelInspectionFixtureServiceEffectDescriptor effect,
            int setupOrdinal)
        {
            reached.Add(new ReachedEffect(setupOrdinal, activeAttempt, effect));
            if (effect.Kind is ModelInspectionFixtureServiceEffectKind.Progress or
                ModelInspectionFixtureServiceEffectKind.Completed or
                ModelInspectionFixtureServiceEffectKind.Cancelled or
                ModelInspectionFixtureServiceEffectKind.OperationalFailure)
            {
                currentEffect = effect;
            }

            terminalReleased = IsTerminal(effect.Kind);
        }
    }

    private static bool IsTerminal(ModelInspectionFixtureServiceEffectKind kind) =>
        kind is ModelInspectionFixtureServiceEffectKind.Completed or
            ModelInspectionFixtureServiceEffectKind.Cancelled or
            ModelInspectionFixtureServiceEffectKind.OperationalFailure;

    private static void ValidateReplayCoverageBinding(
        ValidatedModelInspectionFixture fixture,
        ReplayTrace replay)
    {
        ModelInspectionFixtureProgressDescriptor[] reachedProgress = replay.Effects
            .Where(item => item.Effect.Progress is not null)
            .Select(item => item.Effect.Progress!)
            .ToArray();
        ModelInspectionFixtureStage[] stages = reachedProgress
            .Select(progress => progress.Stage)
            .Distinct()
            .ToArray();
        ModelInspectionFixtureStageStatus[] statuses = reachedProgress
            .Select(progress => progress.Status)
            .Distinct()
            .ToArray();
        if (!fixture.Coverage.Stages.SequenceEqual(stages) ||
            !fixture.Coverage.StageStatuses.SequenceEqual(statuses))
        {
            throw CoverageFailure("$.fixtures", "coverage.semantic-binding");
        }

        ModelInspectionExpectedFigmaState replayedState = replay.CurrentEffect switch
        {
            null when fixture.Id.Equals("MI-001", StringComparison.Ordinal) =>
                ModelInspectionExpectedFigmaState.InspectionProgress,
            { Kind: ModelInspectionFixtureServiceEffectKind.Progress } =>
                ModelInspectionExpectedFigmaState.InspectionProgress,
            { Kind: ModelInspectionFixtureServiceEffectKind.Cancelled } =>
                ModelInspectionExpectedFigmaState.Cancelled,
            { Kind: ModelInspectionFixtureServiceEffectKind.OperationalFailure } =>
                ModelInspectionExpectedFigmaState.OperationalFailure,
            { Kind: ModelInspectionFixtureServiceEffectKind.Completed,
                Outcome: { } outcome } => ExpectedCompletedState(
                    outcome,
                    fixture.Variant),
            _ => throw CoverageFailure(
                "$.fixtures",
                "coverage.semantic-binding")
        };
        if (fixture.Expected.Figma.State != replayedState)
        {
            throw CoverageFailure("$.fixtures", "coverage.semantic-binding");
        }

        foreach (ModelInspectionFixtureOutcome outcome in fixture.Coverage.Outcomes)
        {
            bool observed = replay.CurrentEffect is
            {
                Kind: ModelInspectionFixtureServiceEffectKind.Completed,
                Outcome: { } currentOutcome
            } && currentOutcome == outcome;
            bool approvedEventual = fixture.Id is "MI-025" or "MI-026" &&
                EventualTerminal(fixture, replay) is
                {
                    Kind: ModelInspectionFixtureServiceEffectKind.Completed,
                    Outcome: { } eventualOutcome
                } && eventualOutcome == outcome;
            if (!observed && !approvedEventual)
            {
                throw CoverageFailure(
                    "$.fixtures",
                    "coverage.semantic-binding");
            }
        }

        foreach (ModelInspectionFixtureFailureProfile profile in
                 fixture.Coverage.FailureProfiles)
        {
            if (!replay.Effects.Any(item =>
                    item.Effect.Kind ==
                        ModelInspectionFixtureServiceEffectKind.OperationalFailure &&
                    item.Effect.FailureProfile == profile))
            {
                throw CoverageFailure(
                    "$.fixtures",
                    "coverage.semantic-binding");
            }
        }

        ImmutableArray<string> expectedTags = ExpectedCoverageTags[
            int.Parse(fixture.Id.AsSpan(3)) - 1];
        if (expectedTags.Contains("progress.initial", StringComparer.Ordinal) &&
            (fixture.Input.Attempts.Count != 0 || replay.CurrentEffect is not null))
        {
            throw CoverageFailure("$.fixtures", "coverage.semantic-binding");
        }

        if (expectedTags.Contains(
                "progress.bounded-fraction",
                StringComparer.Ordinal) &&
            !reachedProgress.Any(progress => progress.Fraction is >= 0 and <= 1))
        {
            throw CoverageFailure("$.fixtures", "coverage.semantic-binding");
        }

        if (expectedTags.Contains("terminal.cancelled", StringComparer.Ordinal))
        {
            bool cancelled = replay.CurrentEffect?.Kind ==
                ModelInspectionFixtureServiceEffectKind.Cancelled;
            bool eventualCancelledProgress = replay.CurrentEffect?.Progress?.Status ==
                    ModelInspectionFixtureStageStatus.Cancelled &&
                EventualTerminal(fixture, replay)?.Kind ==
                    ModelInspectionFixtureServiceEffectKind.Cancelled;
            if (!cancelled && !eventualCancelledProgress)
            {
                throw CoverageFailure(
                    "$.fixtures",
                    "coverage.semantic-binding");
            }
        }
    }

    private static ModelInspectionFixtureServiceEffectDescriptor? EventualTerminal(
        ValidatedModelInspectionFixture fixture,
        ReplayTrace replay)
    {
        if (replay.ActiveAttempt <= 0 ||
            replay.ActiveAttempt > fixture.Input.Attempts.Count)
        {
            return null;
        }

        return fixture.Input.Attempts[replay.ActiveAttempt - 1].ServiceSteps
            .Skip(replay.NextServiceStep)
            .Select(step => step.Effect)
            .FirstOrDefault(effect => IsTerminal(effect.Kind));
    }

    private static ModelInspectionExpectedFigmaState ExpectedCompletedState(
        ModelInspectionFixtureOutcome outcome,
        string? variant) => outcome switch
        {
            ModelInspectionFixtureOutcome.Ready when variant == "expanded" =>
                ModelInspectionExpectedFigmaState.ReadyExpanded,
            ModelInspectionFixtureOutcome.Ready =>
                ModelInspectionExpectedFigmaState.ReadyCollapsed,
            ModelInspectionFixtureOutcome.ReadyWithWarnings when
                variant == "expanded" =>
                ModelInspectionExpectedFigmaState.ReadyWithWarningsExpanded,
            ModelInspectionFixtureOutcome.ReadyWithWarnings =>
                ModelInspectionExpectedFigmaState.ReadyWithWarningsCollapsed,
            ModelInspectionFixtureOutcome.ConversionRequired when
                variant == "expanded" =>
                ModelInspectionExpectedFigmaState.ConversionRequiredExpanded,
            ModelInspectionFixtureOutcome.ConversionRequired =>
                ModelInspectionExpectedFigmaState.ConversionRequiredCollapsed,
            ModelInspectionFixtureOutcome.IncompletePackage =>
                ModelInspectionExpectedFigmaState.IncompletePackage,
            ModelInspectionFixtureOutcome.Unsupported =>
                ModelInspectionExpectedFigmaState.Unsupported,
            ModelInspectionFixtureOutcome.Invalid when variant == "expanded" =>
                ModelInspectionExpectedFigmaState.InvalidExpanded,
            ModelInspectionFixtureOutcome.Invalid =>
                ModelInspectionExpectedFigmaState.InvalidCollapsed,
            _ => throw CoverageFailure(
                "$.fixtures",
                "coverage.semantic-binding")
        };

    private static void ValidateLifecycleAndStressBinding(
        ValidatedModelInspectionFixture fixture,
        ReplayTrace replay)
    {
        foreach (ModelInspectionFixtureLifecycleTag tag in
                 fixture.Coverage.LifecycleTags)
        {
            bool proven = tag switch
            {
                ModelInspectionFixtureLifecycleTag.CancellationRequested =>
                    replay.Interactions.Any(item =>
                        item.Kind == ModelInspectionFixtureInteractionKind.Cancel),
                ModelInspectionFixtureLifecycleTag.CooperativeCancellation =>
                    HasCancelTerminal(
                        replay,
                        ModelInspectionFixtureServiceEffectKind.Cancelled,
                        null),
                ModelInspectionFixtureLifecycleTag.ForcedCancellation =>
                    HasCancelTerminal(
                        replay,
                        ModelInspectionFixtureServiceEffectKind.OperationalFailure,
                        ModelInspectionFixtureFailureProfile.CancellationUnconfirmed),
                ModelInspectionFixtureLifecycleTag.RetryAfterCancellation =>
                    HasRetryTerminal(
                        replay,
                        ModelInspectionFixtureInteractionKind.Restart,
                        ModelInspectionFixtureServiceEffectKind.Cancelled),
                ModelInspectionFixtureLifecycleTag.RetryAfterOperationalFailure =>
                    HasRetryTerminal(
                        replay,
                        ModelInspectionFixtureInteractionKind.Retry,
                        ModelInspectionFixtureServiceEffectKind.OperationalFailure),
                ModelInspectionFixtureLifecycleTag.StaleProgressRejected =>
                    HasStaleProof(
                        replay,
                        ModelInspectionFixtureServiceEffectKind.DeferStaleProgress,
                        ModelInspectionFixtureSetupStepKind.ReleaseStaleProgress),
                ModelInspectionFixtureLifecycleTag.StaleResultRejected =>
                    HasStaleProof(
                        replay,
                        ModelInspectionFixtureServiceEffectKind.DeferStaleResultSnapshot,
                        ModelInspectionFixtureSetupStepKind.SubmitStaleResultSnapshot),
                ModelInspectionFixtureLifecycleTag.StaleMotionRejected =>
                    HasStaleProof(
                        replay,
                        ModelInspectionFixtureServiceEffectKind.DeferStaleMotion,
                        ModelInspectionFixtureSetupStepKind.ReleaseStaleMotion),
                ModelInspectionFixtureLifecycleTag.StaleAnnouncementRejected =>
                    HasStaleProof(
                        replay,
                        ModelInspectionFixtureServiceEffectKind.DeferStaleAnnouncement,
                        ModelInspectionFixtureSetupStepKind.ReleaseStaleAnnouncement),
                ModelInspectionFixtureLifecycleTag.ChooseAnotherRetired =>
                    fixture.Id.Equals("MI-037", StringComparison.Ordinal) &&
                    HasVisibleChooseAnotherRetirement(fixture) &&
                    !replay.Interactions.Any(item => item.Kind ==
                        ModelInspectionFixtureInteractionKind.ChooseAnother),
                ModelInspectionFixtureLifecycleTag.GallerySwitchRetired =>
                    fixture.Id.Equals("MI-038", StringComparison.Ordinal) &&
                    HasVisibleChooseAnotherRetirement(fixture) &&
                    !replay.Interactions.Any(item => item.Kind ==
                        ModelInspectionFixtureInteractionKind.ChooseAnother),
                _ => false
            };
            if (!proven)
            {
                throw CoverageFailure(
                    "$.fixtures",
                    "coverage.semantic-binding");
            }
        }

        foreach (ModelInspectionFixtureStressTag tag in fixture.Coverage.StressTags)
        {
            bool proven = tag switch
            {
                ModelInspectionFixtureStressTag.MaximumModelName =>
                    fixture.Input.Request.DisplayName.Length == 160,
                ModelInspectionFixtureStressTag.MissingOptionalMetadata =>
                    fixture.Input.Request.EvidenceProfile ==
                        ModelInspectionFixtureEvidenceProfile
                            .CompatibleMissingOptionalMetadata &&
                    fixture.Expected.Model.Metadata.Any(field =>
                        field.Value.DefaultText.Equals(
                            "Not reported",
                            StringComparison.Ordinal)),
                ModelInspectionFixtureStressTag.MaximumCheckRows =>
                    fixture.Expected.Model.Checks.Count == 5,
                ModelInspectionFixtureStressTag.MaximumFindingRows =>
                    fixture.Expected.Content.Rows.Count == 1 &&
                    fixture.Expected.Figma.State ==
                        ModelInspectionExpectedFigmaState
                            .ReadyWithWarningsExpanded,
                ModelInspectionFixtureStressTag.MaximumReportRows =>
                    fixture.Expected.Content.Rows.Count == 1 &&
                    fixture.Expected.Figma.State ==
                        ModelInspectionExpectedFigmaState.InvalidExpanded,
                ModelInspectionFixtureStressTag.MaximumDetailCopy =>
                    EnumerateExpectedCopies(fixture.Expected)
                        .Any(copy => copy.DefaultText.Length == 512) &&
                    (replay.Effects.Any(item =>
                         item.Effect.Progress?.DetailProfile ==
                             ModelInspectionFixtureProgressDetailProfile.Maximum) ||
                     replay.Effects.Any(item =>
                         item.Effect.FailureDetailProfile ==
                             ModelInspectionFixtureFailureDetailProfile.Maximum)),
                _ => false
            };
            if (!proven)
            {
                throw CoverageFailure(
                    "$.fixtures",
                    "coverage.semantic-binding");
            }
        }

        ModelInspectionFixtureFailureDetailProfile expectedFailureDetail =
            fixture.Id.Equals("MI-049", StringComparison.Ordinal)
                ? ModelInspectionFixtureFailureDetailProfile.Maximum
                : ModelInspectionFixtureFailureDetailProfile.Default;
        if (fixture.Input.Attempts
            .SelectMany(attempt => attempt.ServiceSteps)
            .Select(step => step.Effect)
            .Where(effect => effect.Kind ==
                ModelInspectionFixtureServiceEffectKind.OperationalFailure)
            .Any(effect => effect.FailureDetailProfile != expectedFailureDetail))
        {
            throw CoverageFailure("$.fixtures", "coverage.semantic-binding");
        }

        ModelInspectionFixtureProgressDetailProfile expectedProgressDetail =
            fixture.Id.Equals("MI-048", StringComparison.Ordinal)
                ? ModelInspectionFixtureProgressDetailProfile.Maximum
                : ModelInspectionFixtureProgressDetailProfile.Default;
        if (fixture.Input.Attempts
            .SelectMany(attempt => attempt.ServiceSteps)
            .Select(step => step.Effect.Progress)
            .Where(progress => progress is not null)
            .Any(progress => progress!.DetailProfile != expectedProgressDetail))
        {
            throw CoverageFailure("$.fixtures", "coverage.semantic-binding");
        }
    }

    private static bool HasCancelTerminal(
        ReplayTrace replay,
        ModelInspectionFixtureServiceEffectKind terminalKind,
        ModelInspectionFixtureFailureProfile? failureProfile)
    {
        foreach (SetupInteractionEvent cancel in replay.Interactions.Where(item =>
                     item.Kind == ModelInspectionFixtureInteractionKind.Cancel))
        {
            if (replay.Effects.Any(item =>
                    item.Attempt == cancel.Attempt &&
                    item.SetupOrdinal > cancel.SetupOrdinal &&
                    item.Effect.Kind == terminalKind &&
                    (failureProfile is null ||
                     item.Effect.FailureProfile == failureProfile)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasRetryTerminal(
        ReplayTrace replay,
        ModelInspectionFixtureInteractionKind retryKind,
        ModelInspectionFixtureServiceEffectKind priorTerminalKind)
    {
        foreach (SetupInteractionEvent transition in replay.Interactions.Where(item =>
                     item.Kind == retryKind))
        {
            bool prior = replay.Effects.Any(item =>
                item.Attempt == transition.Attempt &&
                item.SetupOrdinal < transition.SetupOrdinal &&
                item.Effect.Kind == priorTerminalKind);
            bool next = replay.Effects.Any(item =>
                item.Attempt == transition.Attempt + 1 &&
                item.SetupOrdinal > transition.SetupOrdinal &&
                item.Effect.Kind == ModelInspectionFixtureServiceEffectKind.Completed &&
                item.Effect.Outcome == ModelInspectionFixtureOutcome.Ready &&
                item.Effect.EvidenceProfile ==
                    ModelInspectionFixtureEvidenceProfile.Compatible);
            if (prior && next)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasStaleProof(
        ReplayTrace replay,
        ModelInspectionFixtureServiceEffectKind deferredKind,
        ModelInspectionFixtureSetupStepKind releaseKind)
    {
        foreach (StaleReleaseEvent release in replay.StaleReleases.Where(item =>
                     item.Kind == releaseKind))
        {
            ReachedEffect[] captures = replay.Effects.Where(item =>
                item.Attempt == release.Attempt &&
                item.Effect.Kind == deferredKind &&
                string.Equals(
                    item.Effect.DeferredCheckpoint,
                    release.Checkpoint,
                    StringComparison.Ordinal) &&
                item.SetupOrdinal < release.SetupOrdinal).ToArray();
            SetupInteractionEvent[] transitions = replay.Interactions.Where(item =>
                item.Attempt == release.Attempt &&
                item.Kind is ModelInspectionFixtureInteractionKind.Retry or
                    ModelInspectionFixtureInteractionKind.Restart &&
                item.SetupOrdinal < release.SetupOrdinal).ToArray();
            if (captures.Length == 1 &&
                transitions.Length == 1 &&
                captures[0].SetupOrdinal < transitions[0].SetupOrdinal &&
                replay.ObsoleteAttempts.Contains(release.Attempt))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasVisibleChooseAnotherRetirement(
        ValidatedModelInspectionFixture fixture)
    {
        ModelInspectionFixtureInteraction[] choices = fixture.VisibleInteractions
            .Where(interaction => interaction.Kind ==
                ModelInspectionFixtureInteractionKind.ChooseAnother)
            .ToArray();
        return choices.Length == 1 &&
            choices[0].Target.Equals(
                "gallery:no-active-fixture",
                StringComparison.Ordinal) &&
            choices[0].LifetimeEffect ==
                ModelInspectionFixtureInteractionLifetimeEffect.NoActiveFixture;
    }

    private static void ValidateInteractionTransitions(
        ValidatedModelInspectionFixture fixture)
    {
        if (!IsUnique(fixture.Interactions.Select(interaction => interaction.Id)))
        {
            throw CoverageFailure("$.fixtures", "coverage.identity-closure");
        }

        foreach (ModelInspectionFixtureInteraction interaction in fixture.Interactions)
        {
            if (interaction.Kind ==
                    ModelInspectionFixtureInteractionKind.ChooseAnother &&
                (!interaction.Target.Equals(
                     "gallery:no-active-fixture",
                     StringComparison.Ordinal) ||
                 interaction.LifetimeEffect !=
                     ModelInspectionFixtureInteractionLifetimeEffect.NoActiveFixture))
            {
                throw CoverageFailure("$.fixtures", "coverage.transition");
            }

            if (interaction.Kind == ModelInspectionFixtureInteractionKind.Reset &&
                (!interaction.SourceCheckpoint.Equals(
                     fixture.Input.ObservationCheckpoint,
                     StringComparison.Ordinal) ||
                 !interaction.Target.Equals(fixture.Id, StringComparison.Ordinal) ||
                 interaction.LifetimeEffect !=
                     ModelInspectionFixtureInteractionLifetimeEffect.RetirePage))
            {
                throw CoverageFailure("$.fixtures", "coverage.transition");
            }

            if (interaction.Kind is ModelInspectionFixtureInteractionKind.Retry or
                    ModelInspectionFixtureInteractionKind.Restart &&
                interaction.LifetimeEffect !=
                    ModelInspectionFixtureInteractionLifetimeEffect.None)
            {
                throw CoverageFailure("$.fixtures", "coverage.transition");
            }
        }

        if (fixture.Id is "MI-037" or "MI-038" &&
            (!HasVisibleChooseAnotherRetirement(fixture) ||
             fixture.Input.SetupSteps.Any(step => step.Kind ==
                 ModelInspectionFixtureSetupStepKind.InvokeChooseAnother)))
        {
            throw CoverageFailure("$.fixtures", "coverage.transition");
        }
    }

    private static void ValidateDisclosurePairs(
        ModelInspectionFixtureCatalogue catalogue)
    {
        (int Collapsed, int Expanded, bool ModelOwner)[] pairs =
        [
            (2, 3, true),
            (4, 5, false),
            (6, 7, false),
            (10, 11, false)
        ];
        foreach ((int collapsedOrdinal, int expandedOrdinal, bool modelOwner) in pairs)
        {
            ValidatedModelInspectionFixture collapsed =
                catalogue.Fixtures[collapsedOrdinal - 1];
            ValidatedModelInspectionFixture expanded =
                catalogue.Fixtures[expandedOrdinal - 1];
            ModelInspectionFixtureInteraction[] collapsedExpand =
                collapsed.VisibleInteractions.Where(interaction =>
                    interaction.Kind == ModelInspectionFixtureInteractionKind.Expand)
                .ToArray();
            ModelInspectionFixtureInteraction[] expandedCollapse =
                expanded.VisibleInteractions.Where(interaction =>
                    interaction.Kind == ModelInspectionFixtureInteractionKind.Collapse)
                .ToArray();
            ModelInspectionFixtureInteraction[] setupExpand = expanded.Interactions
                .Where(interaction =>
                    interaction.Kind == ModelInspectionFixtureInteractionKind.Expand &&
                    !interaction.SourceCheckpoint.Equals(
                        expanded.Input.ObservationCheckpoint,
                        StringComparison.Ordinal))
                .ToArray();
            int setupDisclosureCount = expanded.Input.SetupSteps.Count(step =>
                step.Kind == ModelInspectionFixtureSetupStepKind.InvokeDisclosure);
            string disclosureOwner = modelOwner
                ? "inspection-details-disclosure"
                : "findings-disclosure";
            int ownerControls = expanded.Expected.Automation.Controls.Count(control =>
                control.Id.Equals(disclosureOwner, StringComparison.Ordinal));

            if (collapsedExpand.Length != 1 ||
                !collapsedExpand[0].Target.Equals(
                    expanded.Id,
                    StringComparison.Ordinal) ||
                collapsedExpand[0].LifetimeEffect !=
                    ModelInspectionFixtureInteractionLifetimeEffect.None ||
                expandedCollapse.Length != 1 ||
                !expandedCollapse[0].Target.Equals(
                    collapsed.Id,
                    StringComparison.Ordinal) ||
                expandedCollapse[0].LifetimeEffect !=
                    ModelInspectionFixtureInteractionLifetimeEffect.None ||
                setupExpand.Length != 1 ||
                !setupExpand[0].Target.Equals(
                    expanded.Input.ObservationCheckpoint,
                    StringComparison.Ordinal) ||
                setupDisclosureCount != 1 ||
                collapsed.Input.SetupSteps.Any(step => step.Kind ==
                    ModelInspectionFixtureSetupStepKind.InvokeDisclosure) ||
                collapsed.Expected.Model.DisclosureExpanded ||
                collapsed.Expected.Content.DisclosureExpanded ||
                expanded.Expected.Model.DisclosureExpanded != modelOwner ||
                expanded.Expected.Content.DisclosureExpanded == modelOwner ||
                ownerControls != 1 ||
                collapsed.Expected.Outcome.Kind != expanded.Expected.Outcome.Kind ||
                !collapsed.TargetCondition.Equals(
                    expanded.TargetCondition,
                    StringComparison.Ordinal) ||
                (modelOwner
                    ? collapsed.Expected.Model.Checks.Count != 0 ||
                      expanded.Expected.Model.Checks.Count != 5
                    : !collapsed.Expected.Model.Checks.Select(row => row.Id)
                        .SequenceEqual(
                            expanded.Expected.Model.Checks.Select(row => row.Id),
                            StringComparer.Ordinal)) ||
                !collapsed.Expected.Content.Rows.Select(row => row.Id).SequenceEqual(
                    expanded.Expected.Content.Rows.Select(row => row.Id),
                    StringComparer.Ordinal))
            {
                throw CoverageFailure("$.disclosurePairs", "coverage.transition");
            }
        }
    }

    private static void ValidatePresetExpectations(
        ValidatedModelInspectionFixture fixture,
        ModelInspectionFixtureCoveragePolicy policy,
        ReplayTrace replay)
    {
        if (fixture.PresetExpectations.Count != fixture.Presets.Count ||
            !fixture.PresetExpectations.Keys.OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual(
                    fixture.Presets.OrderBy(value => value, StringComparer.Ordinal),
                    StringComparer.Ordinal))
        {
            throw CoverageFailure("$.presets", "coverage.preset-matrix");
        }

        AnimationStartBounds normalMotionBounds =
            CalculateNormalMotionBounds(replay);
        HashSet<string> retained = fixture.Expected.RetainedIdentities.Ids
            .ToHashSet(StringComparer.Ordinal);
        foreach (string presetId in fixture.Presets)
        {
            if (!fixture.PresetExpectations.TryGetValue(
                    presetId,
                    out ModelInspectionPresetExpectation? expectation) ||
                presetId.Length != 3 ||
                !int.TryParse(presetId.AsSpan(1), out int presetOrdinal) ||
                presetOrdinal < 1 ||
                presetOrdinal > policy.Presets.Count)
            {
                throw CoverageFailure("$.presets", "coverage.preset-matrix");
            }

            ModelInspectionFixturePreset preset = policy.Presets[presetOrdinal - 1];
            (ModelInspectionFixtureResponsiveLayout Layout, double Minimum,
                double Maximum) bounds = preset.Width switch
            {
                ModelInspectionFixtureWidthProfile.Desktop1440 =>
                    (ModelInspectionFixtureResponsiveLayout.Desktop, 480, 960),
                ModelInspectionFixtureWidthProfile.Medium600 =>
                    (ModelInspectionFixtureResponsiveLayout.Medium, 360, 600),
                ModelInspectionFixtureWidthProfile.Narrow360 =>
                    (ModelInspectionFixtureResponsiveLayout.Narrow, 280, 360),
                _ => throw CoverageFailure(
                    "$.presets",
                    "coverage.preset-matrix")
            };
            AnimationStartBounds expectedMotionBounds = preset.Motion ==
                ModelInspectionFixtureMotionProfile.Reduced
                    ? new AnimationStartBounds(0, 0)
                    : normalMotionBounds;
            if (!preset.Id.Equals(presetId, StringComparison.Ordinal) ||
                expectation.ResponsiveLayout != bounds.Layout ||
                expectation.MinimumContentColumnWidth != bounds.Minimum ||
                expectation.MaximumContentColumnWidth != bounds.Maximum ||
                !expectation.NoClipping ||
                !expectation.NoOverlap ||
                !expectation.AllRequiredContentReachable ||
                expectation.MinimumPointerTargetWidth != 44 ||
                expectation.MinimumPointerTargetHeight != 44 ||
                expectation.Resources != preset.Resources ||
                expectation.TextScale != preset.Text ||
                expectation.Motion != preset.Motion ||
                !expectation.SemanticBrushesResolvedWithoutColorOnlyMeaning ||
                !expectation.FinalGeometryAndSemanticsEquivalentToNormalMotion ||
                expectation.MinimumAnimationStarts !=
                    expectedMotionBounds.Minimum ||
                expectation.MaximumAnimationStarts !=
                    expectedMotionBounds.Maximum)
            {
                throw CoverageFailure("$.presets", "coverage.preset-matrix");
            }

            string[] textRoleIds = expectation.TextRoles
                .Select(role => role.Id)
                .ToArray();
            if (!EqualNullable(
                    expectation.ScrollOwner,
                    fixture.Expected.RowsAndScroll.ScrollOwner) ||
                !expectation.FocusTarget.Equals(
                    fixture.Expected.Focus.Target,
                    StringComparison.Ordinal) ||
                !IsUnique(textRoleIds) ||
                !IsUnique(expectation.LogicalReadingOrder) ||
                !IsUnique(expectation.TabOrder) ||
                textRoleIds.Any(id => !retained.Contains(id)) ||
                expectation.LogicalReadingOrder.Any(id => !retained.Contains(id)) ||
                expectation.TabOrder.Any(id => !retained.Contains(id)) ||
                !retained.Contains(expectation.FocusTarget) ||
                expectation.ScrollOwner is { } scroll && !retained.Contains(scroll))
            {
                throw CoverageFailure(
                    "$.presetExpectations",
                    "coverage.identity-closure");
            }
        }
    }

    private static AnimationStartBounds CalculateNormalMotionBounds(
        ReplayTrace replay)
    {
        int terminalStarts = replay.Effects.Count(item =>
            IsTerminal(item.Effect.Kind));
        int disclosureStarts = replay.Interactions.Count(item => item.Kind is
            ModelInspectionFixtureInteractionKind.Expand or
            ModelInspectionFixtureInteractionKind.Collapse);
        int minimumProgressStarts = 0;
        int maximumProgressStarts = 0;

        foreach (IGrouping<int, ReachedEffect> attemptEffects in replay.Effects
                     .GroupBy(item => item.Attempt)
                     .OrderBy(group => group.Key))
        {
            MotionRowState[] maximumRows = CreateInitialMotionRows();
            foreach (ReachedEffect reached in attemptEffects)
            {
                if (reached.Effect.Progress is not { } progress)
                {
                    continue;
                }

                MotionRowState[] target = CreateMotionRows(progress);
                maximumProgressStarts = checked(maximumProgressStarts +
                    CountProgressAnimationStarts(maximumRows, target));
                maximumRows = target;
            }

            MotionRowState[] minimumRows = CreateInitialMotionRows();
            foreach (IGrouping<int, ReachedEffect> renderBatch in attemptEffects
                         .GroupBy(item => item.SetupOrdinal)
                         .OrderBy(group => group.Key))
            {
                ReachedEffect? lastVisible = renderBatch
                    .LastOrDefault(item => item.Effect.Progress is not null ||
                        IsTerminal(item.Effect.Kind));
                if (lastVisible?.Effect.Progress is not { } progress)
                {
                    continue;
                }

                MotionRowState[] target = CreateMotionRows(progress);
                minimumProgressStarts = checked(minimumProgressStarts +
                    CountProgressAnimationStarts(minimumRows, target));
                minimumRows = target;
            }
        }

        return new AnimationStartBounds(
            checked(terminalStarts + disclosureStarts + minimumProgressStarts),
            checked(terminalStarts + disclosureStarts + maximumProgressStarts));
    }

    private static MotionRowState[] CreateInitialMotionRows() =>
        Enumerable.Repeat(MotionRowState.Waiting, 5).ToArray();

    private static MotionRowState[] CreateMotionRows(
        ModelInspectionFixtureProgressDescriptor progress)
    {
        int currentIndex = (int)progress.Stage - 1;
        var rows = new MotionRowState[5];
        for (int index = 0; index < rows.Length; index++)
        {
            if (index < currentIndex)
            {
                rows[index] = MotionRowState.Passed;
                continue;
            }

            if (index > currentIndex)
            {
                rows[index] = MotionRowState.Waiting;
                continue;
            }

            MotionRowStatus status = progress.Status switch
            {
                ModelInspectionFixtureStageStatus.Active =>
                    MotionRowStatus.Active,
                ModelInspectionFixtureStageStatus.Completed =>
                    MotionRowStatus.Passed,
                ModelInspectionFixtureStageStatus.Warning =>
                    MotionRowStatus.Warning,
                ModelInspectionFixtureStageStatus.Failed =>
                    MotionRowStatus.Failed,
                ModelInspectionFixtureStageStatus.Cancelled =>
                    MotionRowStatus.Cancelled,
                _ => throw CoverageFailure(
                    "$.presetExpectations",
                    "coverage.preset-matrix")
            };
            rows[index] = new MotionRowState(
                status,
                status == MotionRowStatus.Active ? progress.Fraction : null,
                $"{progress.Stage}:{progress.DetailProfile}",
                DetailVisible: true);
        }

        return rows;
    }

    private static int CountProgressAnimationStarts(
        IReadOnlyList<MotionRowState> previous,
        IReadOnlyList<MotionRowState> current)
    {
        int starts = 0;
        for (int index = 0; index < current.Count; index++)
        {
            MotionRowState before = previous[index];
            MotionRowState after = current[index];
            bool statusChanged = before.Status != after.Status;
            bool detailChanged = before.DetailVisible != after.DetailVisible ||
                !string.Equals(
                    before.DetailToken,
                    after.DetailToken,
                    StringComparison.Ordinal);
            if (statusChanged)
            {
                starts++;
            }

            if (detailChanged &&
                after.Status == MotionRowStatus.Active &&
                after.DetailVisible)
            {
                starts++;
            }
        }

        return starts;
    }

    private static void ValidateUiIdentityClosure(
        ValidatedModelInspectionFixture fixture)
    {
        string[] rowIds = fixture.Expected.Model.Metadata.Select(row => row.Id)
            .Concat(fixture.Expected.Model.Checks.Select(row => row.Id))
            .Concat(fixture.Expected.Content.Rows.Select(row => row.Id))
            .ToArray();
        string[] actionIds = fixture.Expected.Actions.Items
            .Select(action => action.Id)
            .ToArray();
        string[] automationIds = fixture.Expected.Automation.Controls
            .Select(control => control.Id)
            .ToArray();
        string[] retainedIds = fixture.Expected.RetainedIdentities.Ids.ToArray();
        string[] orderedIds = fixture.Expected.RowsAndScroll.OrderedRowIds.ToArray();
        if (!IsUnique(rowIds) ||
            !IsUnique(actionIds) ||
            !IsUnique(automationIds) ||
            !IsUnique(retainedIds) ||
            !IsUnique(orderedIds))
        {
            throw CoverageFailure(
                "$.expected",
                "coverage.identity-closure");
        }

        HashSet<string> declared = rowIds.ToHashSet(StringComparer.Ordinal);
        declared.UnionWith(fixture.Expected.Actions.Items
            .Where(action => action.Visible)
            .Select(action => action.Id));
        declared.UnionWith(automationIds);
        HashSet<string> retained = retainedIds.ToHashSet(StringComparer.Ordinal);
        if (!retained.SetEquals(declared) ||
            !retained.Contains(fixture.Expected.Focus.Target))
        {
            throw CoverageFailure(
                "$.expected",
                "coverage.identity-closure");
        }

        string? scrollOwner = fixture.Expected.RowsAndScroll.ScrollOwner;
        string[] expectedOrdered = scrollOwner switch
        {
            null => [],
            "model-card" => fixture.Expected.Model.Checks
                .Select(row => row.Id)
                .ToArray(),
            _ => fixture.Expected.Content.Rows
                .Select(row => row.Id)
                .ToArray()
        };
        if (!orderedIds.SequenceEqual(expectedOrdered, StringComparer.Ordinal) ||
            scrollOwner is { } owner && !retained.Contains(owner))
        {
            throw CoverageFailure(
                "$.expected.rowsAndScroll",
                "coverage.identity-closure");
        }

        foreach (ModelInspectionExpectedContentRow row in
                 fixture.Expected.Content.Rows)
        {
            ModelInspectionExpectedAutomationControl[] controls =
                fixture.Expected.Automation.Controls.Where(control =>
                    control.Id.Equals(row.Id, StringComparison.Ordinal)).ToArray();
            if (controls.Length != 1 ||
                controls[0].ControlType !=
                    ModelInspectionExpectedControlType.ListItem ||
                !MatchesCopy(controls[0].AccessibleName, row.PrimaryText))
            {
                throw CoverageFailure(
                    "$.expected.automation",
                    "coverage.identity-closure");
            }
        }

        ModelInspectionExpectedAutomationControl[] modelCards =
            fixture.Expected.Automation.Controls.Where(control =>
                control.Id.Equals("model-card", StringComparison.Ordinal)).ToArray();
        if (modelCards.Length != 1 ||
            !MatchesCopy(
                modelCards[0].AccessibleName,
                fixture.Expected.Model.DisplayName))
        {
            throw CoverageFailure(
                "$.expected.automation",
                "coverage.identity-closure");
        }

        if (fixture.Expected.Content.Rows.Count > 0)
        {
            if (scrollOwner is null || fixture.Expected.Content.Heading is null)
            {
                throw CoverageFailure(
                    "$.expected.automation",
                    "coverage.identity-closure");
            }

            ModelInspectionExpectedAutomationControl[] listControls =
                fixture.Expected.Automation.Controls.Where(control =>
                    control.Id.Equals(scrollOwner, StringComparison.Ordinal)).ToArray();
            if (listControls.Length != 1 ||
                listControls[0].ControlType !=
                    ModelInspectionExpectedControlType.List ||
                !MatchesCopy(
                    listControls[0].AccessibleName,
                    fixture.Expected.Content.Heading))
            {
                throw CoverageFailure(
                    "$.expected.automation",
                    "coverage.identity-closure");
            }
        }
    }

    private static bool MatchesCopy(
        ModelInspectionExpectedCopy left,
        ModelInspectionExpectedCopy right) =>
        left.CopyKey.Equals(right.CopyKey, StringComparison.Ordinal) &&
        left.DefaultText.Equals(right.DefaultText, StringComparison.Ordinal);

    private static bool IsUnique(IEnumerable<string> values)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        return values.All(seen.Add);
    }

    private static void ValidateCopyRegistryClosure(
        ModelInspectionFixtureCatalogue catalogue)
    {
        var used = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (ModelInspectionExpectedCopy copy in catalogue.Fixtures
                     .SelectMany(fixture => EnumerateExpectedCopies(fixture.Expected)))
        {
            if (used.TryGetValue(copy.CopyKey, out string? existing))
            {
                if (!existing.Equals(copy.DefaultText, StringComparison.Ordinal))
                {
                    throw CoverageFailure(
                        "$.copyRegistry",
                        "coverage.copy-registry");
                }
            }
            else
            {
                used.Add(copy.CopyKey, copy.DefaultText);
            }
        }

        IReadOnlyDictionary<string, string> registry =
            catalogue.Policy.Value.CopyRegistry;
        if (registry.Count != 106 ||
            used.Count != registry.Count ||
            used.Any(pair =>
                !registry.TryGetValue(pair.Key, out string? value) ||
                !value.Equals(pair.Value, StringComparison.Ordinal)) ||
            !CopyRegistryHash(registry).Equals(
                AuthoritativeCopyRegistrySha256,
                StringComparison.Ordinal))
        {
            throw CoverageFailure(
                "$.copyRegistry",
                "coverage.copy-registry");
        }
    }

    private static string CopyRegistryHash(
        IReadOnlyDictionary<string, string> registry)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(
            HashAlgorithmName.SHA256);
        byte[] lengthPrefix = new byte[sizeof(int)];
        foreach ((string key, string value) in registry
                     .OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            Append(key);
            Append(value);
        }

        return Convert.ToHexString(hash.GetHashAndReset());

        void Append(string value)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(value);
            BinaryPrimitives.WriteInt32BigEndian(lengthPrefix, utf8.Length);
            hash.AppendData(lengthPrefix);
            hash.AppendData(utf8);
        }
    }

    private static IEnumerable<ModelInspectionExpectedCopy> EnumerateExpectedCopies(
        ModelInspectionExpectedScreen expected)
    {
        IEnumerable<ModelInspectionExpectedCopy?> direct =
        [
            expected.Outcome.Badge,
            expected.Outcome.Title,
            expected.Outcome.SupportingText,
            expected.Model.DisplayName,
            expected.Model.DisplayFileName,
            expected.Content.Heading
        ];
        return direct.Where(copy => copy is not null).Select(copy => copy!)
            .Concat(expected.Model.Metadata.SelectMany(field =>
                new[] { field.Label, field.Value }))
            .Concat(expected.Model.Checks.Select(row => row.Text))
            .Concat(expected.Content.Rows.SelectMany(row =>
                new[] { row.PrimaryText, row.SecondaryText }
                    .Where(copy => copy is not null)
                    .Select(copy => copy!)))
            .Concat(expected.Actions.Items.SelectMany(action =>
                new[] { action.Label, action.HelpText }
                    .Where(copy => copy is not null)
                    .Select(copy => copy!)))
            .Concat(expected.Automation.Controls.SelectMany(control =>
                new[] { control.AccessibleName, control.HelpText }
                    .Where(copy => copy is not null)
                    .Select(copy => copy!)))
            .Concat(expected.Announcements.Items);
    }

    private static string SemanticHash(JsonElement root)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(
                   buffer,
                   new JsonWriterOptions
                   {
                       Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                   }))
        {
            WriteCanonical(root, writer);
        }

        return Convert.ToHexString(SHA256.HashData(buffer.WrittenSpan));
    }

    private static void WriteCanonical(JsonElement value, Utf8JsonWriter writer)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (JsonProperty property in value.EnumerateObject()
                             .OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(property.Value, writer);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (JsonElement item in value.EnumerateArray())
                {
                    WriteCanonical(item, writer);
                }

                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(value.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(value.GetRawText(), skipInputValidation: true);
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw SchemaFailure();
        }
    }

    private sealed record ReachedEffect(
        int SetupOrdinal,
        int Attempt,
        ModelInspectionFixtureServiceEffectDescriptor Effect);

    private sealed record SetupInteractionEvent(
        int SetupOrdinal,
        int Attempt,
        ModelInspectionFixtureInteractionKind Kind);

    private sealed record StaleReleaseEvent(
        int SetupOrdinal,
        int Attempt,
        string Checkpoint,
        ModelInspectionFixtureSetupStepKind Kind);

    private readonly record struct AnimationStartBounds(
        int Minimum,
        int Maximum);

    private enum MotionRowStatus
    {
        Waiting,
        Passed,
        Active,
        Warning,
        Failed,
        Cancelled
    }

    private readonly record struct MotionRowState(
        MotionRowStatus Status,
        double? Fraction,
        string? DetailToken,
        bool DetailVisible)
    {
        internal static MotionRowState Waiting { get; } = new(
            MotionRowStatus.Waiting,
            Fraction: null,
            DetailToken: null,
            DetailVisible: false);

        internal static MotionRowState Passed { get; } = new(
            MotionRowStatus.Passed,
            Fraction: null,
            DetailToken: null,
            DetailVisible: false);
    }

    private sealed record ReplayTrace(
        ImmutableArray<ReachedEffect> Effects,
        ImmutableArray<SetupInteractionEvent> Interactions,
        ImmutableArray<StaleReleaseEvent> StaleReleases,
        ImmutableHashSet<int> ObsoleteAttempts,
        int ActiveAttempt,
        int NextServiceStep,
        ModelInspectionFixtureServiceEffectDescriptor? CurrentEffect);

    private static ModelInspectionFixtureValidationException SchemaFailure() =>
        new(
            "model-inspection-fixture.schema.json",
            "$",
            "schema.authoritative-contract");

    private static ModelInspectionFixtureValidationException CoverageFailure(
        string path,
        string rule) =>
        new(
            "model-inspection-fixture-coverage-policy.json",
            path,
            rule);
}
