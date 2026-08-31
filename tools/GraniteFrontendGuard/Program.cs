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
    private const string SchemaVersion = "3";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
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
        "GettingFocus", "LosingFocus", "GotFocus", "LostFocus", "AccessKeyInvoked"
    };

    private static readonly HashSet<string> XamlContractProperties = new(StringComparer.Ordinal)
    {
        "Command", "CommandParameter", "IsEnabled", "IsChecked", "IsOn",
        "SelectedItem", "SelectedIndex", "SelectedValue", "IsOpen", "Visibility"
    };

    private static readonly string[] ProtectedRootPrefixes =
    {
        "shared/", "infrastructure/", "runtime/", "workers/", "experiments/",
        "models/", "tests/", "research/", "release-evidence/"
    };

    private static readonly string[] ProtectedSegments =
    {
        "/Contracts/", "/Domain/", "/Application/", "/Infrastructure/", "/Runtime/",
        "/Execution/", "/Storage/", "/QuickScan/", "/WorkerClient/", "/Protocol/"
    };

    private static readonly string[] BootstrapAllowedPrefixes =
    {
        ".agents/plugins/",
        ".agents/skills/",
        ".codex/agents/",
        ".frontend-worker/",
        "plugins/granite-native-frontend-worker/",
        "scripts/frontend-worker/",
        "tools/GraniteFrontendGuard/",
        "docs/frontend-worker/"
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
        Console.WriteLine($"Declaration files: {snapshot.DeclaredSymbols.Count}");
        Console.WriteLine($"Behaviour-sensitive files: {snapshot.InvocationEdges.Count}");
        Console.WriteLine($"XAML contract files: {snapshot.XamlContracts.Count}");
        return 0;
    }

    private static int RunCompare(string[] args)
    {
        string beforePath = RequireOption(args, "--before");
        string afterPath = RequireOption(args, "--after");
        string? outputPath = GetOption(args, "--output");

        FrontendSnapshot before = ReadJson<FrontendSnapshot>(beforePath);
        FrontendSnapshot after = ReadJson<FrontendSnapshot>(afterPath);
        if (before.SchemaVersion != SchemaVersion || after.SchemaVersion != SchemaVersion)
        {
            throw new InvalidDataException("Snapshot schema is stale or unsupported.");
        }

        List<ContractDifference> differences = new();
        CompareDictionary("protectedFileHashes", before.ProtectedFileHashes, after.ProtectedFileHashes, differences);
        CompareDictionary("declaredSymbols", before.DeclaredSymbols, after.DeclaredSymbols, differences);
        CompareDictionary("invocationEdges", before.InvocationEdges, after.InvocationEdges, differences);
        CompareDictionary("xamlContracts", before.XamlContracts, after.XamlContracts, differences);
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
            Console.WriteLine("PASS: protected files, declarations, behaviour-sensitive invocations, XAML contracts, and project dependencies are unchanged.");
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
        string? explicitAuthorizationPath = GetOption(args, "--authorization");
        string? outputPath = GetOption(args, "--output");

        List<string> failures = new();
        string authorizationPath = explicitAuthorizationPath
            ?? ResolveAuthorizationStatePath(repositoryRoot);

        if (File.Exists(authorizationPath))
        {
            using JsonDocument authorization = JsonDocument.Parse(File.ReadAllText(authorizationPath));
            JsonElement root = authorization.RootElement;
            bool implementationAuthorized = root.TryGetProperty("implementationAuthorized", out JsonElement authorized)
                && authorized.ValueKind == JsonValueKind.True;
            int surfaceCount = root.TryGetProperty("authorizedSurfaces", out JsonElement surfaces)
                && surfaces.ValueKind == JsonValueKind.Array
                ? surfaces.GetArrayLength()
                : 0;
            if (implementationAuthorized) failures.Add("Implementation authorization is open during bootstrap.");
            if (surfaceCount != 0) failures.Add("Bootstrap authorization contains one or more production surfaces.");
        }

        string[] changedFiles = GetChangedPaths(repositoryRoot, baseRef);
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
            Console.WriteLine("PASS: bootstrap changes are isolated and implementation authorization is closed.");
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
        SortedDictionary<string, string[]> xamlContracts = new(StringComparer.Ordinal);

        foreach (string relativePath in EnumerateRepositoryFiles(repositoryRoot))
        {
            string fullPath = Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath)) continue;

            if (IsProtectedPath(relativePath))
            {
                protectedFileHashes[relativePath] = HashFile(fullPath);
            }

            string extension = Path.GetExtension(relativePath);
            if (extension.Equals(".cs", StringComparison.OrdinalIgnoreCase))
            {
                SyntaxTree tree = CSharpSyntaxTree.ParseText(File.ReadAllText(fullPath), path: relativePath);
                SyntaxNode root = tree.GetRoot();

                string[] symbols = ExtractDeclaredSymbols(root);
                if (symbols.Length > 0)
                {
                    declaredSymbols[relativePath] = symbols;
                }

                if (IsBehaviourSensitivePath(relativePath))
                {
                    string[] edges = ExtractBehaviourEdges(root);
                    if (edges.Length > 0)
                    {
                        invocationEdges[relativePath] = edges;
                    }
                }
            }
            else if (extension.Equals(".xaml", StringComparison.OrdinalIgnoreCase))
            {
                string[] contracts = ExtractXamlContracts(fullPath);
                if (contracts.Length > 0)
                {
                    xamlContracts[relativePath] = contracts;
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
            xamlContracts,
            dependencies.PackageReferences,
            dependencies.ProjectReferences,
            dependencies.Imports);
    }

    private static string[] EnumerateRepositoryFiles(string repositoryRoot)
    {
        string output = RunGit(repositoryRoot, "ls-files", "-z", "--cached", "--others", "--exclude-standard");
        return output
            .Split('\0', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizePath)
            .Where(path => !IsExcludedPath(path))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsExcludedPath(string path)
    {
        string wrapped = $"/{path}/";
        return wrapped.Contains("/.git/", StringComparison.OrdinalIgnoreCase)
            || wrapped.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || wrapped.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
            || wrapped.Contains("/.worktrees/", StringComparison.OrdinalIgnoreCase)
            || wrapped.Contains("/TestResults/", StringComparison.OrdinalIgnoreCase)
            || wrapped.Contains("/.frontend-worker/v2/.state/", StringComparison.OrdinalIgnoreCase)
            || wrapped.Contains("/.frontend-worker/v2/providers/", StringComparison.OrdinalIgnoreCase)
            || wrapped.Contains("/.frontend-worker/v2/evidence/", StringComparison.OrdinalIgnoreCase)
            || wrapped.Contains("/.frontend-worker/v2/baselines/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProtectedPath(string path)
    {
        string normalized = NormalizePath(path);
        if (ProtectedRootPrefixes.Any(prefix => normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        string wrapped = $"/{normalized}/";
        if (ProtectedSegments.Any(segment => wrapped.Contains(segment, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

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
            || wrapped.Contains("/Controllers/", StringComparison.OrdinalIgnoreCase)
            || wrapped.Contains("/Coordinators/", StringComparison.OrdinalIgnoreCase)
            || wrapped.Contains("/Navigation/", StringComparison.OrdinalIgnoreCase)
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
            if (!HasContractVisibility(type.Modifiers)) continue;
            string typeParameters = type is TypeDeclarationSyntax declaration
                ? declaration.TypeParameterList?.ToString() ?? string.Empty
                : string.Empty;
            symbols.Add($"Type:{type.Kind()}:{ContainingTypePrefix(type)}{type.Identifier}{typeParameters}|{NormalizeModifiers(type.Modifiers)}");
        }

        foreach (DelegateDeclarationSyntax declaration in root.DescendantNodes().OfType<DelegateDeclarationSyntax>())
        {
            if (!HasContractVisibility(declaration.Modifiers)) continue;
            symbols.Add($"Delegate:{ContainingTypePrefix(declaration)}{declaration.Identifier}{declaration.TypeParameterList}({NormalizeParameters(declaration.ParameterList)}):{declaration.ReturnType}|{NormalizeModifiers(declaration.Modifiers)}");
        }

        foreach (MethodDeclarationSyntax method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            if (!HasContractVisibility(method.Modifiers)) continue;
            symbols.Add($"Method:{ContainingTypePrefix(method)}{method.Identifier}{method.TypeParameterList}({NormalizeParameters(method.ParameterList)}):{method.ReturnType}|{NormalizeModifiers(method.Modifiers)}");
        }

        foreach (ConstructorDeclarationSyntax constructor in root.DescendantNodes().OfType<ConstructorDeclarationSyntax>())
        {
            if (!HasContractVisibility(constructor.Modifiers)) continue;
            symbols.Add($"Constructor:{ContainingTypePrefix(constructor)}{constructor.Identifier}({NormalizeParameters(constructor.ParameterList)})|{NormalizeModifiers(constructor.Modifiers)}");
        }

        foreach (PropertyDeclarationSyntax property in root.DescendantNodes().OfType<PropertyDeclarationSyntax>())
        {
            if (!HasContractVisibility(property.Modifiers)) continue;
            string accessors = property.AccessorList is null
                ? "expression"
                : string.Join(',', property.AccessorList.Accessors.Select(accessor => accessor.Keyword.Text));
            symbols.Add($"Property:{ContainingTypePrefix(property)}{property.Identifier}:{property.Type}|{accessors}|{NormalizeModifiers(property.Modifiers)}");
        }

        foreach (IndexerDeclarationSyntax indexer in root.DescendantNodes().OfType<IndexerDeclarationSyntax>())
        {
            if (!HasContractVisibility(indexer.Modifiers)) continue;
            symbols.Add($"Indexer:{ContainingTypePrefix(indexer)}[{NormalizeBracketedParameters(indexer.ParameterList)}]:{indexer.Type}|{NormalizeModifiers(indexer.Modifiers)}");
        }

        foreach (EventDeclarationSyntax eventDeclaration in root.DescendantNodes().OfType<EventDeclarationSyntax>())
        {
            if (!HasContractVisibility(eventDeclaration.Modifiers)) continue;
            symbols.Add($"Event:{ContainingTypePrefix(eventDeclaration)}{eventDeclaration.Identifier}:{eventDeclaration.Type}|{NormalizeModifiers(eventDeclaration.Modifiers)}");
        }

        foreach (EventFieldDeclarationSyntax eventField in root.DescendantNodes().OfType<EventFieldDeclarationSyntax>())
        {
            if (!HasContractVisibility(eventField.Modifiers)) continue;
            foreach (VariableDeclaratorSyntax variable in eventField.Declaration.Variables)
            {
                symbols.Add($"EventField:{ContainingTypePrefix(eventField)}{variable.Identifier}:{eventField.Declaration.Type}|{NormalizeModifiers(eventField.Modifiers)}");
            }
        }

        foreach (FieldDeclarationSyntax field in root.DescendantNodes().OfType<FieldDeclarationSyntax>())
        {
            if (!HasContractVisibility(field.Modifiers)) continue;
            foreach (VariableDeclaratorSyntax variable in field.Declaration.Variables)
            {
                symbols.Add($"Field:{ContainingTypePrefix(field)}{variable.Identifier}:{field.Declaration.Type}|{NormalizeModifiers(field.Modifiers)}");
            }
        }

        foreach (OperatorDeclarationSyntax operatorDeclaration in root.DescendantNodes().OfType<OperatorDeclarationSyntax>())
        {
            if (!HasContractVisibility(operatorDeclaration.Modifiers)) continue;
            symbols.Add($"Operator:{ContainingTypePrefix(operatorDeclaration)}{operatorDeclaration.OperatorToken}({NormalizeParameters(operatorDeclaration.ParameterList)}):{operatorDeclaration.ReturnType}|{NormalizeModifiers(operatorDeclaration.Modifiers)}");
        }

        foreach (ConversionOperatorDeclarationSyntax conversion in root.DescendantNodes().OfType<ConversionOperatorDeclarationSyntax>())
        {
            if (!HasContractVisibility(conversion.Modifiers)) continue;
            symbols.Add($"Conversion:{ContainingTypePrefix(conversion)}{conversion.ImplicitOrExplicitKeyword}:{conversion.Type}({NormalizeParameters(conversion.ParameterList)})|{NormalizeModifiers(conversion.Modifiers)}");
        }

        foreach (EnumDeclarationSyntax enumeration in root.DescendantNodes().OfType<EnumDeclarationSyntax>())
        {
            if (!HasContractVisibility(enumeration.Modifiers)) continue;
            foreach (EnumMemberDeclarationSyntax member in enumeration.Members)
            {
                symbols.Add($"EnumMember:{ContainingTypePrefix(enumeration)}{enumeration.Identifier}.{member.Identifier}={member.EqualsValue?.Value.ToString() ?? "implicit"}");
            }
        }

        return CountValues(symbols);
    }

    private static string[] ExtractBehaviourEdges(SyntaxNode root)
    {
        List<string> edges = new();
        edges.AddRange(root.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Select(invocation => $"call:{NormalizeCode(invocation.ToString())}"));
        edges.AddRange(root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>()
            .Select(creation => $"new:{NormalizeCode(creation.ToString())}"));
        edges.AddRange(root.DescendantNodes().OfType<ImplicitObjectCreationExpressionSyntax>()
            .Select(creation => $"new:{NormalizeCode(creation.ToString())}"));
        edges.AddRange(root.DescendantNodes().OfType<AssignmentExpressionSyntax>()
            .Select(assignment => $"assign:{NormalizeCode(assignment.ToString())}"));
        edges.AddRange(root.DescendantNodes().OfType<PrefixUnaryExpressionSyntax>()
            .Where(expression => expression.IsKind(SyntaxKind.PreIncrementExpression) || expression.IsKind(SyntaxKind.PreDecrementExpression))
            .Select(expression => $"mutate:{NormalizeCode(expression.ToString())}"));
        edges.AddRange(root.DescendantNodes().OfType<PostfixUnaryExpressionSyntax>()
            .Where(expression => expression.IsKind(SyntaxKind.PostIncrementExpression) || expression.IsKind(SyntaxKind.PostDecrementExpression))
            .Select(expression => $"mutate:{NormalizeCode(expression.ToString())}"));
        return CountValues(edges);
    }

    private static string[] ExtractXamlContracts(string filePath)
    {
        try
        {
            XDocument document = XDocument.Load(filePath, LoadOptions.PreserveWhitespace);
            List<string> contracts = new();
            foreach (XElement element in document.Descendants())
            {
                string? identity = element.Attributes()
                    .FirstOrDefault(attribute => attribute.Name.LocalName is "Name" or "Uid")?.Value.Trim();
                string elementKey = string.IsNullOrWhiteSpace(identity)
                    ? element.Name.LocalName
                    : $"{element.Name.LocalName}[{identity}]";

                foreach (XAttribute attribute in element.Attributes())
                {
                    if (attribute.IsNamespaceDeclaration) continue;
                    string name = attribute.Name.LocalName;
                    string value = attribute.Value.Trim();
                    bool contractAttribute = KnownXamlEvents.Contains(name)
                        || XamlContractProperties.Contains(name)
                        || value.Contains("{x:Bind", StringComparison.Ordinal)
                        || value.Contains("{Binding", StringComparison.Ordinal);
                    if (!contractAttribute) continue;
                    contracts.Add($"{elementKey}.{name}={NormalizeCode(value)}");
                }
            }
            return CountValues(contracts);
        }
        catch (Exception exception) when (exception is XmlException or IOException)
        {
            return new[] { $"count:1|parse-error:{exception.GetType().Name}:{NormalizeCode(exception.Message)}" };
        }
    }

    private static string[] CountValues(IEnumerable<string> values) =>
        values
            .GroupBy(value => value, StringComparer.Ordinal)
            .Select(group => $"count:{group.Count()}|{group.Key}")
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static ProjectDependencySnapshot ExtractProjectDependencies(string repositoryRoot)
    {
        SortedSet<string> packages = new(StringComparer.Ordinal);
        SortedSet<string> projects = new(StringComparer.Ordinal);
        SortedSet<string> imports = new(StringComparer.Ordinal);

        foreach (string relative in EnumerateRepositoryFiles(repositoryRoot)
            .Where(path => Path.GetExtension(path).Equals(".csproj", StringComparison.OrdinalIgnoreCase)))
        {
            string projectFile = Path.Combine(repositoryRoot, relative.Replace('/', Path.DirectorySeparatorChar));
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

    private static string[] GetChangedPaths(string repositoryRoot, string baseRef)
    {
        string baseCommit = RunGit(repositoryRoot, "rev-parse", "--verify", $"{baseRef}^{{commit}}").Trim();
        string mergeBase = RunGit(repositoryRoot, "merge-base", "HEAD", baseCommit).Trim();
        List<string> paths = new();
        paths.AddRange(SplitLines(RunGit(repositoryRoot, "diff", "--name-only", $"{mergeBase}...HEAD")));
        paths.AddRange(SplitLines(RunGit(repositoryRoot, "diff", "--name-only")));
        paths.AddRange(SplitLines(RunGit(repositoryRoot, "diff", "--cached", "--name-only")));
        paths.AddRange(SplitLines(RunGit(repositoryRoot, "ls-files", "--others", "--exclude-standard")));
        return paths.Select(NormalizePath).Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToArray();
    }

    private static IEnumerable<string> SplitLines(string value) =>
        value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string ResolveAuthorizationStatePath(string repositoryRoot)
    {
        string policyPath = Path.Combine(repositoryRoot, ".frontend-worker", "v2", "implementation-lock.yml");
        if (!File.Exists(policyPath)) throw new FileNotFoundException("Missing implementation-lock.yml.", policyPath);
        Match match = Regex.Match(File.ReadAllText(policyPath), "(?m)^state_file:\\s*(.+?)\\s*$");
        if (!match.Success) throw new InvalidDataException("implementation-lock.yml does not declare state_file.");
        string relative = match.Groups[1].Value.Trim().Trim('"', '\'');
        return Path.GetFullPath(Path.Combine(repositoryRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static bool IsAllowedBootstrapPath(string path)
    {
        if (path is "AGENTS.md"
            or "scripts/Authorize-GraniteNativeFrontendWorkerV2.ps1"
            or "scripts/Initialize-GraniteNativeFrontendWorkerV2.ps1"
            or "scripts/Test-GraniteNativeFrontendWorkerV2.ps1"
            or "docs/superpowers/specs/2026-08-31-granite-native-frontend-worker-v2-design.md"
            or "docs/superpowers/plans/2026-08-31-granite-native-frontend-worker-v2-bootstrap.md"
            or ".github/workflows/frontend-worker-bootstrap.yml")
        {
            return true;
        }
        return BootstrapAllowedPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static string? GetAttributeByLocalName(XElement element, string localName) =>
        element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == localName)?.Value.Trim();

    private static bool HasContractVisibility(SyntaxTokenList modifiers) =>
        modifiers.Any(token => token.IsKind(SyntaxKind.PublicKeyword)
            || token.IsKind(SyntaxKind.InternalKeyword)
            || token.IsKind(SyntaxKind.ProtectedKeyword));

    private static string NormalizeModifiers(SyntaxTokenList modifiers) =>
        string.Join(' ', modifiers.Select(token => token.Text).OrderBy(value => value, StringComparer.Ordinal));

    private static string NormalizeParameters(ParameterListSyntax parameterList) =>
        string.Join(',', parameterList.Parameters.Select(parameter =>
            $"{string.Join(' ', parameter.Modifiers.Select(modifier => modifier.Text))}:{parameter.Type}:{parameter.Identifier}:{parameter.Default?.Value}"));

    private static string NormalizeBracketedParameters(BracketedParameterListSyntax parameterList) =>
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
        string candidate = Path.GetFullPath(supplied ?? Environment.CurrentDirectory);
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
        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Task.WaitAll(stdoutTask, stderrTask);
        string stdout = stdoutTask.Result;
        string stderr = stderrTask.Result;
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
        SortedDictionary<string, string[]> XamlContracts,
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
