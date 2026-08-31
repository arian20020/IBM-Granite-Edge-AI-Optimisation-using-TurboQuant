using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace GraniteEdgeAI.CrossFeature.IntegrationTests;

[TestClass]
public sealed class RemediationGapContractTests
{
#if DEBUG
    private const string TestConfiguration = "Debug";
#else
    private const string TestConfiguration = "Release";
#endif

    private static readonly Lazy<EvaluatedAppProjectInfo> EvaluatedAppProject =
        new(EvaluateAppProject);
    [TestMethod]
    public void MainWindowSourceCompositionNavigatesItsOwnedRootFrameToOnboarding()
    {
        var failures = new List<string>();
        string xamlPath = AppFile("MainWindow.xaml");
        string sourcePath = AppFile("MainWindow.xaml.cs");
        RequireCompiledByAppProject(sourcePath, failures);
        XDocument xaml = XDocument.Load(xamlPath);
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XElement[] rootFrames = xaml.Descendants(presentation + "Frame")
            .Where(element => (string?)element.Attribute(x + "Name") == "rootFrame")
            .ToArray();
        if (rootFrames.Length != 1)
            failures.Add("MainWindow XAML must declare exactly one rootFrame Frame");
        if ((string?)xaml.Root?.Attribute(x + "Class") != "GraniteEdgeAI.MainWindow")
            failures.Add("MainWindow XAML is not bound to the evaluated compiled MainWindow type");
        string constructor = ConstructorBody(File.ReadAllText(sourcePath), "MainWindow");
        IReadOnlyList<string[]> navigation = InvocationStatements(
            constructor, "Navigate", out bool containsLocalFunction);
        if (containsLocalFunction)
            failures.Add("MainWindow constructor contains a local function and is not accepted as direct composition");
        if (navigation.Count(arguments => arguments.Length == 1
                && Normalize(arguments[0]) == "typeof(OnboardingShellPage)") != 1)
        {
            failures.Add("MainWindow constructor must directly navigate rootFrame to OnboardingShellPage once");
        }
        if (!NormalizeCode(constructor).Contains(
                "rootFrame.Navigate(typeof(OnboardingShellPage));",
                StringComparison.Ordinal))
            failures.Add("Onboarding navigation is not issued by the owned rootFrame");
        if (EvaluatedAppProject.Value.DefineConstants.Contains(
                "COMPATIBILITY_FIXTURE_GALLERY", StringComparer.Ordinal))
        {
            failures.Add("fixture-gallery conditional is active in evaluated app constants");
        }

        Assert.AreEqual(0, failures.Count,
            "MainWindow composition gaps: " + string.Join("; ", failures));
    }

