using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.Contracts.Tests;

[TestClass]
[TestCategory("Architecture")]
[TestCategory("Contract")]
[TestCategory("RetryRestartContract")]
public sealed class ModelInspectionFixtureRetryRestartContractTests
{
    private static readonly string Root = FindRepositoryRoot();

    [TestMethod]
    [DataRow("MI-012-cancelled-cooperative-user-cancellation.fixture.json",
        "restart", "restart")]
    [DataRow("MI-013-operational-failure-worker-start-failure.fixture.json",
        "retry", "retry")]
    [DataRow("MI-029-cancellation-requested-cooperative-cancelled.fixture.json",
        "restart", "restart")]
    [DataRow("MI-030-cancellation-forced-operational-failure.fixture.json",
        "retry", "retry")]
    [DataRow("MI-039-operational-failure-worker-timeout.fixture.json",
        "retry", "retry")]
    [DataRow("MI-040-operational-failure-worker-crash-early-exit.fixture.json",
        "retry", "retry")]
    [DataRow("MI-041-operational-failure-malformed-worker-response.fixture.json",
        "retry", "retry")]
    [DataRow("MI-042-operational-failure-cancellation-unconfirmed.fixture.json",
        "retry", "retry")]
    [DataRow("MI-049-operational-failure-detail-copy-maximum.fixture.json",
        "retry", "retry")]
    public void ObservedRetryOrRestartDeclaresFreshAttemptAndCanonicalDestination(
        string fileName,
        string interactionId,
        string interactionKind)
    {
        JsonObject descriptor = LoadDescriptor(fileName);

        AssertRetryRestartContract(descriptor, interactionId, interactionKind);
    }

    [TestMethod]
    [DataRow("MI-012-cancelled-cooperative-user-cancellation.fixture.json",
        "restart", "restart", "delete-attempt-2")]
    [DataRow("MI-012-cancelled-cooperative-user-cancellation.fixture.json",
        "restart", "restart", "self-target")]
    [DataRow("MI-012-cancelled-cooperative-user-cancellation.fixture.json",
        "restart", "restart", "wrong-terminal")]
    [DataRow("MI-012-cancelled-cooperative-user-cancellation.fixture.json",
        "restart", "restart", "wrong-evidence")]
    [DataRow("MI-012-cancelled-cooperative-user-cancellation.fixture.json",
        "restart", "restart", "wrong-footer")]
    [DataRow("MI-012-cancelled-cooperative-user-cancellation.fixture.json",
        "restart", "restart", "initial-setup-release")]
    [DataRow("MI-013-operational-failure-worker-start-failure.fixture.json",
        "retry", "retry", "delete-attempt-2")]
    [DataRow("MI-013-operational-failure-worker-start-failure.fixture.json",
        "retry", "retry", "self-target")]
    [DataRow("MI-013-operational-failure-worker-start-failure.fixture.json",
        "retry", "retry", "wrong-terminal")]
    [DataRow("MI-013-operational-failure-worker-start-failure.fixture.json",
        "retry", "retry", "wrong-evidence")]
    [DataRow("MI-013-operational-failure-worker-start-failure.fixture.json",
        "retry", "retry", "wrong-footer")]
    [DataRow("MI-013-operational-failure-worker-start-failure.fixture.json",
        "retry", "retry", "initial-setup-release")]
    [DataRow("MI-039-operational-failure-worker-timeout.fixture.json",
        "retry", "retry", "delete-attempt-2")]
    [DataRow("MI-039-operational-failure-worker-timeout.fixture.json",
        "retry", "retry", "self-target")]
    [DataRow("MI-039-operational-failure-worker-timeout.fixture.json",
        "retry", "retry", "wrong-terminal")]
    [DataRow("MI-039-operational-failure-worker-timeout.fixture.json",
        "retry", "retry", "wrong-evidence")]
    [DataRow("MI-039-operational-failure-worker-timeout.fixture.json",
        "retry", "retry", "wrong-footer")]
    [DataRow("MI-039-operational-failure-worker-timeout.fixture.json",
        "retry", "retry", "initial-setup-release")]
    public void RetryRestartContractRejectsMutation(
        string fileName,
        string interactionId,
        string interactionKind,
        string mutation)
    {
        JsonObject descriptor = LoadDescriptor(fileName);
        AssertRetryRestartContract(descriptor, interactionId, interactionKind);

        JsonObject interaction = descriptor["interactions"]!.AsArray()
            .Select(node => node!.AsObject())
            .Single(item => item["id"]!.GetValue<string>() == interactionId);
        JsonObject effect = descriptor["input"]!["attempts"]![1]![
            "serviceSteps"]![0]!["effect"]!.AsObject();
        switch (mutation)
        {
            case "delete-attempt-2":
                descriptor["input"]!["attempts"]!.AsArray().RemoveAt(1);
                break;
            case "self-target":
                interaction["target"] = descriptor["id"]!.GetValue<string>();
                break;
            case "wrong-terminal":
                effect["kind"] = "operationalFailure";
                break;
            case "wrong-evidence":
                effect["evidenceProfile"] = "missingChatTemplate";
                break;
            case "wrong-footer":
                interaction["expectedFooterStatus"] = "interrupted";
                break;
            case "initial-setup-release":
                descriptor["input"]!["setupSteps"]!.AsArray().Insert(
                    1,
                    new JsonObject
                    {
                        ["kind"] = "release-service-checkpoint",
                        ["attempt"] = 2,
                        ["checkpoint"] = "attempt-2-ready",
                        ["interactionId"] = null
                    });
                break;
            default:
                Assert.Fail($"Unknown retry/restart mutation: {mutation}");
                break;
        }

        Assert.ThrowsExactly<AssertFailedException>(() =>
            AssertRetryRestartContract(descriptor, interactionId, interactionKind));
    }

