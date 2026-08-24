using System.Buffers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Windows.ApplicationModel;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection.Acceptance;

internal static class HardwareInspectionProcessAcceptanceHost
{
    private const string ActivationCommand = "--hardware-inspection-process-acceptance";
    private const string Gate8Category = "HardwareInspectionGate8Acceptance";
    private const string ProcessCategory = "HardwareInspectionProcessAcceptance";
    private const string ResultTokenSwitch = "--result-token";
    private const string Schema = "granite.hardware-inspection.process-acceptance/v1";
    private const string TestPackageIdentity = "GraniteEdgeAI.WinUI.UnitTests";
    private const int MaximumResultBytes = 64 * 1024;
    private const int MaximumTestCount = 128;
    private const int MaximumTestNameScalars = 512;

    internal static bool TryParseActivation(
        IReadOnlyList<string> commandLine,
        out string resultToken)
    {
        resultToken = string.Empty;
        if (commandLine.Count != 4 ||
            !string.Equals(commandLine[1], ActivationCommand, StringComparison.Ordinal) ||
            !string.Equals(commandLine[2], ResultTokenSwitch, StringComparison.Ordinal) ||
            !IsResultToken(commandLine[3]))
        {
            return false;
        }

        resultToken = commandLine[3];
        return true;
    }

    internal static async Task<int> RunAsync(string resultToken)
    {
        if (!IsResultToken(resultToken))
        {
            return 64;
        }

        try
        {
            PackageId packageIdentity = Package.Current.Id;
            if (!string.Equals(
                    packageIdentity.Name,
                    TestPackageIdentity,
                    StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(packageIdentity.FullName))
            {
                return 70;
            }

            IReadOnlyList<AcceptanceTest> tests = DiscoverTests();
            var failed = new List<string>();
            int passed = 0;
            foreach (AcceptanceTest test in tests)
            {
                if (await ExecuteAsync(test))
                {
                    passed++;
                }
                else
                {
                    failed.Add(test.Name);
                }
            }

            WriteResultAtomically(resultToken, tests.Count, passed, failed);
            return failed.Count == 0 ? 0 : 1;
        }
        catch
        {
            return 70;
        }
    }

    private static IReadOnlyList<AcceptanceTest> DiscoverTests()
    {
        AcceptanceTest[] tests = typeof(HardwareInspectionProcessAcceptanceHost)
            .Assembly
            .GetTypes()
            .Where(static type => type.IsClass && !type.IsAbstract)
            .SelectMany(static type => type
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(method =>
                    method.GetCustomAttribute<TestMethodAttribute>(inherit: true) is not null &&
                    IsAcceptanceTest(type, method))
                .Select(method => CreateAcceptanceTest(type, method)))
            .OrderBy(static test => test.Name, StringComparer.Ordinal)
            .ToArray();

        if (tests.Length is 0 or > MaximumTestCount ||
            tests.Select(static test => test.Name).Distinct(StringComparer.Ordinal).Count() != tests.Length)
        {
            throw new InvalidOperationException("The acceptance inventory is invalid.");
        }

        return tests;
    }

    private static bool IsAcceptanceTest(Type type, MethodInfo method) =>
        HasAcceptanceCategory(method.GetCustomAttributes<TestCategoryAttribute>(inherit: true)) ||
        HasAcceptanceCategory(type.GetCustomAttributes<TestCategoryAttribute>(inherit: true));

    private static bool HasAcceptanceCategory(
        IEnumerable<TestCategoryAttribute> attributes) => attributes
        .SelectMany(static attribute => attribute.TestCategories)
        .Any(static category => category is ProcessCategory or Gate8Category);

    private static AcceptanceTest CreateAcceptanceTest(Type type, MethodInfo method)
    {
        if (type.GetConstructor(Type.EmptyTypes) is null ||
            method.GetParameters().Length != 0 ||
            (method.ReturnType != typeof(void) &&
             method.ReturnType != typeof(Task) &&
             method.ReturnType != typeof(ValueTask)))
        {
            throw new InvalidOperationException("An acceptance test has an unsupported signature.");
        }

        string name = $"{type.FullName}.{method.Name}";
        if (name.EnumerateRunes().Count() > MaximumTestNameScalars ||
            !name.All(static character =>
                character is >= 'A' and <= 'Z' or
                    >= 'a' and <= 'z' or
                    >= '0' and <= '9' or '.' or '_'))
        {
            throw new InvalidOperationException("An acceptance test name is unsafe.");
        }

        return new(name, type, method);
    }

    private static async Task<bool> ExecuteAsync(AcceptanceTest test)
    {
        try
        {
            object instance = Activator.CreateInstance(test.DeclaringType) ??
                throw new InvalidOperationException("The acceptance test could not be created.");
            object? result = test.Method.Invoke(instance, parameters: null);
            if (result is Task task)
            {
                await task;
            }
            else if (result is ValueTask valueTask)
            {
                await valueTask;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void WriteResultAtomically(
        string resultToken,
        int total,
        int passed,
        IReadOnlyList<string> failed)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("schema", Schema);
            writer.WriteBoolean("packageIdentityPresent", true);
            writer.WriteNumber("total", total);
            writer.WriteNumber("passed", passed);
            writer.WriteStartArray("failed");
            foreach (string name in failed)
            {
                writer.WriteStringValue(name);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        int lengthWithLf = checked(buffer.WrittenCount + 1);
        if (lengthWithLf > MaximumResultBytes)
        {
            throw new InvalidOperationException("The acceptance result exceeds its bound.");
        }

        string resultRoot = Path.Combine(
            Path.GetTempPath(),
            "GraniteEdgeAI.HardwareInspection.Tests",
            "Acceptance");
        Directory.CreateDirectory(resultRoot);
        string resultPath = Path.Combine(resultRoot, $"{resultToken}.json");
        string temporaryPath = Path.Combine(
            resultRoot,
            $".{resultToken}.{Guid.NewGuid():N}.tmp");
        if (File.Exists(resultPath))
        {
            throw new InvalidOperationException("The acceptance result already exists.");
        }

        try
        {
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None))
            {
                stream.Write(buffer.WrittenSpan);
                stream.WriteByte(0x0a);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, resultPath);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static bool IsResultToken(string value) =>
        value.Length == 32 && value.All(static character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private sealed record AcceptanceTest(string Name, Type DeclaringType, MethodInfo Method);
}
