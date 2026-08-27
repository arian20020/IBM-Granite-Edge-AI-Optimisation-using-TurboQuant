using GraniteEdgeAI.Features.OpenVinoRoute.TurboQuant;
using GraniteEdgeAI.Features.Prompting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.OpenVino.Tests.TurboQuant;

[TestClass]
[TestCategory("OpenVinoRoute")]
public sealed class TurboQuantFallbackServiceTests
{
    private static readonly string[] ExpectedRoutes =
        ["openvino.official", "gguf.local"];

    [TestMethod]
    public void OffersVerifiedOfficialFirstAndApplicableGgufSecondWithoutExecution()
    {
        RecordingTarget official = Target("openvino.official", PromptRouteKind.OpenVino);
        RecordingTarget gguf = Target("gguf.local", PromptRouteKind.Gguf);
        TurboQuantFallbackService service = new(official.Target, gguf.Target);

        CollectionAssert.AreEqual(
            ExpectedRoutes,
            service.Offers.Select(offer => offer.RouteId).ToArray());
        Assert.AreEqual(0, official.CallCount);
        Assert.AreEqual(0, gguf.CallCount);
    }

    [TestMethod]
    public async Task ConfirmationIsRequiredAndRunsOnlyTheChosenRealRoute()
    {
        RecordingTarget official = Target("openvino.official", PromptRouteKind.OpenVino);
        RecordingTarget gguf = Target("gguf.local", PromptRouteKind.Gguf);
        TurboQuantFallbackService service = new(official.Target, gguf.Target);
        TurboQuantFallbackOffer chosen = service.Offers[0];

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            service.ConfirmAsync(chosen.OfferId, userConfirmed: false, _ => { }, CancellationToken.None));
        Assert.AreEqual(0, official.CallCount);

        PromptRouteSessionActivation activation = await service.ConfirmAsync(
            chosen.OfferId,
            userConfirmed: true,
            _ => { },
            CancellationToken.None);

        Assert.AreEqual(1, official.CallCount);
        Assert.AreEqual(0, gguf.CallCount);
        Assert.AreEqual("openvino.official", activation.Session.Capability.RouteId);
        Assert.IsFalse(activation.Presentation.CapabilitySummary.Contains(
            "TurboQuant", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void UnverifiedOrInapplicableFallbackIsNotOffered()
    {
        RecordingTarget official = Target(
            "openvino.official", PromptRouteKind.OpenVino, verified: false);
        RecordingTarget gguf = Target(
            "gguf.local", PromptRouteKind.Gguf, applicable: false);

        TurboQuantFallbackService service = new(official.Target, gguf.Target);

        Assert.IsEmpty(service.Offers);
    }

    [TestMethod]
    public async Task TargetCannotRetainTurboQuantBrandingOrSwitchRouteIdentity()
    {
        RecordingTarget branded = Target(
            "openvino.official",
            PromptRouteKind.OpenVino,
            presentation: new PromptRoutePresentation(
                "TurboQuant remained active", "CPU", "verified", "ready"));
        TurboQuantFallbackService service = new(branded.Target, null);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            service.ConfirmAsync(
                service.Offers.Single().OfferId,
                userConfirmed: true,
                _ => { },
                CancellationToken.None));
        Assert.AreEqual(1, branded.Session.DisposeCount);
    }

    [TestMethod]
    public async Task ConfirmedOfferIsSingleUse()
    {
        RecordingTarget official = Target("openvino.official", PromptRouteKind.OpenVino);
        TurboQuantFallbackService service = new(official.Target, null);
        Guid offerId = service.Offers.Single().OfferId;

        _ = await service.ConfirmAsync(
            offerId, userConfirmed: true, _ => { }, CancellationToken.None);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            service.ConfirmAsync(
                offerId, userConfirmed: true, _ => { }, CancellationToken.None));
        Assert.AreEqual(1, official.CallCount);
    }

    private static RecordingTarget Target(
        string routeId,
        PromptRouteKind kind,
        bool verified = true,
        bool applicable = true,
        PromptRoutePresentation? presentation = null)
    {
        PromptRouteCapability capability = new(
            kind, routeId, routeId + ".cpu", routeId, "CPU", "Verified",
            4096, 128, 128);
        FakeSession session = new(capability);
        RecordingTarget result = new();
        result.Session = session;
        result.Target = new TurboQuantFallbackTarget(
            capability,
            verified,
            applicable,
            async (sink, token) =>
            {
                result.CallCount++;
                await Task.Yield();
                return new PromptRouteSessionActivation(
                    session,
                    presentation ?? new PromptRoutePresentation(
                        routeId, "Requested CPU · Running CPU", "Verified", routeId + " ready"));
            });
        return result;
    }

    private sealed class RecordingTarget
    {
        public TurboQuantFallbackTarget Target { get; set; } = null!;
        public FakeSession Session { get; set; } = null!;
        public int CallCount { get; set; }
    }

    private sealed class FakeSession(PromptRouteCapability capability) : IPromptRouteSession
    {
        public int DisposeCount { get; private set; }
        public PromptRouteCapability Capability { get; } = capability;
        public Task<PromptTurnResult> GenerateAsync(string prompt, int requestedNewTokens, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task CancelAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task CloseAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}
