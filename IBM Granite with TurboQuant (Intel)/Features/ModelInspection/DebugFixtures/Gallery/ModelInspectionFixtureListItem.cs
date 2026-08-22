#if MODEL_INSPECTION_FIXTURE_GALLERY
using GraniteEdgeAI.ModelInspection.Fixtures;
using System;
using System.Collections.Generic;

namespace GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Gallery;

internal sealed class ModelInspectionFixtureListItem
{
    internal ModelInspectionFixtureListItem(
        ValidatedModelInspectionFixture fixture,
        bool hasN001RealWorkerCoverage)
    {
        Fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
        HasN001RealWorkerCoverage = hasN001RealWorkerCoverage;
    }

    internal ValidatedModelInspectionFixture Fixture { get; }

    internal ReadOnlyMemory<byte> RawUtf8 => Fixture.RawUtf8.AsMemory();

    internal string Id => Fixture.Id;

    internal string FileName => Fixture.FileName;

    internal string TargetCondition => Fixture.TargetCondition;

    internal ModelInspectionFixtureCategory Category => Fixture.Category;

    internal bool HasN001RealWorkerCoverage { get; }

    internal IReadOnlyList<ModelInspectionFixtureInteraction>
        VisibleInteractions => Fixture.VisibleInteractions;

    internal string RealWorkerCoverageLabel =>
        HasN001RealWorkerCoverage
            ? "Real-worker coverage: N-001"
            : string.Empty;

    public override string ToString() => $"{Id}  {TargetCondition}";
}
#endif
