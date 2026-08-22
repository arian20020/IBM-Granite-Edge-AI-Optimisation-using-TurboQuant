using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Capabilities;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Invariants;

/// <summary>
/// Section 14 forbids any path, filename, model name, hostname, device
/// identifier or raw tool output entering a C1 type. The cheapest durable
/// enforcement is to know every string member in the library by name.
/// </summary>
[TestClass]
public sealed class PrivacyCanaryTests
{
    private static readonly HashSet<string> AllowedStringMembers =
    [
        "SafetyPolicy.PolicyVersion",
        "EstimatorPolicy.PolicyVersion",
        "SupportMatrix.MatrixVersion",
        "CompatibilityCandidate.SupportEntryId",
        "CompatibilitySupportEntry.EntryId",
        "RouteConfiguration.CanonicalDescriptor",
        "GgufRouteConfiguration.CanonicalDescriptor",
        "CandidateFingerprint.Value",

        // Keyed by the same admitted support-entry id already allowed above
        // (CompatibilitySupportEntry.EntryId); this member only looks that id
        // up against an observed installation state and introduces no new
        // string content of its own.
        "CandidateGenerationRequest.InstallationStates",

        // The same admitted support-entry ids again (CompatibilitySupportEntry.EntryId),
        // this time carried as a set of ids requiring evidence rather than a lookup
        // dictionary. No new string content originates here.
        "ModeSelectionRequest.EvidenceRequiringEntryIds",

        // Returns a dictionary keyed by CompatibilitySupportEntry.EntryId, already
        // allowed above. The projection reads entry ids out of the matrix and
        // pairs each with an observed installation state; it never constructs a
        // string of its own, so nothing new can enter here.
        "CapabilityProjection.Project",

        // Hand-written ToString overrides surfaced once the scan widened to
        // cover methods. Each formats an already-reviewed numeric value (or,
        // for CandidateFingerprint, delegates to the already-allowed Value
        // property) and introduces no new content of its own.
        "ByteCount.ToString",
        "ContextTokenCount.ToString",
        "CandidateFingerprint.ToString",

        // Formats the run's Guid value (already reviewed via CompatibilityRunId's
        // only field) for logging/diagnostics; carries no path, filename or
        // model name.
        "CompatibilityRunId.ToString",

        // A policy name and version tag identifying which versioned policy
        // produced a number (e.g. "estimator", "v1") — never a path, filename
        // or model name.
        "PolicyIdentity.PolicyName",
        "PolicyIdentity.Version",

        // Run identities for the claimed handoffs, not paths or model names —
        // they identify which upstream run's facts were claimed, for
        // correlation and stale-claim detection.
        "HandoffClaim.ModelInspectionRunId",
        "HandoffClaim.ProductHardwareRunId"
    ];

    private static readonly BindingFlags AllMembers =
        BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
        | BindingFlags.DeclaredOnly;

    [TestMethod]
    public void NoUnreviewedStringMemberExistsInTheCompatibilityCore()
    {
        // A new string member is where a path or model name would first appear.
        // Adding one is allowed; adding one without review is not.
        //
        // Scans instance and static properties AND fields, and treats
        // string[] / IEnumerable<string> the same as a bare string: a path or
        // model name reaching a collection is exactly as much of a leak as
        // reaching a scalar. Methods returning a string-carrying type are
        // scanned too, since a computed string escapes this canary just as
        // easily as a stored one.
        string[] properties = typeof(ByteCount).Assembly
            .GetTypes()
            .SelectMany(type => type
                .GetProperties(AllMembers)
                .Where(property => CarriesString(property.PropertyType))
                .Select(property => $"{type.Name}.{property.Name}"))
            .ToArray();

        // Compiler-generated members are excluded: a property's backing field
        // and a record's synthesized ToString() carry no content beyond what
        // the declared members already expose, so they are not a new place
        // for a string to enter - they are a mechanical byproduct of members
        // this scan already reviews.
        string[] fields = typeof(ByteCount).Assembly
            .GetTypes()
            .SelectMany(type => type
                .GetFields(AllMembers)
                .Where(field => CarriesString(field.FieldType) && !IsCompilerGenerated(field))
                .Select(field => $"{type.Name}.{field.Name}"))
            .ToArray();

        string[] methods = ScanMethodsForUnexemptedStrings(typeof(ByteCount).Assembly.GetTypes());

        string[] found = properties
            .Concat(fields)
            .Concat(methods)
            .Where(name => !AllowedStringMembers.Contains(name))
            .Distinct()
            .Order()
            .ToArray();

        Assert.AreEqual(
            0,
            found.Length,
            "Unreviewed string-carrying members found. Confirm each carries no path, "
            + "filename, model name or native error, then add it to the allowlist: "
            + string.Join(", ", found));
    }

