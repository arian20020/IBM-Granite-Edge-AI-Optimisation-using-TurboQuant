using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests;

/// <summary>
/// Provides the small string-assertion surface used by this packaged WinUI
/// test project while preserving MSTest behaviour for existing positive checks.
/// </summary>
/// <remarks>
/// The packaged MSTest version exposes <c>Contains</c> but does not expose the
/// comparison-aware <c>DoesNotContain</c> overload required by the application
/// contract graph tests. Keeping this compatibility facade in test code avoids
/// changing production contracts or suppressing the boundary assertions.
/// </remarks>
internal static class StringAssert
{
    /// <summary>
    /// Delegates existing positive containment checks to MSTest.
    /// </summary>
    internal static void Contains(string value, string substring)
    {
        Microsoft.VisualStudio.TestTools.UnitTesting.StringAssert.Contains(
            value,
            substring);
    }

    /// <summary>
    /// Verifies containment using an explicit comparison mode.
    /// </summary>
    internal static void Contains(
        string value,
        string substring,
        StringComparison comparison)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(substring);

        Assert.IsTrue(
            value.Contains(substring, comparison),
            "The value did not contain the expected text.");
    }

    /// <summary>
    /// Verifies that a value does not contain forbidden text using the supplied
    /// ordinal comparison semantics.
    /// </summary>
    internal static void DoesNotContain(
        string value,
        string substring,
        StringComparison comparison)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(substring);

        Assert.IsFalse(
            value.Contains(substring, comparison),
            "The value contained forbidden text.");
    }
}
