using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace GraniteEdgeAI.R4Handoff.Validation;

public static class AssemblyRepositoryIdentityVerifier
{
    public static bool HasExactSubject(string assemblyPath, string subject)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath) ||
            subject.Length != 40 ||
            subject.Any(character => !Uri.IsHexDigit(character)))
        {
            return false;
        }

        using FileStream stream = new(
            assemblyPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            4096,
            FileOptions.SequentialScan);
        using var pe = new PEReader(stream, PEStreamOptions.LeaveOpen);
        if (!pe.HasMetadata)
        {
            return false;
        }

        MetadataReader metadata = pe.GetMetadataReader();
        AssemblyDefinition assembly = metadata.GetAssemblyDefinition();
        foreach (CustomAttributeHandle handle in assembly.GetCustomAttributes())
        {
            CustomAttribute attribute = metadata.GetCustomAttribute(handle);
            if (!IsInformationalVersionAttribute(metadata, attribute.Constructor))
            {
                continue;
            }

            BlobReader value = metadata.GetBlobReader(attribute.Value);
            if (value.ReadUInt16() != 1)
            {
                return false;
            }

            string? informationalVersion = value.ReadSerializedString();
            return informationalVersion is not null &&
                informationalVersion.EndsWith(
                    "+" + subject.ToLowerInvariant(),
                    StringComparison.Ordinal);
        }

        return false;
    }

    private static bool IsInformationalVersionAttribute(
        MetadataReader metadata,
        EntityHandle constructor)
    {
        if (constructor.Kind != HandleKind.MemberReference)
        {
            return false;
        }

        MemberReference member = metadata.GetMemberReference(
            (MemberReferenceHandle)constructor);
        if (member.Parent.Kind != HandleKind.TypeReference ||
            !string.Equals(metadata.GetString(member.Name), ".ctor", StringComparison.Ordinal))
        {
            return false;
        }

        TypeReference type = metadata.GetTypeReference((TypeReferenceHandle)member.Parent);
        if (!string.Equals(
                metadata.GetString(type.Namespace),
                "System.Reflection",
                StringComparison.Ordinal) ||
            !string.Equals(
                metadata.GetString(type.Name),
                "AssemblyInformationalVersionAttribute",
                StringComparison.Ordinal) ||
            type.ResolutionScope.Kind != HandleKind.AssemblyReference)
        {
            return false;
        }

        string scope = metadata.GetString(metadata.GetAssemblyReference(
            (AssemblyReferenceHandle)type.ResolutionScope).Name);
        return scope is "System.Runtime" or "mscorlib" or "System.Private.CoreLib";
    }
}
