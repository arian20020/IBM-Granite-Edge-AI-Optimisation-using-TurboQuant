using System.Reflection;

[assembly: Hostile.AssemblyInformationalVersionAttribute(
    "1.0.0+aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]

namespace Hostile;

[AttributeUsage(AttributeTargets.Assembly)]
public sealed class AssemblyInformationalVersionAttribute(string value) : Attribute
{
    public string Value { get; } = value;
}

public sealed class FixtureMarker;
