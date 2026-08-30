using System.Reflection;
using GraniteEdgeAI.R4Handoff.Validation;

namespace GraniteEdgeAI.SecurityAudit.Tests;

[TestClass]
public sealed class AssemblyRepositoryIdentityVerifierTests
{
    [TestMethod]
    public void ParsedInformationalVersionRejectsAppendedDecoySubject()
    {
        string source = typeof(AssemblyRepositoryIdentityVerifierTests).Assembly.Location;
        string informationalVersion = typeof(AssemblyRepositoryIdentityVerifierTests)
            .Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;
        string actualSubject = informationalVersion[(informationalVersion.LastIndexOf('+') + 1)..];
        Assert.IsTrue(
            AssemblyRepositoryIdentityVerifier.HasExactSubject(source, actualSubject));

        string decoy = new('a', 40);
        string copy = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".dll");
        try
        {
            File.Copy(source, copy);
            using (FileStream stream = new(copy, FileMode.Append, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, leaveOpen: true))
            {
                writer.Write(decoy);
            }

            Assert.IsFalse(
                AssemblyRepositoryIdentityVerifier.HasExactSubject(copy, decoy));
        }
        finally
        {
            File.Delete(copy);
        }
    }

    [TestMethod]
    public void SameShortAttributeNameOutsideFrameworkNamespaceIsRejected()
    {
        Assert.IsFalse(AssemblyRepositoryIdentityVerifier.HasExactSubject(
            typeof(Hostile.FixtureMarker).Assembly.Location,
            new string('a', 40)));
    }
}
