using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace GraniteEdgeAI.CrossFeature.IntegrationTests;

[TestClass]
public sealed class RemediationGapContractTests
{
    private static readonly Lazy<IReadOnlySet<string>> EvaluatedAppCompileItems =
        new(EvaluateAppCompileItems);
    [TestMethod]
    public void ApplicationComposesExactlyOneOnboardingShell()
    {
        string source = ReadAppFile("MainWindow.xaml.cs");
        const string composition = "rootFrame.Navigate(typeof(OnboardingShellPage))";

        Assert.AreEqual(1, Count(source, composition));
    }

    [TestMethod]
    public void ChatStageHasNoOnboardingFooter()
    {
        string shell = ReadAppFile(
            "Features", "Onboarding", "OnboardingShellPage.xaml.cs");
        string chat = ReadAppFile("Features", "GgufRuntime", "ChatPage.xaml");

        StringAssert.Contains(shell, "value == OnboardingStage.ReadyToChat");
        StringAssert.Contains(shell, "? Visibility.Collapsed");
        Assert.IsFalse(chat.Contains("OnboardingStageIndicator", StringComparison.Ordinal));
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

        if (!button.Attributes().Any(attribute =>
                attribute.Name.LocalName is "Click" or "Command"
                && !string.IsNullOrWhiteSpace(attribute.Value)))
        {
            failures.Add("the recommended-model action has no Click or Command binding");
        }

        string downloadRoot = AppFile(
            "Features", "ModelImport", "ModelDownload");
        string catalogPath = Path.Combine(downloadRoot, "PinnedGraniteModelCatalog.cs");
        string servicePath = Path.Combine(
            downloadRoot, "ResumableVerifiedModelDownloadService.cs");
        string coordinatorPath = Path.Combine(downloadRoot, "ModelDownloadCoordinator.cs");
        RequireFile(catalogPath, "the compiled pinned catalogue", failures);
        RequireFile(servicePath, "the resumable verified download service", failures);
        RequireFile(coordinatorPath, "the cancellation/restart coordinator", failures);

        if (File.Exists(catalogPath))
        {
            RequireCompiledByAppProject(catalogPath, failures);
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
            string service = StripComments(File.ReadAllText(servicePath));
            RequireDeclaration(service, "ResumableVerifiedModelDownloadService", failures);
            foreach (string token in new[]
            {
                "CancellationToken", "Content-Range", "ExpectedByteLength",
                "ExpectedSha256", "Publish", "Recover", "Resume"
            })
            {
                if (!service.Contains(token, StringComparison.Ordinal))
                    failures.Add($"download transaction token missing: {token}");
            }
        }

        if (File.Exists(coordinatorPath))
        {
            RequireCompiledByAppProject(coordinatorPath, failures);
            string coordinator = StripComments(File.ReadAllText(coordinatorPath));
            RequireDeclaration(coordinator, "ModelDownloadCoordinator", failures);
            RequireActualInvocation(coordinator, "DownloadAsync",
                "coordinator has no executable download invocation", failures);
            RequireActualInvocation(coordinator, "SubmitInputAsync",
                "verified completion does not invoke SubmitInputAsync", failures);
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
        string shell = StripComments(File.ReadAllText(shellPath));
        string method = MethodBody(shell,
            "LaunchOptimizedChatAsync");
        string export = MethodBody(shell,
            "SaveOptimizedModelAsync");
        string selectionPath = AppFile(
            "Features", "ModelImport", "ModelImportPage.Selection.cs");
        RequireCompiledByAppProject(selectionPath, failures);
        string selection = StripComments(File.ReadAllText(selectionPath));

        RequireToken(method, "openVinoResult.ConfigurationSha256",
            "Chat does not bind the result configuration", failures);
        RequireLifecycleInvocation(method, "CreateChatTargetAsync",
            ["state.Result"], failures);
        RejectToken(method, "CancellationToken.None",
            "Chat uses CancellationToken.None instead of a lifecycle token", failures);
        RejectToken(method, "LastPublishedDirectory",
            "Chat still trusts LastPublishedDirectory", failures);
        RequireLifecycleInvocation(export, "ExportPersistentAsync",
            ["state.Result", "destination", "maximumBytes"], failures);
        RejectToken(export, "CancellationToken.None",
            "export uses CancellationToken.None instead of a lifecycle token", failures);
        RejectToken(export, "LastPublishedDirectory",
            "export still trusts LastPublishedDirectory", failures);
        string conversionMethod = MethodBody(selection, "TryRequestFolderInspection");
        RequireToken(conversionMethod, "SourceModelConversionRequested",
            "source-model selection has no conversion intent", failures);
        RejectToken(conversionMethod,
            "SourceModelConversionRequested?.Invoke(this, new OpenVinoInspectionRequestedEventArgs",
            "source conversion bypasses conversion as an inspection request", failures);
        RequireActualInvocation(conversionMethod, "ConvertAndInspectAsync",
            "source conversion intent has no real conversion-and-inspection composition",
            failures);

        Assert.AreEqual(0, failures.Count,
            "Exact result/source conversion composition gaps: "
            + string.Join("; ", failures));
    }

    [TestMethod]
    public void RuntimeOnlyOpenVinoResultCannotEnterModelFileExport()
    {
        string method = MethodBody(
            StripComments(ReadAppFile("Features", "Onboarding", "OnboardingShellPage.xaml.cs")),
            "SaveOptimizedModelAsync");

        StringAssert.Contains(method,
            "Status: OptimizationExecutionStatus.SucceededPersistent");
        Assert.IsFalse(method.Contains(
            "OptimizationExecutionStatus.SucceededRuntimeProfile",
            StringComparison.Ordinal));
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
        string chat = File.ReadAllText(chatPath);
        string composer = StripComments(File.ReadAllText(composerPath));
        string shell = StripComments(File.ReadAllText(shellPath));
        string key = Normalize(MethodBody(composer, "IsSendKey"));
        string keyHandler = Normalize(MethodBody(composer, "TryHandlePromptKeyDown"));
        string submission = Normalize(MethodBody(composer, "TrySubmitPrompt"));
        string enabledState = Normalize(MethodBody(composer, "UpdateSubmissionState"));

        StringAssert.Contains(chat, "RequestedTheme=\"Light\"");
        StringAssert.Contains(chat, "x:Name=\"SettingsFooter\"");
        StringAssert.Contains(shell, "OnboardingStage.ReadyToChat");
        StringAssert.Contains(shell, "Visibility.Collapsed");
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

    private static string MethodBody(string source, string methodName)
    {
        Match signature = Regex.Match(source,
            $@"\b(?:private|internal|public|protected)\s+(?:static\s+)?(?:async\s+)?[A-Za-z0-9_<>,?.]+\s+{Regex.Escape(methodName)}\s*\(",
            RegexOptions.CultureInvariant);
        Assert.IsTrue(signature.Success, $"Method signature not found: {methodName}");
        string code = MaskStrings(source);
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

    private static int Count(string value, string fragment)
    {
        int count = 0;
        int position = 0;
        while ((position = value.IndexOf(fragment, position,
                   StringComparison.Ordinal)) >= 0)
        {
            count++;
            position += fragment.Length;
        }
        return count;
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
        if (!source.Contains(token, StringComparison.Ordinal))
            failures.Add(failure);
    }

    private static void RejectToken(
        string source,
        string token,
        string failure,
        ICollection<string> failures)
    {
        if (source.Contains(token, StringComparison.Ordinal))
            failures.Add(failure);
    }

    private static void RequireNormalizedToken(
        string source,
        string token,
        string failure,
        ICollection<string> failures)
    {
        string normalized = string.Concat(source.Where(
            character => !char.IsWhiteSpace(character)));
        if (!normalized.Contains(token, StringComparison.Ordinal))
            failures.Add(failure);
    }

    private static void RequireDeclaration(
        string source,
        string typeName,
        ICollection<string> failures)
    {
        if (!Regex.IsMatch(MaskStrings(source),
                $@"\b(?:class|record|struct)\s+{Regex.Escape(typeName)}\b",
                RegexOptions.CultureInvariant))
        {
            failures.Add($"compiled type declaration missing: {typeName}");
        }
    }

    private static void RequireActualInvocation(
        string source,
        string methodName,
        string failure,
        ICollection<string> failures)
    {
        if (InvocationArguments(source, methodName).Count == 0)
            failures.Add(failure);
    }

    private static void RequireLifecycleInvocation(
        string method,
        string invokedMethod,
        IReadOnlyList<string> expectedPrefixArguments,
        ICollection<string> failures)
    {
        IReadOnlyList<string[]> invocations = InvocationArguments(method, invokedMethod);
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
        if (!Regex.IsMatch(lifecycleToken, @"^[A-Za-z_][A-Za-z0-9_]*$",
                RegexOptions.CultureInvariant)
            || !Regex.IsMatch(MaskStrings(method),
                $@"\bCancellationToken\s+{Regex.Escape(lifecycleToken)}\b",
                RegexOptions.CultureInvariant))
        {
            failures.Add($"{invokedMethod} must receive a declared lifecycle CancellationToken variable");
        }
    }

    private static IReadOnlyList<string[]> InvocationArguments(
        string source,
        string methodName)
    {
        string code = MaskStrings(StripComments(source));
        var invocations = new List<string[]>();
        foreach (Match match in Regex.Matches(code,
                     $@"\b{Regex.Escape(methodName)}\s*\(",
                     RegexOptions.CultureInvariant))
        {
            int open = code.IndexOf('(', match.Index);
            int close = FindBalancedClose(code, open, '(', ')');
            if (close > open)
                invocations.Add(SplitArguments(source[(open + 1)..close]));
        }
        return invocations;
    }

    private static string[] SplitArguments(string arguments)
    {
        var values = new List<string>();
        int start = 0;
        int depth = 0;
        string code = MaskStrings(arguments);
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
        var result = new StringBuilder(source);
        bool quoted = false;
        bool verbatim = false;
        bool character = false;
        for (int index = 0; index < source.Length; index++)
        {
            char current = source[index];
            char next = index + 1 < source.Length ? source[index + 1] : '\0';
            if (!character && current == '"')
            {
                if (!quoted) { quoted = true; verbatim = index > 0 && source[index - 1] == '@'; }
                else if (verbatim && next == '"') { result[index] = ' '; result[index + 1] = ' '; index++; continue; }
                else if (verbatim || index == 0 || source[index - 1] != '\\') quoted = false;
                result[index] = ' ';
                continue;
            }
            if (!quoted && current == '\''
                && (index == 0 || source[index - 1] != '\\'))
            {
                character = !character;
                result[index] = ' ';
                continue;
            }
            if (quoted || character) result[index] = current is '\r' or '\n' ? current : ' ';
        }
        return result.ToString();
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
            if (!EvaluatedAppCompileItems.Value.Contains(canonicalPath))
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

    private static IReadOnlySet<string> EvaluateAppCompileItems()
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
        return document.RootElement.GetProperty("Items").GetProperty("Compile")
            .EnumerateArray()
            .Select(item => Path.GetFullPath(item.GetProperty("FullPath").GetString()!))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

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