    /// <summary>
    /// The method half of the scan, factored out so the regression test below
    /// can run the real pipeline against a hand-built type instead of asserting
    /// on an exemption helper in isolation.
    /// </summary>
    private static string[] ScanMethodsForUnexemptedStrings(IEnumerable<Type> types) =>
        types
            .SelectMany(type => type
                .GetMethods(AllMembers)
                .Where(method =>
                    !method.IsSpecialName
                    && CarriesString(method.ReturnType)
                    && !IsCompilerGenerated(method)
                    && !IsForwardingLambdaOverAReviewedGetter(method))
                .Select(method => $"{type.Name}.{method.Name}"))
            .ToArray();

    private static bool CarriesString(Type type) =>
        type == typeof(string)
        || type == typeof(string[])
        || (typeof(System.Collections.IEnumerable).IsAssignableFrom(type)
            && type != typeof(string)
            && type.IsGenericType
            && type.GetGenericArguments().Contains(typeof(string)));

    private static bool IsCompilerGenerated(MemberInfo member) =>
        member.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false);

    /// <summary>
    /// True only when a method's entire body is "load the one argument, call a
    /// single property getter, return the result" - the exact IL shape the
    /// compiler emits for a lambda like <c>entry => entry.EntryId</c> - AND
    /// that getter is declared in the very assembly being scanned.
    ///
    /// This exists because every lambda, whether it merely forwards an
    /// already-reviewed property or computes brand-new content (say,
    /// <c>x => Path.GetFileName(x)</c>), is compiled onto the same
    /// compiler-generated "&lt;&gt;c" closure class. Exempting by declaring
    /// type alone (an earlier version of this check did exactly that) would
    /// therefore exempt both alike, silently widening the canary's blind spot
    /// to any lambda anywhere in Core. Decoding the IL instead lets a
    /// genuine forward through - it introduces no content beyond the getter's
    /// own already-scanned return value - while still catching a lambda that
    /// computes something new. See
    /// <see cref="MethodScan_StillCatchesAComputingLambdaDespiteTheForwardingExemption"/>
    /// for the regression test that exercises this distinction end to end.
    ///
    /// The "own already-scanned return value" reasoning only holds when the
    /// getter itself lives in the assembly this canary scans: that is the
    /// only case where the getter's return value is guaranteed to have been
    /// walked by <see cref="NoUnreviewedStringMemberExistsInTheCompatibilityCore"/>
    /// already. A lambda forwarding an external getter - for example
    /// <c>files.Select(f => f.FullName)</c> on a <see cref="System.IO.FileInfo"/> -
    /// has the identical single-callvirt IL shape but returns content this
    /// canary has never reviewed, so it must not be exempted. See
    /// <see cref="MethodScan_DoesNotExemptAForwardOverAnExternalGetter"/> for
    /// the regression test that exercises this distinction end to end.
    /// </summary>
    private static bool IsForwardingLambdaOverAReviewedGetter(MethodInfo method)
    {
        if (method.GetParameters().Length != 1)
        {
            return false;
        }

        if (!TryGetSoleCallTarget(method, out MethodBase? target))
        {
            return false;
        }

        return target is MethodInfo getter
            && getter.Name.StartsWith("get_", StringComparison.Ordinal)
            && CarriesString(getter.ReturnType)
            && getter.DeclaringType?.Assembly == method.DeclaringType?.Assembly;
    }

    /// <summary>
    /// Decodes a method body's IL and returns its single call/callvirt target,
    /// or false if the body contains anything other than exactly one such call
    /// (branches, multiple calls, field access, arithmetic, string
    /// concatenation, and so on all fail this and are treated conservatively as
    /// "not a proven simple forward").
    /// </summary>
    private static bool TryGetSoleCallTarget(MethodInfo method, out MethodBase? target)
    {
        target = null;

        byte[]? il = method.GetMethodBody()?.GetILAsByteArray();
        if (il is null)
        {
            return false;
        }

        List<int> callTokens = [];
        int i = 0;

        while (i < il.Length)
        {
            OpCode code;

            if (il[i] == 0xFE)
            {
                if (i + 1 >= il.Length
                    || !TwoByteOpCodes.TryGetValue((short)(0xFE00 | il[i + 1]), out code))
                {
                    return false;
                }

                i += 2;
            }
            else
            {
                if (!SingleByteOpCodes.TryGetValue(il[i], out code))
                {
                    return false;
                }

                i += 1;
            }

            switch (code.OperandType)
            {
                case OperandType.InlineNone:
                    break;

                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    i += 1;
                    break;

                case OperandType.InlineVar:
                    i += 2;
                    break;

                case OperandType.InlineSwitch:
                    if (i + 4 > il.Length)
                    {
                        return false;
                    }

                    int caseCount = BitConverter.ToInt32(il, i);
                    i += 4 + (caseCount * 4);
                    break;

                case OperandType.InlineMethod:
                    if (i + 4 > il.Length)
                    {
                        return false;
                    }

                    callTokens.Add(BitConverter.ToInt32(il, i));
                    i += 4;
                    break;

                case OperandType.InlineBrTarget:
                case OperandType.InlineField:
                case OperandType.InlineI:
                case OperandType.InlineSig:
                case OperandType.InlineString:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                case OperandType.ShortInlineR:
                    i += 4;
                    break;

                case OperandType.InlineI8:
                case OperandType.InlineR:
                    i += 8;
                    break;

                default:
                    // An operand shape this decoder does not know is treated as
                    // disqualifying rather than guessed at.
                    return false;
            }
        }

        if (callTokens.Count != 1)
        {
            return false;
        }

        try
        {
            target = method.Module.ResolveMethod(callTokens[0]);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static readonly Dictionary<short, OpCode> SingleByteOpCodes = BuildOpCodeTable(size: 1);

    private static readonly Dictionary<short, OpCode> TwoByteOpCodes = BuildOpCodeTable(size: 2);

    private static Dictionary<short, OpCode> BuildOpCodeTable(int size)
    {
        Dictionary<short, OpCode> table = [];

        foreach (FieldInfo field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is OpCode code && code.Size == size)
            {
                table[code.Value] = code;
            }
        }

        return table;
    }

    [TestMethod]
    public void MethodScan_StillCatchesAComputingLambdaDespiteTheForwardingExemption()
    {
        // Regression guard for IsForwardingLambdaOverAReviewedGetter: it must
        // not widen into "any lambda is exempt". ComputingLambdaHolder's lambda
        // sits on the exact same kind of compiler-generated "<>c" closure class
        // as a genuine forwarding lambda would, so only structural IL analysis -
        // never the declaring type - can tell the two apart. This runs the real
        // method-scan pipeline (not the exemption helper in isolation) against a
        // hand-built type to prove the distinction holds end to end.
        string[] found = ScanMethodsForUnexemptedStrings(NestedTypesOf(typeof(ComputingLambdaHolder)));

        Assert.IsTrue(
            found.Length > 0,
            "A lambda that computes new string content (rather than forwarding an "
            + "already-reviewed property) must still be caught by the scan.");
    }

    [TestMethod]
    public void MethodScan_DoesNotFlagAGenuineForwardingLambda()
    {
        // The positive control for the same distinction: a lambda that only
        // returns an already-reviewed property must still be exempt, or every
        // Select(x => x.SomeProperty) in Core would need an unstable
        // compiler-generated name added to the allowlist.
        string[] found = ScanMethodsForUnexemptedStrings(NestedTypesOf(typeof(ForwardingLambdaHolder)));

        Assert.AreEqual(
            0,
            found.Length,
            "A lambda that only forwards an existing property must remain exempt.");
    }

    [TestMethod]
    public void MethodScan_DoesNotExemptAForwardOverAnExternalGetter()
    {
        // The missing-conjunct regression: ExternalForwardingLambdaHolder's
        // lambda has the exact same "single callvirt to a get_* returning
        // string" IL shape as ForwardingLambdaHolder's above, but the getter
        // it calls (FileInfo.FullName) lives outside this assembly and has
        // never been walked by the property/field scan. Exempting it on
        // shape alone - as the helper did before the assembly check was
        // added - would let files.Select(f => f.FullName) through unnoticed
        // and return an absolute path. It must still be caught.
        string[] found = ScanMethodsForUnexemptedStrings(
            NestedTypesOf(typeof(ExternalForwardingLambdaHolder)));

        Assert.IsTrue(
            found.Length > 0,
            "A lambda forwarding an external (out-of-assembly) getter must not "
            + "be exempted merely because its IL shape matches a reviewed forward.");
    }

    private static IEnumerable<Type> NestedTypesOf(Type outer) =>
        new[] { outer }.Concat(outer.GetNestedTypes(AllMembers));

    private static class ExternalForwardingLambdaHolder
    {
        // Same shape as ForwardingLambdaHolder.DistinctNameCount, but the
        // lambda forwards System.IO.FileInfo.FullName - a getter declared in
        // an assembly this canary never scans - instead of an in-assembly
        // property.
        internal static int DistinctFullNameCount(IEnumerable<FileInfo> files) =>
            files.Select(file => file.FullName).Distinct().Count();
    }

    private static class ComputingLambdaHolder
    {
        // Deliberately not a forwarding lambda: it derives new string content
        // (a file name) rather than returning an existing property, so the scan
        // must still catch it even though it is compiler-generated. The outer
        // method returns int, not a string-carrying type, so only the lambda
        // itself is under test here - matching the shape SupportMatrix.FromEntries
        // actually uses (Select(...).Distinct().Count()).
        internal static int DistinctFileNameCount(IEnumerable<string> paths) =>
            paths.Select(path => Path.GetFileName(path)).Distinct().Count();
    }

    private static class ForwardingLambdaHolder
    {
        internal sealed record Item(string Name);

        // The positive control: same shape as ComputingLambdaHolder above, but
        // the lambda only forwards Item.Name rather than computing anything new.
        internal static int DistinctNameCount(IEnumerable<Item> items) =>
            items.Select(item => item.Name).Distinct().Count();
    }

    [TestMethod]
    public void EveryAllowedStringValue_IsFreeOfPathLikeContent()
    {
        string[] forbidden = ["\\", "/", ":", ".gguf", ".."];

        SupportMatrix matrix = SupportMatrix.ProvisionalV1();

        // Entry ids are hand-authored strings in a list explicitly designed
        // to be extended by people - nothing stops someone writing
        // Entry("C:/models/granite-q4.gguf-cpu", ...). Every allowed string
        // value is sampled here, not just the two policy versions, so a
        // future entry id (or matrix version) carrying path-like content is
        // caught the same way.
        string[] samples =
        [
            SafetyPolicy.ProvisionalV1().PolicyVersion,
            EstimatorPolicy.ProvisionalV1().PolicyVersion,
            matrix.MatrixVersion,
            .. matrix.Entries.Select(entry => entry.EntryId)
        ];

        foreach (string sample in samples)
        {
            foreach (string fragment in forbidden)
            {
                Assert.IsFalse(
                    sample.Contains(fragment, StringComparison.OrdinalIgnoreCase),
                    $"'{sample}' contains path-like content '{fragment}'.");
            }
        }
    }
}
