namespace GraniteEdgeAI.EndToEndTests.Journeys;

using GraniteEdgeAI.EndToEndTests.Pages;
using GraniteEdgeAI.EndToEndTests.Automation;

[TestClass]
[TestCategory("NativeSmoke")]
public sealed class PackagedSmokeJourneys
{
    [TestMethod]
    public void Packaged_candidate_launches_with_one_onboarding_shell_and_import_action()
    {
        PackagedJourneyFixture.Run(nameof(Packaged_candidate_launches_with_one_onboarding_shell_and_import_action), session =>
        {
            Assert.IsNotNull(session.ByName("Choose model file", CancellationToken.None));
            Assert.IsNotNull(session.ByName("Continue to model inspection", CancellationToken.None));
        });
    }

    [TestMethod]
    public void Malformed_GGUF_reaches_a_terminal_failure_without_disclosing_its_path()
    {
        PackagedJourneyFixture.Run(nameof(Malformed_GGUF_reaches_a_terminal_failure_without_disclosing_its_path), session =>
        {
            string path = PackagedJourneyFixture.Require("GRANITE_E2E_MALFORMED_GGUF_PATH");
            new ImportPage(session).ChooseModel(path, CancellationToken.None);
            Assert.IsNotNull(session.ByName("Model could not be imported", CancellationToken.None));
            Assert.IsFalse(session.ExistsByName(Path.GetFullPath(path)), "The UI disclosed the guarded local asset path.");
        }, "GRANITE_E2E_MALFORMED_GGUF_PATH");
    }

    [TestMethod]
    public void Import_cancellation_returns_to_a_retryable_import_shell()
    {
        PackagedJourneyFixture.Run(nameof(Import_cancellation_returns_to_a_retryable_import_shell), session =>
        {
            new ImportPage(session).ChooseModel(PackagedJourneyFixture.Require("GRANITE_E2E_CANCEL_GGUF_PATH"), CancellationToken.None);
            AutomationSession.Invoke(session.ByName("Remove selected model and cancel quick scan", CancellationToken.None));
            Assert.IsNotNull(session.ByName("Choose model file", CancellationToken.None));
        }, "GRANITE_E2E_CANCEL_GGUF_PATH");
    }
}