    private static void AssertRetryRestartContract(
        JsonObject descriptor,
        string interactionId,
        string interactionKind)
    {
        string id = descriptor["id"]!.GetValue<string>();
        JsonObject input = descriptor["input"]!.AsObject();
        Assert.AreEqual("observed",
            input["observationCheckpoint"]!.GetValue<string>(), id);

        JsonObject actions = descriptor["expected"]!["actions"]!.AsObject();
        Assert.IsTrue(actions["visible"]!.GetValue<bool>(), id);
        JsonObject action = actions["items"]!
            .AsArray()
            .Select(node => node!.AsObject())
            .Single(item => item["id"]!.GetValue<string>() == interactionId);
        Assert.IsTrue(action["visible"]!.GetValue<bool>(), id);
        Assert.IsTrue(action["enabled"]!.GetValue<bool>(), id);

        JsonArray attempts = input["attempts"]!.AsArray();
        Assert.AreEqual(2, attempts.Count, id);
        JsonObject attempt2 = attempts[1]!.AsObject();
        AssertExactProperties(attempt2, ["attempt", "serviceSteps"], id);
        Assert.AreEqual(2, attempt2["attempt"]!.GetValue<int>(), id);

        JsonArray serviceSteps = attempt2["serviceSteps"]!.AsArray();
        Assert.AreEqual(1, serviceSteps.Count, id);
        JsonObject serviceStep = serviceSteps[0]!.AsObject();
        AssertExactProperties(serviceStep, ["trigger", "effect"], id);

        JsonObject trigger = serviceStep["trigger"]!.AsObject();
        AssertExactProperties(trigger, ["kind", "checkpoint"], id);
        Assert.AreEqual("checkpoint", trigger["kind"]!.GetValue<string>(), id);
        Assert.AreEqual("attempt-2-ready",
            trigger["checkpoint"]!.GetValue<string>(), id);

        JsonObject effect = serviceStep["effect"]!.AsObject();
        AssertExactProperties(
            effect,
            ["kind", "progress", "outcome", "evidenceProfile",
                "failureProfile", "failureDetailProfile", "deferredCheckpoint"],
            id);
        Assert.AreEqual("completed", effect["kind"]!.GetValue<string>(), id);
        AssertExplicitNull(effect, "progress", id);
        Assert.AreEqual("ready", effect["outcome"]!.GetValue<string>(), id);
        Assert.AreEqual("compatible",
            effect["evidenceProfile"]!.GetValue<string>(), id);
        AssertExplicitNull(effect, "failureProfile", id);
        AssertExplicitNull(effect, "failureDetailProfile", id);
        AssertExplicitNull(effect, "deferredCheckpoint", id);

        Assert.IsFalse(input["setupSteps"]!.AsArray()
            .Select(node => node!.AsObject())
            .Any(step => step["kind"]!.GetValue<string>() ==
                    "release-service-checkpoint" &&
                step["attempt"]?.GetValue<int>() == 2), id);

        JsonObject interaction = descriptor["interactions"]!.AsArray()
            .Select(node => node!.AsObject())
            .Single(item => item["id"]!.GetValue<string>() == interactionId);
        Assert.AreEqual(interactionKind,
            interaction["kind"]!.GetValue<string>(), id);
        Assert.AreEqual("observed",
            interaction["sourceCheckpoint"]!.GetValue<string>(), id);
        Assert.AreEqual("MI-002", interaction["target"]!.GetValue<string>(), id);
        Assert.AreEqual("choose-another",
            interaction["expectedFocus"]!.GetValue<string>(), id);
        Assert.AreEqual(1,
            interaction["expectedAnnouncementCount"]!.GetValue<int>(), id);
        Assert.AreEqual("complete",
            interaction["expectedFooterStatus"]!.GetValue<string>(), id);
        Assert.AreEqual("none",
            interaction["lifetimeEffect"]!.GetValue<string>(), id);
    }

    private static void AssertExplicitNull(
        JsonObject value,
        string property,
        string id)
    {
        Assert.IsTrue(value.ContainsKey(property), id);
        Assert.IsNull(value[property], id);
    }

    private static void AssertExactProperties(
        JsonObject value,
        string[] expected,
        string id) => CollectionAssert.AreEquivalent(
            expected,
            value.Select(item => item.Key).ToArray(),
            id);

    private static JsonObject LoadDescriptor(string fileName) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(
            Root,
            "tests",
            "TestFixtures",
            "ModelInspectionScenarios",
            fileName)))!.AsObject();

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(
                    current.FullName,
                    "IBM Granite with TurboQuant (Intel).slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