    [TestMethod]
    public void ChatXamlExcludesOnboardingIndicatorAndShellSetterCollapsesIt()
    {
        var failures = new List<string>();
        string shellPath = AppFile(
            "Features", "Onboarding", "OnboardingShellPage.xaml.cs");
        string chatCodePath = AppFile("Features", "GgufRuntime", "ChatPage.xaml.cs");
        RequireCompiledByAppProject(shellPath, failures);
        RequireCompiledByAppProject(chatCodePath, failures);
        string setter = NormalizeCode(PropertySetterBody(
            File.ReadAllText(shellPath), "CurrentStage"));
        XDocument chat = XDocument.Load(AppFile("Features", "GgufRuntime", "ChatPage.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        if ((string?)chat.Root?.Attribute(x + "Class")
            != "GraniteEdgeAI.Features.GgufRuntime.ChatPage")
            failures.Add("Chat XAML is not bound to the evaluated compiled ChatPage type");

        if (chat.Descendants().Any(element =>
                element.Name.LocalName == "OnboardingStageIndicator"))
            failures.Add("Chat XAML contains an onboarding stage indicator element");
        if (!setter.Contains(
                "StageIndicator.Visibility=value==OnboardingStage.ReadyToChat?Visibility.Collapsed:Visibility.Visible;",
                StringComparison.Ordinal))
            failures.Add("CurrentStage setter lacks the exact ReadyToChat collapse branch");
        Assert.AreEqual(0, failures.Count,
            "Chat/shell composition gaps: " + string.Join("; ", failures));
    }

    [TestMethod]
    public void RecommendedModelDownloadActionIsFunctionallyWired()
    {
        var failures = new List<string>();
        string xamlPath = AppFile(
            "Features", "ModelImport", "ModelDownload", "ModelDownloadCard.xaml");
        XElement button = XDocument.Load(xamlPath)
            .Descendants()
            .Single(element =>
                element.Attributes().Any(attribute =>
                    attribute.Name.LocalName == "Name"
                    && attribute.Value == "DownloadModelButton"));

        XAttribute? actionBinding = button.Attributes().SingleOrDefault(attribute =>
            attribute.Name.LocalName is "Click" or "Command"
            && !string.IsNullOrWhiteSpace(attribute.Value));
        if (actionBinding is null)
        {
            failures.Add("the recommended-model action has no Click or Command binding");
        }
        else
        {
            string cardCodePath = Path.ChangeExtension(xamlPath, ".xaml.cs");
            RequireCompiledByAppProject(cardCodePath, failures);
            string cardCode = File.ReadAllText(cardCodePath);
            string? actionCaller = ResolveActionCaller(
                actionBinding, cardCode, failures);
            if (actionCaller is not null)
            {
                IReadOnlyList<string[]> calls = InvocationStatements(
                    actionCaller, "StartAsync", out bool containsLocalFunction);
                if (containsLocalFunction || calls.Count != 1
                    || !NormalizeCode(actionCaller).Contains(
                        "_coordinator.StartAsync(", StringComparison.Ordinal))
                {
                    failures.Add("download action does not directly invoke the owned coordinator once");
                }
            }
        }

        string downloadRoot = AppFile(
            "Features", "ModelImport", "ModelDownload");
        string catalogPath = Path.Combine(
            RepositoryRoot(), "shared", "GraniteEdgeAI.ModelDownload.Authority",
            "PinnedGraniteModelCatalog.cs");
        string servicePath = Path.Combine(
            downloadRoot, "ResumableVerifiedModelDownloadService.cs");
        string coordinatorPath = Path.Combine(downloadRoot, "ModelDownloadCoordinator.cs");
        RequireFile(catalogPath, "the compiled pinned catalogue", failures);
        RequireFile(servicePath, "the resumable verified download service", failures);
        RequireFile(coordinatorPath, "the cancellation/restart coordinator", failures);

        if (File.Exists(catalogPath))
        {
            string authorityProject = Path.Combine(
                Path.GetDirectoryName(catalogPath)!,
                "GraniteEdgeAI.ModelDownload.Authority.csproj");
            string appBuildTargets = AppFile("Directory.Build.targets");
            RequireFile(authorityProject, "the pinned catalogue authority project", failures);
            RequireFile(appBuildTargets, "the app project authority reference", failures);
            if (File.Exists(appBuildTargets)
                && !File.ReadAllText(appBuildTargets).Contains(
                    "GraniteEdgeAI.ModelDownload.Authority.csproj",
                    StringComparison.Ordinal))
            {
                failures.Add("the app project does not reference the pinned catalogue authority");
            }
            string catalog = StripComments(File.ReadAllText(catalogPath));
            RequireDeclaration(catalog, "PinnedGraniteModelCatalog", failures);
            string[] exactPins =
            [
                "ibm-granite/granite-4.0-h-micro-GGUF",
                "51ce07a9c9cfa971ca359d9625836bf8a4a1b61f",
                "granite-4.0-h-micro-Q2_K.gguf", "1_226_247_840", "e60b313fc0ce2a0a2c3903f4399ac61fe1048c38f219bcbd9f7a708e168b8ead",
                "granite-4.0-h-micro-Q3_K_M.gguf", "1_555_472_032", "bb684177d546a2c6d3aec9cc591fc8fa75d9c16a27e798cbb2400fe52d9b7e29",
                "granite-4.0-h-micro-Q4_K_M.gguf", "1_942_564_512", "c698c78e895740f0e707eb7f8e92894f83f6d5b3f2f2f0b446dfe9635fa0063e",
                "granite-4.0-h-micro-Q5_K_M.gguf", "2_273_455_776", "69857575412143ea74d66e4d54ad70ec420d42452eadfff4a7cc043fe445ed4c",
                "granite-4.0-h-micro-Q8_0.gguf", "3_397_676_704", "a009111abf2865b7aad1e66326a6c772cddc29bccd22898f470292068b27bb59"
            ];
            foreach (string pin in exactPins)
            {
                if (!catalog.Contains(pin, StringComparison.Ordinal))
                    failures.Add($"catalogue pin missing: {pin}");
            }
        }

        if (File.Exists(servicePath))
        {
            RequireCompiledByAppProject(servicePath, failures);
            string service = File.ReadAllText(servicePath);
            RequireDeclaration(service, "ResumableVerifiedModelDownloadService", failures);
            foreach (string token in new[]
            {
                "CancellationToken", "ExpectedByteLength",
                "ExpectedSha256", "Publish", "Recover", "Resume"
            })
            {
                if (!ActiveCodeMask(service).Contains(token, StringComparison.Ordinal))
                    failures.Add($"download transaction token missing: {token}");
            }
            string downloadMethod = MethodBody(service, "DownloadAsync");
            foreach (string rangeMember in new[]
            {
                "HttpStatusCode.PartialContent", "IsValidContentRange("
            })
            {
                if (!ActiveCodeMask(downloadMethod).Contains(
                        rangeMember, StringComparison.Ordinal))
                    failures.Add($"active range validation member missing: {rangeMember}");
            }
            string rangeValidation = MethodBody(service, "IsValidContentRange");
            foreach (string rangeMember in new[]
            {
                "response.ContentRange", ".HasRange", ".From", ".To", ".Length"
            })
            {
                if (!ActiveCodeMask(rangeValidation).Contains(
                        rangeMember, StringComparison.Ordinal))
                    failures.Add($"active range validator member missing: {rangeMember}");
            }
        }

        if (File.Exists(coordinatorPath))
        {
            RequireCompiledByAppProject(coordinatorPath, failures);
            string coordinator = File.ReadAllText(coordinatorPath);
            RequireDeclaration(coordinator, "ModelDownloadCoordinator", failures);
            string coordinatorMethod = MethodBody(coordinator, "StartAsync");
            string normalizedCoordinator = NormalizeCode(coordinatorMethod);
            if (!normalizedCoordinator.Contains(
                    "_service.DownloadAsync(", StringComparison.Ordinal))
                failures.Add("coordinator StartAsync does not call the owned service");
            if (!normalizedCoordinator.Contains(
                    "result.VerifiedModelisnotnull", StringComparison.Ordinal)
                || !normalizedCoordinator.Contains(
                    "PublishVerifiedModelAvailable(", StringComparison.Ordinal))
                failures.Add("verified completion does not publish the opaque handoff event");
            int serviceCall = normalizedCoordinator.IndexOf(
                "_service.DownloadAsync(", StringComparison.Ordinal);
            int publishCall = normalizedCoordinator.IndexOf(
                "PublishVerifiedModelAvailable(", StringComparison.Ordinal);
            if (serviceCall < 0 || publishCall <= serviceCall)
                failures.Add("verified handoff publication does not follow the service call");
            RequireInvocationStatement(coordinatorMethod, "DownloadAsync",
                "coordinator has no executable service download invocation", failures);
            RequireInvocationStatement(coordinatorMethod, "PublishVerifiedModelAvailable",
                "verified completion does not publish its opaque handoff", failures);

            string importPage = ReadAppFile(
                "Features", "ModelImport", "ModelImportPage.xaml.cs");
            string verifiedHandoff = MethodBody(
                importPage, "CompleteVerifiedDownloadHandoffAsync");
            RequireToken(verifiedHandoff, "TryClaimVerifiedModel",
                "Model Import does not claim the verified model from the coordinator", failures);
            RequireInvocationStatement(verifiedHandoff, "SubmitInputAsync",
                "Model Import does not converge the claimed model through SubmitInputAsync", failures);
        }

        RequireCompiledByAppProject(xamlPath, failures, compileItem: "Page");

        Assert.AreEqual(0, failures.Count,
            "Download composition fitness gaps: " + string.Join("; ", failures));
    }

    [TestMethod]
    public void OpenVinoChatAndExportSourceCompositionRequiresExactResultAndLifecycleToken()
    {
        var failures = new List<string>();
        string shellPath = AppFile(
            "Features", "Onboarding", "OnboardingShellPage.xaml.cs");
        RequireCompiledByAppProject(shellPath, failures);
        string shell = File.ReadAllText(shellPath);
        string method = MethodBody(shell,
            "LaunchOptimizedChatAsync");
        string export = MethodBody(shell,
            "SaveOptimizedModelAsync");
        string selectionPath = AppFile(
            "Features", "ModelImport", "ModelImportPage.Selection.cs");
        RequireCompiledByAppProject(selectionPath, failures);
        string selection = File.ReadAllText(selectionPath);

        RequireToken(method, "CreateOptimizationChatTargetAsync",
            "Chat does not use the exact-result destination facade", failures);
        RequireLifecycleInvocation(method, "CreateOptimizationChatTargetAsync",
            ["state.Result"], failures);
        RejectToken(method, "CancellationToken.None",
            "Chat uses CancellationToken.None instead of a lifecycle token", failures);
        RejectToken(method, "LastPublishedDirectory",
            "Chat still trusts LastPublishedDirectory", failures);
        RequireLifecycleInvocation(export, "ExportOptimizedModelAsync",
            ["state.Result", "destination", "maximumBytes"], failures);
        RejectToken(export, "CancellationToken.None",
            "export uses CancellationToken.None instead of a lifecycle token", failures);
        RejectToken(export, "LastPublishedDirectory",
            "export still trusts LastPublishedDirectory", failures);
        string conversionIntent = MethodBody(selection, "TryRequestFolderInspection");
        RequireToken(conversionIntent, "SourceModelConversionRequested",
            "source-model selection has no conversion intent", failures);
        RejectToken(conversionIntent,
            "SourceModelConversionRequested?.Invoke(this, new OpenVinoInspectionRequestedEventArgs",
            "source conversion bypasses conversion as an inspection request", failures);
        string conversionHandler = MethodBody(
            shell, "ModelImportPage_SourceModelConversionRequested");
        RequireInvocationStatement(conversionHandler, "ConvertAndInspectAsync",
            "source conversion intent has no real conversion-and-inspection composition",
            failures);
        string conversionComposition = MethodBody(shell, "ConvertAndInspectAsync");
        RequireInvocationStatement(conversionComposition, "ConvertAsync",
            "source conversion composition does not invoke the conversion authority", failures);
        RequireInvocationStatement(
            conversionComposition, "NavigateToOpenVinoInspectionDirectoryAsync",
            "published conversion output does not converge into inspection", failures);

        Assert.AreEqual(0, failures.Count,
            "Exact result/source conversion composition gaps: "
            + string.Join("; ", failures));
    }

    [TestMethod]
    public void RuntimeOnlyOpenVinoResultCannotEnterModelFileExport()
    {
        string method = MethodBody(
            ReadAppFile("Features", "Onboarding", "OnboardingShellPage.xaml.cs"),
            "SaveOptimizedModelAsync");

        string activeMethod = ActiveCodeMask(method);
        StringAssert.Contains(activeMethod,
            "Status: OptimizationExecutionStatus.SucceededPersistent");
        Assert.IsFalse(activeMethod.Contains(
            "OptimizationExecutionStatus.SucceededRuntimeProfile",
            StringComparison.Ordinal));
    }

    [TestMethod]
    public void OptimizationDestinationsUseOneJourneyScopedFacadeAndRetainChatCustody()
    {
        string shell = ReadAppFile(
            "Features", "Onboarding", "OnboardingShellPage.xaml.cs");
        string lifecycle = ReadAppFile(
            "Features", "Onboarding",
            "OnboardingShellPage.OptimizationDestinations.cs");
        string navigation = NormalizeCode(MethodBody(
            shell, "NavigateToOptimization"));
        string chat = NormalizeCode(MethodBody(
            shell, "LaunchOptimizedChatAsync"));
        string export = NormalizeCode(MethodBody(
            shell, "SaveOptimizedModelAsync"));
        string retirement = NormalizeCode(MethodBody(
            shell, "RetireOptimizationAsync"));
        string lifecycleCode = NormalizeCode(lifecycle);

        StringAssert.Contains(navigation,
            "CreateOptimizationDestinationFacade(entry.OptimizationHandoff.Plan,backend.Executor,outputs,appRoot)");
        StringAssert.Contains(navigation,
            "BeginOptimizationDestinationLifecycle(");
        StringAssert.Contains(chat,
            "CreateOptimizationChatTargetAsync(state.Result,cancellationToken)");
        StringAssert.Contains(export,
            "ExportOptimizedModelAsync(state.Result,destination,maximumBytes,cancellationToken)");
        StringAssert.Contains(retirement,
            "RetireOptimizationDestinationLifecycleAsync()");
        StringAssert.Contains(lifecycleCode,
            "newGgufOptimizationDestinationRoute(plan,outputs,_modelSourceCustodyRegistry)");
        StringAssert.Contains(lifecycleCode,
            "newOpenVinoOptimizationDestinationRoute(openVinoExecutor)");
        StringAssert.Contains(lifecycleCode,
            "newOptimizationDestinationFacade(gguf,openVino)");
        StringAssert.Contains(lifecycleCode,
            "_activeOptimizationChatTarget");
        StringAssert.Contains(lifecycleCode,
            "RetireActiveOptimizationChatTarget");
        StringAssert.Contains(lifecycleCode,
            "CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCancellation.Token)");
        StringAssert.Contains(lifecycleCode,
            "catch(Exception)when(cancellationisnotnull)");
    }

    [TestMethod]
    public void LightShellAndChatComposerSourceCompositionHasExactKeyAndActionGuards()
    {
        var failures = new List<string>();
        string chatPath = AppFile("Features", "GgufRuntime", "ChatPage.xaml");
        string composerPath = AppFile(
            "Features", "GgufRuntime", "Controls", "ChatComposer.xaml.cs");
        string shellPath = AppFile(
            "Features", "Onboarding", "OnboardingShellPage.xaml.cs");
        RequireCompiledByAppProject(composerPath, failures);
        RequireCompiledByAppProject(shellPath, failures);
        XDocument chat = XDocument.Load(chatPath);
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        string composer = File.ReadAllText(composerPath);
        string shell = File.ReadAllText(shellPath);
        string key = NormalizeCode(MethodBody(composer, "IsSendKey"));
        string keyHandler = NormalizeCode(MethodBody(composer, "TryHandlePromptKeyDown"));
        string submission = NormalizeCode(MethodBody(composer, "TrySubmitPrompt"));
        string enabledState = NormalizeCode(MethodBody(composer, "UpdateSubmissionState"));

        Assert.AreEqual("Light", (string?)chat.Root?.Attribute("RequestedTheme"));
        Assert.AreEqual(1, chat.Descendants().Count(element =>
            (string?)element.Attribute(x + "Name") == "SettingsFooter"));
        string shellCode = NormalizeCode(shell);
        StringAssert.Contains(shellCode, "OnboardingStage.ReadyToChat");
        StringAssert.Contains(shellCode, "Visibility.Collapsed");
        StringAssert.Contains(key, "key==VirtualKey.Enter&&!isShiftPressed");
        StringAssert.Contains(keyHandler, "if(!IsSendKey(key,isShiftPressed)){returnfalse;}");
        StringAssert.Contains(keyHandler, "TrySubmitPrompt();returntrue;");
        StringAssert.Contains(submission,
            "if(IsGenerating||prompt.Length==0){returnfalse;}");
        StringAssert.Contains(submission, "SendRequested?.Invoke(this,prompt);returntrue;");
        StringAssert.Contains(enabledState,
            "SendButton.IsEnabled=!IsGenerating&&PromptTextBox.Text.Trim().Length>0;");
        Assert.AreEqual(0, failures.Count,
            "UI composition files are not owned by the evaluated app project: "
            + string.Join("; ", failures));
    }

    [TestMethod]
    public void CompositionParserRejectsDeclarationsLocalFunctionsAndLocalDefaultTokens()
    {
        const string declaration = "private Task ConvertAndInspectAsync(CancellationToken token) { return Task.CompletedTask; }";
        Assert.AreEqual(0, InvocationStatements(
            declaration, "ConvertAndInspectAsync", out bool declarationHasLocal).Count);
        Assert.IsFalse(declarationHasLocal);

        const string hidden = "private async Task CallerAsync() { async Task HiddenAsync() { await ConvertAndInspectAsync(CancellationToken.None); } await Task.CompletedTask; }";
        _ = InvocationStatements(hidden, "ConvertAndInspectAsync", out bool hasLocalFunction);
        Assert.IsTrue(hasLocalFunction,
            "A call inside a never-invoked local function cannot satisfy composition.");

        const string localDefault = "private async Task CallerAsync(State state) { CancellationToken localToken = default; await CreateChatTargetAsync(state.Result, localToken); }";
        var failures = new List<string>();
        RequireLifecycleInvocation(localDefault, "CreateChatTargetAsync",
            ["state.Result"], failures);
        Assert.AreEqual(1, failures.Count,
            "A locally defaulted token cannot establish lifecycle provenance.");

        const string fakeSignatures = "private void Real() { string method = \"private void PhantomMethod() { }\"; string constructor = \"public PhantomType() { }\"; string property = \"public Stage PhantomProperty { set { } }\"; string receivers = \"_downloadCoordinator.DownloadAsync(); _downloadService.DownloadAsync(); _modelImportPage.SubmitInputAsync(); result.IsVerified\"; }";
        Assert.IsFalse(FindMethodSignature(fakeSignatures, "PhantomMethod").Success);
        Assert.IsFalse(FindConstructorSignature(fakeSignatures, "PhantomType").Success);
        Assert.IsFalse(FindPropertySignature(fakeSignatures, "PhantomProperty").Success);
        string maskedFakeCode = NormalizeCode(fakeSignatures);
        Assert.IsFalse(maskedFakeCode.Contains("_downloadCoordinator", StringComparison.Ordinal));
        Assert.IsFalse(maskedFakeCode.Contains("_downloadService", StringComparison.Ordinal));
        Assert.IsFalse(maskedFakeCode.Contains("_modelImportPage", StringComparison.Ordinal));
        Assert.IsFalse(maskedFakeCode.Contains(".IsVerified", StringComparison.Ordinal));

        const string rawLiteral = "private void Real() { string fake = \"\"\"private void RawPhantom() { _downloadService.DownloadAsync(); }\"\"\"; }";
        const string interpolatedLiteral = "private void Real() { string fake = $\"private void InterpolatedPhantom() { _modelImportPage.SubmitInputAsync(); }\"; }";
        Assert.IsFalse(FindMethodSignature(rawLiteral, "RawPhantom").Success);
        Assert.IsFalse(FindMethodSignature(
            interpolatedLiteral, "InterpolatedPhantom").Success);
        Assert.IsFalse(NormalizeCode(rawLiteral).Contains(
            "_downloadService", StringComparison.Ordinal));
        Assert.IsFalse(NormalizeCode(interpolatedLiteral).Contains(
            "_modelImportPage", StringComparison.Ordinal));

        const string characterLiteral = "private void CharacterHost() { char close = '}'; RealInvocation(); } private void Following() { }";
        string characterHost = MethodBody(characterLiteral, "CharacterHost");
        Assert.AreEqual(1, InvocationStatements(
            characterHost, "RealInvocation", out bool characterHasLocal).Count);
        Assert.IsFalse(characterHasLocal);

        const string inactiveBranch = "#if T1_UNDEFINED_CANARY && (true || false)\nprivate void InactiveMethod() { _downloadCoordinator.DownloadAsync(); _downloadService.DownloadAsync(); _modelImportPage.SubmitInputAsync(); bool verified = result.IsVerified; }\npublic InactiveType() { }\npublic Stage InactiveProperty { set { } }\n#elif false\nprivate void AlsoInactive() { }\n#else\nprivate void ActiveFallback() { RealInvocation(); }\n#endif";
        Assert.IsFalse(FindMethodSignature(inactiveBranch, "InactiveMethod").Success);
        Assert.IsFalse(FindConstructorSignature(inactiveBranch, "InactiveType").Success);
        Assert.IsFalse(FindPropertySignature(inactiveBranch, "InactiveProperty").Success);
        string activeBranchCode = NormalizeCode(inactiveBranch);
        Assert.IsFalse(activeBranchCode.Contains("_downloadCoordinator", StringComparison.Ordinal));
        Assert.IsFalse(activeBranchCode.Contains("_downloadService", StringComparison.Ordinal));
        Assert.IsFalse(activeBranchCode.Contains("_modelImportPage", StringComparison.Ordinal));
        Assert.IsFalse(activeBranchCode.Contains(".IsVerified", StringComparison.Ordinal));
        Assert.IsTrue(FindMethodSignature(inactiveBranch, "ActiveFallback").Success);

        string evaluatedSymbol = EvaluatedAppProject.Value.DefineConstants
            .FirstOrDefault(symbol => Regex.IsMatch(
                symbol, @"^[A-Za-z_][A-Za-z0-9_]*$")) ?? "true";
        string evaluatedBranch = $"#if {evaluatedSymbol}\nprivate void EvaluatedSymbolActive() {{ RealInvocation(); }}\n#endif";
        Assert.IsTrue(FindMethodSignature(
            evaluatedBranch, "EvaluatedSymbolActive").Success);
        Assert.ThrowsExactly<FormatException>(() =>
            ActiveCodeMask("#if true &&\nprivate void Broken() { }\n#endif"));
        Assert.ThrowsExactly<FormatException>(() =>
            ActiveCodeMask("#unsupported value\nprivate void Broken() { }"));
    }

    private static string MethodBody(string source, string methodName)
    {
        string code = ActiveCodeMask(source);
        Match signature = FindMethodSignature(source, methodName);
        Assert.IsTrue(signature.Success, $"Method signature not found: {methodName}");
        int bodyStart = code.IndexOfAny(['{', '='], signature.Index + signature.Length);
        Assert.IsTrue(bodyStart >= 0, $"Method body not found: {methodName}");
        if (source[bodyStart] == '=')
        {
            int semicolon = source.IndexOf(';', bodyStart);
            Assert.IsTrue(semicolon > bodyStart, $"Expression body not closed: {methodName}");
            return source[signature.Index..(semicolon + 1)];
        }
        int depth = 0;
        for (int index = bodyStart; index < code.Length; index++)
        {
            if (code[index] == '{') depth++;
            else if (code[index] == '}' && --depth == 0)
                return source[signature.Index..(index + 1)];
        }
        Assert.Fail($"Method body not closed: {methodName}");
        return string.Empty;
    }

    private static string ConstructorBody(string source, string typeName)
    {
        Match signature = FindConstructorSignature(source, typeName);
        Assert.IsTrue(signature.Success, $"Constructor signature not found: {typeName}");
        return BracedMember(source, signature.Index, signature.Index + signature.Length,
            typeName + " constructor");
    }

    private static string PropertySetterBody(string source, string propertyName)
    {
        Match property = FindPropertySignature(source, propertyName);
        Assert.IsTrue(property.Success, $"Property not found: {propertyName}");
        string propertyBody = BracedMember(source, property.Index,
            property.Index + property.Length - 1, propertyName + " property");
        Match setter = Regex.Match(ActiveCodeMask(propertyBody),
            @"\b(?:private\s+)?set\s*\{",
            RegexOptions.CultureInvariant);
        Assert.IsTrue(setter.Success, $"Setter not found: {propertyName}");
        return BracedMember(propertyBody, setter.Index,
            setter.Index + setter.Length - 1, propertyName + " setter");
    }

    private static Match FindMethodSignature(string source, string methodName) =>
        Regex.Match(ActiveCodeMask(source),
            $@"\b(?:private|internal|public|protected)\s+(?:static\s+)?(?:async\s+)?[A-Za-z0-9_<>,?.]+\s+{Regex.Escape(methodName)}\s*\(",
            RegexOptions.CultureInvariant);

    private static Match FindConstructorSignature(string source, string typeName) =>
        Regex.Match(ActiveCodeMask(source),
            $@"\b(?:public|internal|private|protected)\s+{Regex.Escape(typeName)}\s*\(",
            RegexOptions.CultureInvariant);

    private static Match FindPropertySignature(string source, string propertyName) =>
        Regex.Match(ActiveCodeMask(source),
            $@"\b(?:public|internal|private|protected)\s+[A-Za-z0-9_<>,?.]+\s+{Regex.Escape(propertyName)}\s*\{{",
            RegexOptions.CultureInvariant);

    private static string BracedMember(
        string source,
        int memberStart,
        int searchStart,
        string description)
    {
        string code = ActiveCodeMask(source);
        int open = code.IndexOf('{', searchStart);
        Assert.IsTrue(open >= 0, $"Opening brace not found: {description}");
        int close = FindBalancedClose(code, open, '{', '}');
        Assert.IsTrue(close > open, $"Closing brace not found: {description}");
        return source[memberStart..(close + 1)];
    }

    private static string? ResolveActionCaller(
        XAttribute actionBinding,
        string code,
        ICollection<string> failures)
    {
        if (actionBinding.Name.LocalName == "Click")
        {
            if (!Regex.IsMatch(actionBinding.Value, @"^[A-Za-z_][A-Za-z0-9_]*$"))
            {
                failures.Add("download Click handler name is not an identifier");
                return null;
            }
            return MethodBody(code, actionBinding.Value);
        }

        Match command = Regex.Match(actionBinding.Value,
            @"^\{x:Bind\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\}$",
            RegexOptions.CultureInvariant);
        if (!command.Success)
        {
            failures.Add("download Command must be an exact x:Bind to an owned command");
            return null;
        }
        Match initialization = Regex.Match(ActiveCodeMask(code),
            $@"\b{Regex.Escape(command.Groups["name"].Value)}\s*=\s*new\s+[A-Za-z0-9_<>.]+\s*\(\s*(?<handler>[A-Za-z_][A-Za-z0-9_]*)\s*\)",
            RegexOptions.CultureInvariant);
        if (!initialization.Success)
        {
            failures.Add("download command does not bind an owned executable handler");
            return null;
        }
        return MethodBody(code, initialization.Groups["handler"].Value);
    }

    private static void RequireFile(
        string path,
        string description,
        ICollection<string> failures)
    {
        if (!File.Exists(path))
            failures.Add($"{description} is absent");
    }

    private static void RequireToken(
        string source,
        string token,
        string failure,
        ICollection<string> failures)
    {
        if (!ActiveCodeMask(source).Contains(token, StringComparison.Ordinal))
            failures.Add(failure);
    }

    private static void RejectToken(
        string source,
        string token,
        string failure,
        ICollection<string> failures)
    {
        if (ActiveCodeMask(source).Contains(token, StringComparison.Ordinal))
            failures.Add(failure);
    }

    private static void RequireDeclaration(
        string source,
        string typeName,
        ICollection<string> failures)
    {
        if (!Regex.IsMatch(ActiveCodeMask(source),
                $@"\b(?:class|record|struct)\s+{Regex.Escape(typeName)}\b",
                RegexOptions.CultureInvariant))
        {
            failures.Add($"compiled type declaration missing: {typeName}");
        }
    }

    private static void RequireInvocationStatement(
        string source,
        string methodName,
        string failure,
        ICollection<string> failures)
    {
        if (InvocationStatements(source, methodName, out bool containsLocalFunction).Count == 0
            || containsLocalFunction)
            failures.Add(failure);
    }

    private static void RequireInvocationExpression(
        string source,
        string methodName,
        string failure,
        ICollection<string> failures)
    {
        string code = ActiveCodeMask(source);
        int outerBody = code.IndexOf('{');
        string nestedCode = outerBody >= 0 ? code[(outerBody + 1)..] : string.Empty;
        bool containsLocalFunction = Regex.IsMatch(nestedCode,
            @"(?m)^\s*(?!if\b|for\b|foreach\b|while\b|switch\b|catch\b|using\b)(?:static\s+)?(?:async\s+)?[A-Za-z_][A-Za-z0-9_<>,?.\[\]]*\s+[A-Za-z_][A-Za-z0-9_]*\s*\([^;{}]*\)\s*(?:\{|=>)",
            RegexOptions.CultureInvariant);
        if (containsLocalFunction || !Regex.IsMatch(code,
                $@"\b{Regex.Escape(methodName)}\s*\(",
                RegexOptions.CultureInvariant))
            failures.Add(failure);
    }

    private static void RequireLifecycleInvocation(
        string method,
        string invokedMethod,
        IReadOnlyList<string> expectedPrefixArguments,
        ICollection<string> failures)
    {
        IReadOnlyList<string[]> invocations = InvocationStatements(
            method, invokedMethod, out bool containsLocalFunction);
        if (containsLocalFunction)
        {
            failures.Add($"{invokedMethod} caller contains a local function and cannot prove direct execution");
            return;
        }
        string[]? matching = invocations.SingleOrDefault(arguments =>
            arguments.Length == expectedPrefixArguments.Count + 1
            && expectedPrefixArguments.Select((expected, index) =>
                    Normalize(arguments[index]) == Normalize(expected))
                .All(match => match));
        if (matching is null)
        {
            failures.Add($"{invokedMethod} lacks exact result/destination argument structure");
            return;
        }

        string lifecycleToken = matching[^1].Trim();
        int signatureClose = ActiveCodeMask(method).IndexOf(')');
        string signature = signatureClose >= 0 ? method[..(signatureClose + 1)] : string.Empty;
        if (!Regex.IsMatch(lifecycleToken, @"^[A-Za-z_][A-Za-z0-9_]*$",
                RegexOptions.CultureInvariant)
            || lifecycleToken is "default" or "None"
            || !Regex.IsMatch(ActiveCodeMask(signature),
                $@"\bCancellationToken\s+{Regex.Escape(lifecycleToken)}\b",
                RegexOptions.CultureInvariant))
        {
            failures.Add($"{invokedMethod} must receive an enclosing method CancellationToken parameter");
        }
    }

    private static IReadOnlyList<string[]> InvocationStatements(
        string source,
        string methodName,
        out bool containsLocalFunction)
    {
        string code = ActiveCodeMask(source);
        int outerBody = code.IndexOf('{');
        string nestedCode = outerBody >= 0 ? code[(outerBody + 1)..] : string.Empty;
        containsLocalFunction = Regex.IsMatch(nestedCode,
            @"(?m)^\s*(?!if\b|for\b|foreach\b|while\b|switch\b|catch\b|using\b)(?:static\s+)?(?:async\s+)?[A-Za-z_][A-Za-z0-9_<>,?.\[\]]*\s+[A-Za-z_][A-Za-z0-9_]*\s*\([^;{}]*\)\s*(?:\{|=>)",
            RegexOptions.CultureInvariant);
        var invocations = new List<string[]>();
        foreach (Match match in Regex.Matches(code,
                     $@"\b{Regex.Escape(methodName)}\s*\(",
                     RegexOptions.CultureInvariant))
        {
            int open = code.IndexOf('(', match.Index);
            int close = FindBalancedClose(code, open, '(', ')');
            int next = close + 1;
            while (next < code.Length && char.IsWhiteSpace(code[next])) next++;
            if (close > open && next < code.Length && code[next] == ';')
                invocations.Add(SplitArguments(source[(open + 1)..close]));
        }
        return invocations;
    }

    private static string[] SplitArguments(string arguments)
    {
        var values = new List<string>();
        int start = 0;
        int depth = 0;
        string code = ActiveCodeMask(arguments);
        for (int index = 0; index < code.Length; index++)
        {
            if (code[index] is '(' or '[' or '{') depth++;
            else if (code[index] is ')' or ']' or '}') depth--;
            else if (code[index] == ',' && depth == 0)
            {
                values.Add(arguments[start..index].Trim());
                start = index + 1;
            }
        }
        values.Add(arguments[start..].Trim());
        return values.ToArray();
    }

    private static int FindBalancedClose(
        string source,
        int open,
        char openCharacter,
        char closeCharacter)
    {
        int depth = 0;
        for (int index = open; index < source.Length; index++)
        {
            if (source[index] == openCharacter) depth++;
            else if (source[index] == closeCharacter && --depth == 0) return index;
        }
        return -1;
    }

    private static string Normalize(string source) =>
        string.Concat(source.Where(character => !char.IsWhiteSpace(character)));

    private static string NormalizeCode(string source) =>
        Normalize(ActiveCodeMask(source));

    private static string ActiveCodeMask(string source) =>
        MaskStrings(MaskInactivePreprocessor(source));

    private static string MaskInactivePreprocessor(string source)
    {
        char[] result = source.ToCharArray();
        string lexical = MaskStrings(source);
        var symbols = new HashSet<string>(
            EvaluatedAppProject.Value.DefineConstants, StringComparer.Ordinal);
        var stack = new Stack<ConditionalFrame>();
        bool active = true;
        int lineStart = 0;
        while (lineStart < source.Length)
        {
            int newline = source.IndexOf('\n', lineStart);
            int lineEnd = newline < 0 ? source.Length : newline;
            string lexicalLine = lexical[lineStart..lineEnd];
            string trimmed = lexicalLine.TrimStart();
            if (trimmed.StartsWith('#'))
            {
                string directiveText = trimmed[1..].Trim();
                int separator = directiveText.IndexOfAny([' ', '\t']);
                string directive = separator < 0
                    ? directiveText
                    : directiveText[..separator];
                string argument = separator < 0
                    ? string.Empty
                    : directiveText[(separator + 1)..].Trim();
                switch (directive)
                {
                    case "if":
                    {
                        bool condition = EvaluatePreprocessorExpression(argument, symbols);
                        stack.Push(new ConditionalFrame(active, condition));
                        active = active && condition;
                        break;
                    }
                    case "elif":
                    {
                        if (stack.Count == 0) throw new FormatException("#elif without #if.");
                        ConditionalFrame frame = stack.Peek();
                        if (frame.ElseSeen) throw new FormatException("#elif after #else.");
                        bool condition = EvaluatePreprocessorExpression(argument, symbols);
                        active = frame.ParentActive && !frame.BranchTaken && condition;
                        frame.BranchTaken |= condition;
                        break;
                    }
                    case "else":
                    {
                        RequireNoDirectiveArgument(directive, argument);
                        if (stack.Count == 0) throw new FormatException("#else without #if.");
                        ConditionalFrame frame = stack.Peek();
                        if (frame.ElseSeen) throw new FormatException("Duplicate #else.");
                        frame.ElseSeen = true;
                        active = frame.ParentActive && !frame.BranchTaken;
                        frame.BranchTaken = true;
                        break;
                    }
                    case "endif":
                    {
                        RequireNoDirectiveArgument(directive, argument);
                        if (stack.Count == 0) throw new FormatException("#endif without #if.");
                        ConditionalFrame frame = stack.Pop();
                        active = frame.ParentActive;
                        break;
                    }
                    case "define":
                    case "undef":
                        if (!Regex.IsMatch(argument, @"^[A-Za-z_][A-Za-z0-9_]*$"))
                            throw new FormatException($"Malformed #{directive}.");
                        if (active)
                        {
                            if (directive == "define") symbols.Add(argument);
                            else symbols.Remove(argument);
                        }
                        break;
                    case "region":
                    case "endregion":
                    case "nullable":
                    case "pragma":
                    case "line":
                        break;
                    case "warning":
                    case "error":
                        if (active) throw new FormatException($"Active #{directive} directive.");
                        break;
                    default:
                        throw new FormatException($"Unsupported preprocessor directive: #{directive}.");
                }
                Blank(result, lineStart, lineEnd);
            }
            else if (!active)
            {
                Blank(result, lineStart, lineEnd);
            }
            lineStart = newline < 0 ? source.Length : newline + 1;
        }
        if (stack.Count != 0) throw new FormatException("Unclosed #if directive.");
        return new string(result);
    }

    private static bool EvaluatePreprocessorExpression(
        string expression,
        IReadOnlySet<string> symbols) =>
        new PreprocessorExpressionParser(expression, symbols).Parse();

    private static void RequireNoDirectiveArgument(string directive, string argument)
    {
        if (argument.Length != 0)
            throw new FormatException($"Unexpected argument after #{directive}.");
    }

    private sealed class ConditionalFrame(bool parentActive, bool branchTaken)
    {
        internal bool ParentActive { get; } = parentActive;
        internal bool BranchTaken { get; set; } = branchTaken;
        internal bool ElseSeen { get; set; }
    }

    private sealed class PreprocessorExpressionParser(
        string expression,
        IReadOnlySet<string> symbols)
    {
        private int position;

        internal bool Parse()
        {
            bool value = ParseOr();
            SkipWhitespace();
            if (position != expression.Length)
                throw new FormatException("Unsupported preprocessor expression.");
            return value;
        }

        private bool ParseOr()
        {
            bool value = ParseAnd();
            while (Take("||")) value = ParseAnd() || value;
            return value;
        }

        private bool ParseAnd()
        {
            bool value = ParseUnary();
            while (Take("&&")) value = ParseUnary() && value;
            return value;
        }

        private bool ParseUnary()
        {
            if (Take("!")) return !ParseUnary();
            if (Take("("))
            {
                bool value = ParseOr();
                if (!Take(")")) throw new FormatException("Missing preprocessor ')'.");
                return value;
            }
            string identifier = TakeIdentifier();
            return identifier switch
            {
                "true" => true,
                "false" => false,
                "" => throw new FormatException("Expected preprocessor symbol."),
                _ => symbols.Contains(identifier)
            };
        }

        private bool Take(string token)
        {
            SkipWhitespace();
            if (!expression.AsSpan(position).StartsWith(token, StringComparison.Ordinal))
                return false;
            position += token.Length;
            return true;
        }

        private string TakeIdentifier()
        {
            SkipWhitespace();
            int start = position;
            if (position < expression.Length
                && (char.IsLetter(expression[position]) || expression[position] == '_'))
            {
                position++;
                while (position < expression.Length
                       && (char.IsLetterOrDigit(expression[position])
                           || expression[position] == '_')) position++;
            }
            return expression[start..position];
        }

        private void SkipWhitespace()
        {
            while (position < expression.Length && char.IsWhiteSpace(expression[position]))
                position++;
        }
    }

    private static string StripComments(string source)
    {
        var result = new StringBuilder(source.Length);
        bool lineComment = false;
        bool blockComment = false;
        bool quoted = false;
        bool verbatim = false;
        bool character = false;
        for (int index = 0; index < source.Length; index++)
        {
            char current = source[index];
            char next = index + 1 < source.Length ? source[index + 1] : '\0';
            if (lineComment)
            {
                if (current is '\r' or '\n')
                {
                    lineComment = false;
                    result.Append(current);
                }
                else result.Append(' ');
                continue;
            }
            if (blockComment)
            {
                if (current == '*' && next == '/')
                {
                    result.Append("  ");
                    index++;
                    blockComment = false;
                }
                else result.Append(current is '\r' or '\n' ? current : ' ');
                continue;
            }
            if (!quoted && !character && current == '/' && next == '/')
            {
                result.Append("  "); index++; lineComment = true; continue;
            }
            if (!quoted && !character && current == '/' && next == '*')
            {
                result.Append("  "); index++; blockComment = true; continue;
            }
            result.Append(current);
            if (!character && current == '"')
            {
                if (!quoted) { quoted = true; verbatim = index > 0 && source[index - 1] == '@'; }
                else if (verbatim && next == '"') { result.Append(next); index++; }
                else if (verbatim || index == 0 || source[index - 1] != '\\') quoted = false;
            }
            else if (!quoted && current == '\''
                     && (index == 0 || source[index - 1] != '\\'))
            {
                character = !character;
            }
        }
        return result.ToString();
    }

    private static string MaskStrings(string source)
    {
        char[] result = source.ToCharArray();
        for (int index = 0; index < source.Length; index++)
        {
            char current = source[index];
            char next = index + 1 < source.Length ? source[index + 1] : '\0';
            if (current == '/' && next == '/')
            {
                int end = source.IndexOfAny(['\r', '\n'], index + 2);
                if (end < 0) end = source.Length;
                Blank(result, index, end);
                index = end - 1;
                continue;
            }
            if (current == '/' && next == '*')
            {
                int closing = source.IndexOf("*/", index + 2, StringComparison.Ordinal);
                int end = closing < 0 ? source.Length : closing + 2;
                Blank(result, index, end);
                index = end - 1;
                continue;
            }
            if (current == '\'')
            {
                int end = index + 1;
                while (end < source.Length)
                {
                    if (source[end] == '\\') end += 2;
                    else if (source[end++] == '\'') break;
                }
                Blank(result, index, Math.Min(end, source.Length));
                index = Math.Min(end, source.Length) - 1;
                continue;
            }

            int quote = -1;
            int prefixEnd = index;
            if (current == '"') quote = index;
            else if (current is '$' or '@')
            {
                while (prefixEnd < source.Length && source[prefixEnd] is '$' or '@')
                    prefixEnd++;
                if (prefixEnd < source.Length && source[prefixEnd] == '"')
                    quote = prefixEnd;
            }
            if (quote < 0) continue;

            int quoteCount = 0;
            while (quote + quoteCount < source.Length
                   && source[quote + quoteCount] == '"') quoteCount++;
            bool raw = quoteCount >= 3;
            bool verbatim = source[index..quote].Contains('@');
            int stringEnd = quote + (raw ? quoteCount : 1);
            if (raw)
            {
                while (stringEnd < source.Length)
                {
                    int run = 0;
                    while (stringEnd + run < source.Length
                           && source[stringEnd + run] == '"') run++;
                    if (run >= quoteCount) { stringEnd += quoteCount; break; }
                    stringEnd += Math.Max(1, run);
                }
            }
            else
            {
                while (stringEnd < source.Length)
                {
                    if (!verbatim && source[stringEnd] == '\\') stringEnd += 2;
                    else if (verbatim && source[stringEnd] == '"'
                             && stringEnd + 1 < source.Length
                             && source[stringEnd + 1] == '"') stringEnd += 2;
                    else if (source[stringEnd] == '"') { stringEnd++; break; }
                    else stringEnd++;
                }
            }
            stringEnd = Math.Min(stringEnd, source.Length);
            Blank(result, index, stringEnd);
            index = stringEnd - 1;
        }
        return new string(result);
    }

    private static void Blank(char[] value, int start, int end)
    {
        for (int index = start; index < end; index++)
            if (value[index] is not ('\r' or '\n')) value[index] = ' ';
    }

    private static void RequireCompiledByAppProject(
        string path,
        ICollection<string> failures,
        string compileItem = "Compile")
    {
        string appRoot = AppFile();
        string canonicalPath = Path.GetFullPath(path);
        if (!canonicalPath.StartsWith(
                Path.GetFullPath(appRoot) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
        {
            failures.Add($"source is outside the app project: {Path.GetFileName(path)}");
            return;
        }

        XDocument project = XDocument.Load(AppFile("IBM Granite with TurboQuant (Intel).csproj"));
        string relative = Path.GetRelativePath(appRoot, canonicalPath).Replace('/', '\\');
        if (compileItem == "Compile")
        {
            if (!EvaluatedAppProject.Value.CompileItems.Contains(canonicalPath))
                failures.Add($"evaluated app Compile items exclude {relative}");
            return;
        }

        bool explicitlyUpdated = project.Descendants().Any(element =>
            element.Name.LocalName == compileItem
            && element.Attribute("Update") is { } update
            && string.Equals(update.Value.Replace('/', '\\'), relative,
                StringComparison.OrdinalIgnoreCase));
        if (!explicitlyUpdated)
            failures.Add($"app project has no explicit {compileItem} ownership for {relative}");

        string enableProperty = compileItem == "Compile"
            ? "EnableDefaultCompileItems"
            : "EnableDefaultPageItems";
        if (project.Descendants().Any(element =>
                element.Name.LocalName == enableProperty
                && string.Equals(element.Value.Trim(), "false", StringComparison.OrdinalIgnoreCase)))
        {
            failures.Add($"{enableProperty} disables ownership of {Path.GetFileName(path)}");
        }
        if (project.Descendants().Any(element =>
                element.Name.LocalName == compileItem
                && element.Attribute("Remove") is { } remove
                && string.Equals(remove.Value.Replace('/', '\\'), relative,
                    StringComparison.OrdinalIgnoreCase)))
        {
            failures.Add($"app project removes {relative} from {compileItem}");
        }
    }

    private static EvaluatedAppProjectInfo EvaluateAppProject()
    {
        string projectPath = AppFile("IBM Granite with TurboQuant (Intel).csproj");
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = RepositoryRoot()
        };
        start.ArgumentList.Add("msbuild");
        start.ArgumentList.Add(projectPath);
        start.ArgumentList.Add("-getItem:Compile");
        start.ArgumentList.Add("-getProperty:DefineConstants");
        start.ArgumentList.Add("-p:Configuration=" + TestConfiguration);
        start.ArgumentList.Add("-p:Platform=x64");
        start.ArgumentList.Add("-p:RuntimeIdentifier=win-x64");
        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("Could not start app project evaluation.");
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30_000))
        {
            process.Kill(entireProcessTree: true);
            Assert.Fail("App project Compile evaluation exceeded 30 seconds.");
        }
        string output = outputTask.GetAwaiter().GetResult();
        string error = errorTask.GetAwaiter().GetResult();
        Assert.AreEqual(0, process.ExitCode,
            "App project Compile evaluation failed: " + error);
        using JsonDocument document = JsonDocument.Parse(output);
        HashSet<string> compileItems = document.RootElement
            .GetProperty("Items").GetProperty("Compile")
            .EnumerateArray()
            .Select(item => Path.GetFullPath(item.GetProperty("FullPath").GetString()!))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        string constantsValue = document.RootElement.GetProperty("Properties")
            .GetProperty("DefineConstants").GetString() ?? string.Empty;
        string[] constants = constantsValue.Split(
            [';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new EvaluatedAppProjectInfo(compileItems, constants);
    }

    private sealed record EvaluatedAppProjectInfo(
        IReadOnlySet<string> CompileItems,
        IReadOnlyList<string> DefineConstants);

    private static string ReadAppFile(params string[] segments) =>
        File.ReadAllText(AppFile(segments));

    private static string AppFile(params string[] segments) =>
        Path.Combine([RepositoryRoot(), "IBM Granite with TurboQuant (Intel)", .. segments]);

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(
                    directory.FullName,
                    "IBM Granite with TurboQuant (Intel).slnx")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
