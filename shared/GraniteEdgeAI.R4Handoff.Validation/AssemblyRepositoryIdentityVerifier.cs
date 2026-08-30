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
        EntityHandle parent = constructor.Kind switch
        {
            HandleKind.MemberReference =>
                metadata.GetMemberReference((MemberReferenceHandle)constructor).Parent,
            HandleKind.MethodDefinition =>
                metadata.GetMethodDefinition((MethodDefinitionHandle)constructor)
                    .GetDeclaringType(),
            _ => default,
        };

        string? name = parent.Kind switch
        {
            HandleKind.TypeReference => metadata.GetString(
                metadata.GetTypeReference((TypeReferenceHandle)parent).Name),
            HandleKind.TypeDefinition => metadata.GetString(
                metadata.GetTypeDefinition((TypeDefinitionHandle)parent).Name),
            _ => null,
        };
        return string.Equals(
            name,
            "AssemblyInformationalVersionAttribute",
            StringComparison.Ordinal);
    }
}
