using GraniteEdgeAI.Features.GgufRuntime.Attachments;

namespace GraniteEdgeAI.UnitTests.Features.GgufRuntime.Attachments;

[TestClass]
public sealed class KnowledgeAttachmentPolicyTests
{
    private const long EightMiB = 8L * 1024 * 1024;

    [TestMethod]
    public void Validate_AcceptsSupportedFilesInPickerOrderAndProvidesNotIndexedState()
    {
        KnowledgeAttachmentValidationResult result = KnowledgeAttachmentPolicy.Validate(
            new[]
            {
                Candidate(@"C:\Knowledge\GUIDE.TXT", 12),
                Candidate(@"C:\Knowledge\notes.md", 24)
            },
            Array.Empty<KnowledgeAttachment>());

        Assert.AreEqual(2, result.Accepted.Count);
        Assert.AreEqual(@"C:\Knowledge\GUIDE.TXT", result.Accepted[0].Path);
        Assert.AreEqual("GUIDE.TXT", result.Accepted[0].FileName);
        Assert.AreEqual("Not indexed", result.Accepted[0].StateText);
        Assert.AreEqual(@"C:\Knowledge\notes.md", result.Accepted[1].Path);
        Assert.AreEqual("notes.md", result.Accepted[1].FileName);
        Assert.AreEqual(0, result.Rejections.Count);
    }

    [TestMethod]
    public void Validate_RejectsUnsupportedExtensionUsingOnlyFileName()
    {
        KnowledgeAttachmentValidationResult result = KnowledgeAttachmentPolicy.Validate(
            new[] { Candidate(@"C:\Private\data.pdf", 12) },
            Array.Empty<KnowledgeAttachment>());

        AssertSingleRejection(result, "attachment-unsupported-type", "data.pdf");
    }

    [TestMethod]
    public void Validate_RejectsDuplicatePathAgainstExistingAttachmentIgnoringCase()
    {
        KnowledgeAttachmentValidationResult result = KnowledgeAttachmentPolicy.Validate(
            new[] { Candidate(@"c:\knowledge\guide.txt", 12) },
            new[] { new KnowledgeAttachment(@"C:\Knowledge\GUIDE.TXT") });

        AssertSingleRejection(result, "attachment-duplicate", "guide.txt");
    }

    [TestMethod]
    public void Validate_RejectsDuplicatePathWithinSelectionIgnoringCase()
    {
        KnowledgeAttachmentValidationResult result = KnowledgeAttachmentPolicy.Validate(
            new[]
            {
                Candidate(@"C:\Knowledge\guide.txt", 12),
                Candidate(@"c:\knowledge\GUIDE.TXT", 12)
            },
            Array.Empty<KnowledgeAttachment>());

        Assert.AreEqual(1, result.Accepted.Count);
        Assert.AreEqual(1, result.Rejections.Count);
        Assert.AreEqual("attachment-duplicate", result.Rejections[0].Code);
        Assert.AreEqual("GUIDE.TXT", result.Rejections[0].FileName);
    }

    [TestMethod]
    public void Validate_RejectsInaccessibleCandidate()
    {
        KnowledgeAttachmentValidationResult result = KnowledgeAttachmentPolicy.Validate(
            new[] { Candidate(@"C:\Knowledge\restricted.md", 12, isAccessible: false) },
            Array.Empty<KnowledgeAttachment>());

        AssertSingleRejection(result, "attachment-inaccessible", "restricted.md");
    }

    [TestMethod]
    public void Validate_RejectsZeroByteCandidate()
    {
        KnowledgeAttachmentValidationResult result = KnowledgeAttachmentPolicy.Validate(
            new[] { Candidate(@"C:\Knowledge\empty.txt", 0) },
            Array.Empty<KnowledgeAttachment>());

        AssertSingleRejection(result, "attachment-empty", "empty.txt");
    }

    [TestMethod]
    public void Validate_RejectsNegativeSizeCandidateAsInvalid()
    {
        KnowledgeAttachmentValidationResult result = KnowledgeAttachmentPolicy.Validate(
            new[] { Candidate(@"C:\Knowledge\invalid-size.txt", -1) },
            Array.Empty<KnowledgeAttachment>());

        AssertSingleRejection(result, "attachment-invalid", "invalid-size.txt");
    }

