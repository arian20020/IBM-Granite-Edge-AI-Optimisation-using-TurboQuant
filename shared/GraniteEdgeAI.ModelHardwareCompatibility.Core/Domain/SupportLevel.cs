namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

/// <summary>
/// What the support matrix claims about a configuration. Anything the matrix
/// does not positively declare is Unknown, which resolves to unsupported: the
/// absence of a claim is never evidence that something works.
/// </summary>
public enum SupportLevel
{
    Unknown = 0,
    DeclaredSupported,
    Experimental
}
