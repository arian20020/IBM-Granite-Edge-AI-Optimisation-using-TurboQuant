using System.Collections.Generic;

namespace GraniteEdgeAI.Features.GgufRuntime.Attachments;

public sealed record KnowledgeAttachment(string Path)
{
    public string FileName => System.IO.Path.GetFileName(Path);
    public string StateText => "Not indexed";
}

internal sealed record KnowledgeFileCandidate(string? Path, long SizeInBytes, bool IsAccessible);

internal sealed record KnowledgeAttachmentRejection(string Code, string FileName);

internal sealed record KnowledgeAttachmentValidationResult(
    IReadOnlyList<KnowledgeAttachment> Accepted,
    IReadOnlyList<KnowledgeAttachmentRejection> Rejections);
