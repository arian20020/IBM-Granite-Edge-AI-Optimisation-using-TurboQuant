using System.Collections.Generic;

namespace GraniteEdgeAI.Features.GgufRuntime.Attachments;

internal sealed record KnowledgeAttachment(string Path)
{
    internal string FileName => System.IO.Path.GetFileName(Path);
    internal string StateText => "Not indexed";
}

internal sealed record KnowledgeFileCandidate(string Path, long SizeInBytes, bool IsAccessible);

internal sealed record KnowledgeAttachmentRejection(string Code, string FileName);

internal sealed record KnowledgeAttachmentValidationResult(
    IReadOnlyList<KnowledgeAttachment> Accepted,
    IReadOnlyList<KnowledgeAttachmentRejection> Rejections);
