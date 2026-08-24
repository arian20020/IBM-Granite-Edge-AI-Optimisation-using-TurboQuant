using System.Reflection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.Estimation;

[TestClass]
public sealed class ResourceEstimateTests
{
    private static ResourceComponent Weights(ulong bytes) =>
        ResourceComponent.Create(
            ResourceComponentKind.Weights,
            ResourceTarget.SystemMemory,
            ByteCount.FromBytes(bytes),
            new HashSet<LifecyclePhase> { LifecyclePhase.SteadyStateGeneration });

    [TestMethod]
    public void Established_CarriesComponentsAndLimitations()
    {
        ResourceEstimate estimate = ResourceEstimate.Established(
            [Weights(1000)],
            new HashSet<EstimationLimitation>
            {
                EstimationLimitation.WeightsDerivedFromFileLength
            });

        Assert.AreEqual(nameof(EstimationStatus.Established), estimate.Status.ToString());
        Assert.AreEqual(1, estimate.Components.Count);
        Assert.AreEqual(1, estimate.Limitations.Count);
        Assert.AreEqual(nameof(EstimationUnavailableReason.None), estimate.Reason.ToString());
    }

    [TestMethod]
    public void Established_RejectsAnEmptyComponentSet()
    {
        // An established estimate with no components would compose to a peak of
        // zero, which reads as "this configuration is free".
        Assert.ThrowsExactly<ArgumentException>(
            () => ResourceEstimate.Established(
                [],
                new HashSet<EstimationLimitation>()));
    }

    [TestMethod]
    public void Established_RejectsAnUnspecifiedLimitation()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => ResourceEstimate.Established(
                [Weights(1000)],
                new HashSet<EstimationLimitation> { EstimationLimitation.Unspecified }));
    }

    [TestMethod]
    public void Established_CopiesComponentsSoLaterMutationCannotChangeIt()
    {
        List<ResourceComponent> components = [Weights(1000)];

        ResourceEstimate estimate = ResourceEstimate.Established(
            components,
            new HashSet<EstimationLimitation>());

        components.Add(Weights(9999));

        Assert.AreEqual(1, estimate.Components.Count);
    }

    [TestMethod]
    public void Established_CopiesLimitationsSoLaterMutationCannotChangeIt()
    {
        HashSet<EstimationLimitation> limitations = [];

        ResourceEstimate estimate = ResourceEstimate.Established([Weights(1000)], limitations);

        limitations.Add(EstimationLimitation.UncalibratedEstimatorPolicy);

        Assert.AreEqual(0, estimate.Limitations.Count);
    }

    [TestMethod]
    public void NotEstablished_CarriesTheReasonAndNoComponents()
    {
        ResourceEstimate estimate = ResourceEstimate.NotEstablished(
            EstimationUnavailableReason.UnknownArchitecture);

        Assert.AreEqual(nameof(EstimationStatus.NotEstablished), estimate.Status.ToString());
        Assert.AreEqual(0, estimate.Components.Count);
        Assert.AreEqual(0, estimate.Limitations.Count);
        Assert.AreEqual(
            nameof(EstimationUnavailableReason.UnknownArchitecture),
            estimate.Reason.ToString());
    }

    [TestMethod]
    public void NotEstablished_RejectsTheNoneReason()
    {
        // A refusal without a reason cannot be explained to the user, and
        // section 12 requires every unavailable outcome to name its cause.
        Assert.ThrowsExactly<ArgumentException>(
            () => ResourceEstimate.NotEstablished(EstimationUnavailableReason.None));
    }

    [TestMethod]
    public void Estimate_CarriesNoStringMember()
    {
        // Privacy canary, section 14. No string member means no place for a
        // path, filename, model name or native error to hide.
        PropertyInfo[] strings = typeof(ResourceEstimate)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(property => property.PropertyType == typeof(string))
            .ToArray();

        Assert.AreEqual(
            0,
            strings.Length,
            "ResourceEstimate must expose no string member: "
            + string.Join(", ", strings.Select(property => property.Name)));
    }
}
