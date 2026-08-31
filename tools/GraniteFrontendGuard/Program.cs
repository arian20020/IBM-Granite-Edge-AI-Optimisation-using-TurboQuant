using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class Program
{
    private const string SchemaVersion = "2";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private static readonly HashSet<string> KnownXamlEvents = new(StringComparer.Ordinal)
    {
        "Click", "Tapped", "DoubleTapped", "RightTapped", "Loaded", "Unloaded",
        "KeyDown", "KeyUp", "CharacterReceived", "PointerPressed", "PointerReleased",
        "PointerEntered", "PointerExited", "PointerMoved", "DragEnter", "DragLeave",
        "DragOver", "Drop", "SelectionChanged", "ItemClick", "TextChanged",
        "PasswordChanged", "ValueChanged", "Checked", "Unchecked", "Toggled",
        "QuerySubmitted", "SuggestionChosen", "Closing", "Closed", "Opened",
        "SizeChanged", "LayoutUpdated", "Navigated", "NavigationFailed",
        "GettingFocus", "LosingFocus", "GotFocus", "LostFocus"
    };

    private static readonly string[] BootstrapAllowedPrefixes =
    {
        ".frontend-worker/",
        "plugins/granite-native-frontend-worker/",
        "docs/superpowers/specs/",
        "docs/superpowers/plans/",
        "tools/GraniteFrontendGuard/"
    };

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
            {
                PrintHelp();
                return 0;
            }

            return args[0] switch
            {
                "snapshot" => RunSnapshot(args[1..]),
                "compare" => RunCompare(args[1..]),
                "verify-bootstrap" => RunVerifyBootstrap(args[1..]),
                _ => throw new ArgumentException($"Unknown command: {args[0]}")
            };
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"GraniteFrontendGuard: {exception.Message}");
            return 2;
        }
    }

    private static int RunSnapshot(string[] args)
    {
        string repositoryRoot = ResolveRepositoryRoot(GetOption(args, "--repo"));
        string outputPath = RequireOption(args, "--output");
        FrontendSnapshot snapshot = CreateSnapshot(repositoryRoot);
        WriteJson(outputPath, snapshot);

        Console.WriteLine($"Snapshot written: {Path.GetFullPath(outputPath)}");
        Console.WriteLine($"HEAD: {snapshot.HeadCommit}");
        Console.WriteLine($"Protected files: {snapshot.ProtectedFileHashes.Count}");
        Console.WriteLine($"C# contract files: {snapshot.DeclaredSymbols.Count}");
        Console.WriteLine($"XAML contract files: {snapshot.XamlBindings.Count}");
        return 0;
    }

    private static int RunCompare(string[] args)
    {
        string beforePath = RequireOption(args, "--before");
        string afterPath = RequireOption(args, "--after");
        string? outputPath = GetOption(args, "--output");

        FrontendSnapshot before = ReadJson<FrontendSnapshot>(beforePath);
        FrontendSnapshot after = ReadJson<FrontendSnapshot>(afterPath);

        List<ContractDifference> differences = new();
        CompareDictionary("protectedFileHashes", before.ProtectedFileHashes, after.ProtectedFileHashes, differences);
        CompareDictionary("declaredSymbols", before.DeclaredSymbols, after.DeclaredSymbols, differences);
        CompareDictionary("xamlBindings", before.XamlBindings, after.XamlBindings, differences);
        CompareSequence("packageReferences", before.PackageReferences, after.PackageReferences, differences);
        CompareSequence("projectReferences", before.ProjectReferences, after.ProjectReferences, differences);
        CompareSequence("imports", before.Imports, after.Imports, differences);

        SnapshotComparison comparison = new(
            SchemaVersion,
            before.HeadCommit,
            after.HeadCommit,
            differences.Count == 0,
            differences);

        if (outputPath is not null)
        {
            WriteJson(outputPath, comparison);
        }

        if (comparison.Passed)
        {
            Console.WriteLine("PASS: protected files, declared contracts, XAML action bindings, and project dependencies are unchanged.");
            return 0;
        }

        Console.Error.WriteLine($"FAIL: {differences.Count} protected contract difference(s) detected.");
        foreach (ContractDifference difference in differences)
        {
            Console.Error.WriteLine($"- {difference.Category}: {difference.Key}");
            if (difference.Before is not null) Console.Error.WriteLine($"  before: {difference.Before}");
            if (difference.After is not null) Console.Error.WriteLine($"  after:  {difference.After}");
        }
        return 1;
    }

    private static int RunVerifyBootstrap(string[] args)
    {
        string repositoryRoot = ResolveRepositoryRoot(GetOption(args, "--repo"));
        string baseRef = GetOption(args, "--base") ?? "integration/ucl-cross-route-native-validation-v1";
        string authorizationPath = GetOption(args, "--authorization")
            ?? Path.Combine(repositoryRoot, ".frontend-worker", "v2", "authorization.json");
        string? outputPath = GetOption(args, "--output");

        using JsonDocument authorization = JsonDocument.Parse(File.ReadAllText(authorizationPath));
        JsonElement root = authorization.RootElement;
        bool implementationAuthorized = root.GetProperty("implementation_authorized").GetBoolean();
        int authorizedSurfaceCount = root.GetProperty("authorized_surfaces").GetArrayLength();

        List<string> failures = new();
        if (implementationAuthorized)
        {
            failures.Add("Implementation lock is open during bootstrap.");
        }
        if (authorizedSurfaceCount != 0)
        {
            failures.Add("Bootstrap authorization contains one or more production surfaces.");
        }

        string diffText = RunGit(repositoryRoot, "diff", "--name-only", $"{baseRef}...HEAD");
        string[] changedFiles = diffText
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizePath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        foreach (string path in changedFiles)
        {
            if (!IsAllowedBootstrapPath(path))
            {
                failures.Add($"Forbidden bootstrap path changed: {path}");
            }
        }

        BootstrapVerification report = new(
            SchemaVersion,
            baseRef,
            RunGit(repositoryRoot, "rev-parse", "HEAD").Trim(),
            failures.Count == 0,
            changedFiles,
            failures);

        if (outputPath is not null)
        {
            WriteJson(outputPath, report);
        }

        if (report.Passed)
        {
            Console.WriteLine("PASS: bootstrap paths are isolated and the implementation lock is closed.");
            return 0;
        }

        foreach (string failure in failures)
        {
            Console.Error.WriteLine($"FAIL: {failure}");
        }
        return 1;
    }

    private static FrontendSnapshot CreateSnapshot(string repositoryRoot)
    {
        SortedDictionary<string, string> protectedFileHashes = new(StringComparer.Ordinal);
        SortedDictionary<string, string[]> declaredSymbols = new(StringComparer.Ordinal);
        SortedDictionary<string, string[]> invocationEdges = new(StringComparer.Ordinal);
        SortedDictionary<string, string[]> xamlBindings = new(StringComparer.Ordinal);

        foreach (string filePath in EnumerateRepositoryFiles(repositoryRoot))
        {
            string relativePath = NormalizePath(Path.GetRelativePath(repositoryRoot, filePath));

            if (IsProtectedPath(relativePath))
            {
                protectedFileHashes[relativePath] = HashFile(filePath);
            }

            string extension = Path.GetExtension(filePath);
            if (extension.Equals(".cs", StringComparison.OrdinalIgnoreCase))
            {
                SyntaxTree tree = CSharpSyntaxTree.ParseText(File.ReadAllText(filePath), path: relativePath);
                SyntaxNode root = tree.GetRoot();

                string[] symbols = ExtractDeclaredSymbols(root);
                if (symbols.Length > 0)
                {
                    declaredSymbols[relativePath] = symbols;
                }

                if (IsBehaviourSensitivePath(relativePath))
                {
                    string[] invocations = ExtractInvocationEdges(root);
                    if (invocations.Length > 0)
                    {
                        invocationEdges[relativePath] = invocations;
                    }
                }
            }
            else if (extension.Equals(".xaml", StringComparison.OrdinalIgnoreCase))
            {
                string[] bindings = ExtractXamlBindings(filePath);
                if (bindings.Length > 0)
                {
                    xamlBindings[relativePath] = bindings;
                }
            }
        }

        ProjectDependencySnapshot dependencies = ExtractProjectDependencies(repositoryRoot);

        return new FrontendSnapshot(
            SchemaVersion,
            repositoryRoot,
            RunGit(repositoryRoot, "rev-parse", "HEAD").Trim(),
            protectedFileHashes,
            declaredSymbols,
            invocationEdges,
            xamlBindings,
            dependencies.PackageReferences,
            dependencies.ProjectReferences,
            dependencies.Imports);
    }

    private static IEnumerable<string> EnumerateRepositoryFiles(string repositoryRoot)
    {
        foreach (string path in Directory.EnumerateFiles(repositoryRoot, "*", SearchOption.AllDirectories))
        {
            string relativePath = NormalizePath(Path.GetRelativePath(repositoryRoot, path));
            if (IsExcludedPath(relativePath)) continue;

            string extension = Path.GetExtension(path);
            if (extension.Equals(".cs", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".xaml", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".targets", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase)
                || Path.GetFileName(path).Equals("Package.appxmanifest", StringComparison.OrdinalIgnoreCase)
                || Path.GetFileName(path).Equals("app.manifest", StringComparison.OrdinalIgnoreCase))
            {
                yield return path;
            }
        }
    }

    private static bool IsExcludedPath(string path)
    {
        string wrapped = $"/{path}/";
        return wrapped.Contains("/.git/", StringComparison.OrdinalIgnoreCase)
            || wrapped.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || wrapped.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
            || wrapped.Contains("/.worktrees/", StringComparison.OrdinalIgnoreCase)
            || wrapped.Contains("/TestResults/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProtectedPath(string path)
    {
        string normalized = NormalizePath(path);
        string[] rootPrefixes = { "shared/", "infrastructure/", "runtime/", "workers/", "experiments/" };
        if (rootPrefixes.Any(prefix => normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))) return true;

        string wrapped = $"/{normalized}/";
        string[] protectedSegments =
        {
            "/Contracts/", "/Domain/", "/Application/", "/Infrastructure/", "/Runtime/",
            "/Execution/", "/Storage/", "/QuickScan/", "/Worker", "/Protocol"
        };
        if (protectedSegments.Any(segment => wrapped.Contains(segment, StringComparison.OrdinalIgnoreCase))) return true;

        string fileName = Path.GetFileName(normalized);
        string extension = Path.GetExtension(normalized);
        return fileName.Equals("Package.appxmanifest", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("app.manifest", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("App.xaml.cs", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".targets", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBehaviourSensitivePath(string path)
    {
        string normalized = NormalizePath(path);
        string fileName = Path.GetFileName(normalized);
        string wrapped = $"/{normalized}/";

        return fileName.EndsWith(".xaml.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("MainWindow.xaml.cs", StringComparison.OrdinalIgnoreCase)
            || wrapped.Contains("/ViewModels/", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith("Controller.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith("Coordinator.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith("Scheduler.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith("StateMachine.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith("Session.cs", StringComparison.OrdinalIgnoreCase)
            || wrapped.Contains("/Presentation/", StringComparison.OrdinalIgnoreCase);
    }

    private static string[] ExtractDeclaredSymbols(SyntaxNode root)
    {
        List<string> symbols = new();

        foreach (BaseTypeDeclarationSyntax type in root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
        {
            if (!HasProtectedVisibility(type.Modifiers)) continue;
            string kind = type.Kind().ToString();
            string typeParameters = type switch
            {
                TypeDeclarationSyntax declaration => declaration.TypeParameterList?.ToString() ?? string.Empty,
                _ => string.Empty
            };
            symbols.Add($"{kind}:{ContainingTypePrefix(type)}{type.Identifier}{typeParameters}|{NormalizeModifiers(type.Modifiers)}");
        }

        foreach (DelegateDeclarationSyntax declaration in root.DescendantNodes().OfType<DelegateDeclarationSyntax>())
        {
            if (!HasProtectedVisibility(declaration.Modifiers)) continue;
            symbols.Add($"Delegate:{ContainingTypePrefix(declaration)}{declaration.Identifier}{declaration.TypeParameterList}({NormalizeParameters(declaration.ParameterList)}):{declaration.ReturnType}|{NormalizeModifiers(declaration.Modifiers)}");
        }

        foreach (MethodDeclarationSyntax method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            if (!HasProtectedVisibility(method.Modifiers)) continue;
            symbols.Add($"Method:{ContainingTypePrefix(method)}{method.Identifier}{method.TypeParameterList}({NormalizeParameters(method.ParameterList)}):{method.ReturnType}|{NormalizeModifiers(method.Modifiers)}");
        }

        foreach (ConstructorDeclarationSyntax constructor in root.DescendantNodes().OfType<ConstructorDeclarationSyntax>())
        {
            if (!HasProtectedVisibility(constructor.Modifiers)) continue;
            symbols.Add($"Constructor:{ContainingTypePrefix(constructor)}{constructor.Identifier}({NormalizeParameters(constructor.ParameterList)})|{NormalizeModifiers(constructor.Modifiers)}");
        }

        foreach (PropertyDeclarationSyntax property in root.DescendantNodes().OfType<PropertyDeclarationSyntax>())
        {
            if (!HasProtectedVisibility(property.Modifiers)) continue;
            string accessors = property.AccessorList is null
                ? "expression"
                : string.Join(',', property.AccessorList.Accessors.Select(accessor => accessor.Keyword.Text));
            symbols.Add($"Property:{ContainingTypePrefix(property)}{property.Identifier}:{property.Type}|{accessors}|{NormalizeModifiers(property.Modifiers)}");
        }

        foreach (EventDeclarationSyntax @event in root.DescendantNodes().OfType<EventDeclarationSyntax>())
        {
            if (!HasProtectedVisibility(@event.Modifiers)) continue;
            symbols.Add($"Event:{ContainingTypePrefix(@event)}{@event.Identifier}:{@event.Type}|{NormalizeModifiers(@event.Modifiers)}");
        }

        foreach (EventFieldDeclarationSyntax eventField in root.DescendantNodes().OfType<EventFieldDeclarationSyntax>())
        {
            if (!HasProtectedVisibility(eventField.Modifiers)) continue;
            foreach (VariableDeclaratorSyntax variable in eventField.Declaration.Variables)
            {
                symbols.Add($"EventField:{ContainingTypePrefix(eventField)}{variable.Identifier}:{eventField.Declaration.Type}|{NormalizeModifiers(eventField.Modifiers)}");
            }
        }

        foreach (EnumDeclarationSyntax enumeration in root.DescendantNodes().OfType<EnumDeclarationSyntax>())
        {
            if (!HasProtectedVisibility(enumeration.Modifiers)) continue;
            foreach (EnumMemberDeclarationSyntax member in enumeration.Members)
            {
                symbols.Add($"EnumMember:{ContainingTypePrefix(enumeration)}{enumeration.Identifier}.{member.Identifier}={member.EqualsValue?.Value.ToString() ?? "implicit"}");
            }
        }

        return symbols.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static string[] ExtractInvocationEdges(SyntaxNode root)
    {
        IEnumerable<string> invocations = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Select(invocation => $"call:{NormalizeCode(invocation.Expression.ToString())}");

        IEnumerable<string> constructions = root.DescendantNodes()
            .OfType<ObjectCreationExpressionSyntax>()
            .Select(creation => $"new:{NormalizeCode(creation.Type.ToString())}");

        return invocations.Concat(constructions)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] ExtractXamlBindings(string filePath)
    {
        try
        {
            XDocument document = XDocument.Load(filePath, LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace);
            List<string> bindings = new();

            foreach (XElement element in document.Descendants())
            {
                foreach (XAttribute attribute in element.Attributes())
                {
                    if (attribute.IsNamespaceDeclaration) continue;

                    string name = attribute.Name.LocalName;
                    string value = attribute.Value.Trim();
                    bool isContractAttribute = KnownXamlEvents.Contains(name)
                        || name is "Command" or "CommandParameter" or "IsEnabled" or "SelectedItem"
                        || value.Contains("{x:Bind", StringComparison.Ordinal)
                        || value.Contains("{Binding", StringComparison.Ordinal);

                    if (!isContractAttribute) continue;

                    int line = (element as IXmlLineInfo)?.LineNumber ?? 0;
                    bindings.Add($"line:{line}|{element.Name.LocalName}.{name}={NormalizeCode(value)}");
                }
            }

            return bindings.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }
        catch (Exception exception) when (exception is XmlException or IOException)
        {
            return new[] { $"parse-error:{exception.GetType().Name}:{exception.Message}" };
        }
    }

    private static ProjectDependencySnapshot ExtractProjectDependencies(string repositoryRoot)
    {
        SortedSet<string> packages = new(StringComparer.Ordinal);
        SortedSet<string> projects = new(StringComparer.Ordinal);
        SortedSet<string> imports = new(StringComparer.Ordinal);

        IEnumerable<string> projectFiles = Directory.EnumerateFiles(repositoryRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !IsExcludedPath(NormalizePath(Path.GetRelativePath(repositoryRoot, path))));

        foreach (string projectFile in projectFiles)
        {
            string relative = NormalizePath(Path.GetRelativePath(repositoryRoot, projectFile));
            XDocument document = XDocument.Load(projectFile, LoadOptions.None);

            foreach (XElement element in document.Descendants())
            {
                string? include = GetAttributeByLocalName(element, "Include") ?? GetAttributeByLocalName(element, "Update");
                string? version = GetAttributeByLocalName(element, "Version")
                    ?? element.Elements().FirstOrDefault(child => child.Name.LocalName == "Version")?.Value.Trim();

                if (element.Name.LocalName == "PackageReference" && include is not null)
                {
                    packages.Add($"{relative}|{include}|{version ?? string.Empty}");
                }
                else if (element.Name.LocalName == "ProjectReference" && include is not null)
                {
                    projects.Add($"{relative}|{NormalizePath(include)}");
                }
                else if (element.Name.LocalName == "Import")
                {
                    string? project = GetAttributeByLocalName(element, "Project");
                    if (project is not null) imports.Add($"{relative}|{NormalizePath(project)}");
                }
            }
        }

        return new ProjectDependencySnapshot(packages.ToArray(), projects.ToArray(), imports.ToArray());
    }

    private static string? GetAttributeByLocalName(XElement element, string localName) =>
        element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == localName)?.Value.Trim();

    private static bool HasProtectedVisibility(SyntaxTokenList modifiers) =>
        modifiers.Any(token => token.IsKind(SyntaxKind.PublicKeyword)
            || token.IsKind(SyntaxKind.InternalKeyword)
            || token.IsKind(SyntaxKind.ProtectedKeyword));

    private static string NormalizeModifiers(SyntaxTokenList modifiers) =>
        string.Join(' ', modifiers.Select(token => token.Text).OrderBy(value => value, StringComparer.Ordinal));

    private static string NormalizeParameters(ParameterListSyntax parameterList) =>
        string.Join(',', parameterList.Parameters.Select(parameter =>
            $"{string.Join(' ', parameter.Modifiers.Select(modifier => modifier.Text))}:{parameter.Type}:{parameter.Identifier}:{parameter.Default?.Value}"));

    private static string ContainingTypePrefix(SyntaxNode node)
    {
        IEnumerable<string> names = node.Ancestors()
            .OfType<BaseTypeDeclarationSyntax>()
            .Reverse()
            .Select(type => type.Identifier.Text);
        string prefix = string.Join('.', names);
        return string.IsNullOrEmpty(prefix) ? string.Empty : prefix + ".";
    }

    private static string NormalizeCode(string value) => Regex.Replace(value.Trim(), "\\s+", " ");

    private static string HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static bool IsAllowedBootstrapPath(string path)
    {
        if (path is "AGENTS.md" or ".agents/plugins/marketplace.json") return true;
        if (BootstrapAllowedPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.Ordinal))) return true;
        return Regex.IsMatch(path, "^scripts/(Initialize|Test|Authorize)-GraniteNativeFrontendWorkerV2\\.ps1$", RegexOptions.CultureInvariant);
    }

    private static void CompareDictionary<T>(
        string category,
        IReadOnlyDictionary<string, T> before,
        IReadOnlyDictionary<string, T> after,
        ICollection<ContractDifference> differences)
    {
        SortedSet<string> keys = new(before.Keys.Concat(after.Keys), StringComparer.Ordinal);
        foreach (string key in keys)
        {
            bool hasBefore = before.TryGetValue(key, out T? beforeValue);
            bool hasAfter = after.TryGetValue(key, out T? afterValue);
            string? beforeJson = hasBefore ? JsonSerializer.Serialize(beforeValue) : null;
            string? afterJson = hasAfter ? JsonSerializer.Serialize(afterValue) : null;
            if (!string.Equals(beforeJson, afterJson, StringComparison.Ordinal))
            {
                differences.Add(new ContractDifference(category, key, beforeJson, afterJson));
            }
        }
    }

    private static void CompareSequence(
        string category,
        IReadOnlyList<string> before,
        IReadOnlyList<string> after,
        ICollection<ContractDifference> differences)
    {
        string beforeJson = JsonSerializer.Serialize(before);
        string afterJson = JsonSerializer.Serialize(after);
        if (!string.Equals(beforeJson, afterJson, StringComparison.Ordinal))
        {
            differences.Add(new ContractDifference(category, "all", beforeJson, afterJson));
        }
    }

    private static T ReadJson<T>(string path) where T : class =>
        JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions)
        ?? throw new InvalidDataException($"Unable to deserialize {path}.");

    private static void WriteJson<T>(string outputPath, T value)
    {
        string fullPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory);
        File.WriteAllText(fullPath, JsonSerializer.Serialize(value, JsonOptions) + Environment.NewLine, new UTF8Encoding(false));
    }

    private static string ResolveRepositoryRoot(string? supplied)
    {
        string candidate = supplied ?? Environment.CurrentDirectory;
        candidate = Path.GetFullPath(candidate);

        string root = RunGit(candidate, "rev-parse", "--show-toplevel").Trim();
        if (string.IsNullOrWhiteSpace(root)) throw new InvalidOperationException("Unable to resolve repository root.");
        return Path.GetFullPath(root);
    }

    private static string RunGit(string workingDirectory, params string[] arguments)
    {
        ProcessStartInfo startInfo = new("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (string argument in arguments) startInfo.ArgumentList.Add(argument);

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Unable to start git.");
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {stderr.Trim()}");
        }
        return stdout;
    }

    private static string RequireOption(string[] args, string name) =>
        GetOption(args, name) ?? throw new ArgumentException($"Missing required option: {name}");

    private static string? GetOption(string[] args, string name)
    {
        for (int index = 0; index < args.Length; index++)
        {
            if (!string.Equals(args[index], name, StringComparison.Ordinal)) continue;
            if (index + 1 >= args.Length) throw new ArgumentException($"Missing value for {name}.");
            return args[index + 1];
        }
        return null;
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/');

    private static void PrintHelp()
    {
        Console.WriteLine("GraniteFrontendGuard commands:");
        Console.WriteLine("  snapshot --repo <path> --output <snapshot.json>");
        Console.WriteLine("  compare --before <snapshot.json> --after <snapshot.json> [--output <report.json>]");
        Console.WriteLine("  verify-bootstrap [--repo <path>] [--base <ref>] [--authorization <path>] [--output <report.json>]");
    }

    private sealed record FrontendSnapshot(
        string SchemaVersion,
        string RepositoryRoot,
        string HeadCommit,
        SortedDictionary<string, string> ProtectedFileHashes,
        SortedDictionary<string, string[]> DeclaredSymbols,
        SortedDictionary<string, string[]> InvocationEdges,
        SortedDictionary<string, string[]> XamlBindings,
        string[] PackageReferences,
        string[] ProjectReferences,
        string[] Imports);

    private sealed record ProjectDependencySnapshot(
        string[] PackageReferences,
        string[] ProjectReferences,
        string[] Imports);

    private sealed record ContractDifference(
        string Category,
        string Key,
        string? Before,
        string? After);

    private sealed record SnapshotComparison(
        string SchemaVersion,
        string BeforeCommit,
        string AfterCommit,
        bool Passed,
        IReadOnlyList<ContractDifference> Differences);

    private sealed record BootstrapVerification(
        string SchemaVersion,
        string BaseRef,
        string HeadCommit,
        bool Passed,
        IReadOnlyList<string> ChangedFiles,
        IReadOnlyList<string> Failures);
}