    [TestMethod]
    public void Validate_AcceptsCandidateAtEightMiB()
    {
        KnowledgeAttachmentValidationResult result = KnowledgeAttachmentPolicy.Validate(
            new[] { Candidate(@"C:\Knowledge\limit.md", EightMiB) },
            Array.Empty<KnowledgeAttachment>());

        Assert.AreEqual(1, result.Accepted.Count);
        Assert.AreEqual(0, result.Rejections.Count);
    }

    [TestMethod]
    public void Validate_RejectsCandidateLargerThanEightMiB()
    {
        KnowledgeAttachmentValidationResult result = KnowledgeAttachmentPolicy.Validate(
            new[] { Candidate(@"C:\Knowledge\large.md", EightMiB + 1) },
            Array.Empty<KnowledgeAttachment>());

        AssertSingleRejection(result, "attachment-too-large", "large.md");
    }

    [TestMethod]
    public void Validate_RejectsNinthCombinedAttachment()
    {
        KnowledgeAttachment[] existing = Enumerable.Range(1, 7)
            .Select(index => new KnowledgeAttachment($@"C:\Knowledge\existing-{index}.txt"))
            .ToArray();

        KnowledgeAttachmentValidationResult result = KnowledgeAttachmentPolicy.Validate(
            new[]
            {
                Candidate(@"C:\Knowledge\eighth.txt", 12),
                Candidate(@"C:\Knowledge\ninth.txt", 12)
            },
            existing);

        Assert.AreEqual(1, result.Accepted.Count);
        Assert.AreEqual(1, result.Rejections.Count);
        Assert.AreEqual("attachment-count-exceeded", result.Rejections[0].Code);
        Assert.AreEqual("ninth.txt", result.Rejections[0].FileName);
    }

    [TestMethod]
    public void Validate_RejectsBlankAndMalformedPathsWithoutExposingPathData()
    {
        KnowledgeAttachmentValidationResult result = KnowledgeAttachmentPolicy.Validate(
            new[]
            {
                Candidate(" ", 12),
                Candidate("bad\0name.txt", 12)
            },
            Array.Empty<KnowledgeAttachment>());

        Assert.AreEqual(0, result.Accepted.Count);
        Assert.AreEqual(2, result.Rejections.Count);
        Assert.AreEqual("attachment-invalid", result.Rejections[0].Code);
        Assert.AreEqual(string.Empty, result.Rejections[0].FileName);
        Assert.AreEqual("attachment-invalid", result.Rejections[1].Code);
        Assert.AreEqual(string.Empty, result.Rejections[1].FileName);
    }

    [TestMethod]
    public void Validate_DoesNotMutateInputsAndRejectionsNeverContainFullPaths()
    {
        var selected = new List<KnowledgeFileCandidate>
        {
            Candidate(@"C:\Private\data.pdf", 12),
            Candidate(@"C:\Private\same.txt", 12),
            Candidate(@"c:\private\SAME.TXT", 12)
        };
        var existing = new List<KnowledgeAttachment>
        {
            new(@"C:\Knowledge\existing.md")
        };
        KnowledgeFileCandidate[] selectedBefore = selected.ToArray();
        KnowledgeAttachment[] existingBefore = existing.ToArray();

        KnowledgeAttachmentValidationResult result = KnowledgeAttachmentPolicy.Validate(selected, existing);

        CollectionAssert.AreEqual(selectedBefore, selected);
        CollectionAssert.AreEqual(existingBefore, existing);
        Assert.AreEqual(2, result.Rejections.Count);
        Assert.IsTrue(result.Rejections.All(rejection =>
            !rejection.FileName.Contains(@"C:\", StringComparison.OrdinalIgnoreCase) &&
            !rejection.FileName.Contains("Private", StringComparison.OrdinalIgnoreCase)));
    }

    private static KnowledgeFileCandidate Candidate(
        string path,
        long sizeInBytes,
        bool isAccessible = true) => new(path, sizeInBytes, isAccessible);

    private static void AssertSingleRejection(
        KnowledgeAttachmentValidationResult result,
        string expectedCode,
        string expectedFileName)
    {
        Assert.AreEqual(0, result.Accepted.Count);
        Assert.AreEqual(1, result.Rejections.Count);
        Assert.AreEqual(expectedCode, result.Rejections[0].Code);
        Assert.AreEqual(expectedFileName, result.Rejections[0].FileName);
    }
}
