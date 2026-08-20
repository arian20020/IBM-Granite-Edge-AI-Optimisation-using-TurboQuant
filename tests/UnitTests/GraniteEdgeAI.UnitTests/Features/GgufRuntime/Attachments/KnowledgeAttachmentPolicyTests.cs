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
    public void Validate_AcceptsSupportedExtendedLengthWindowsPaths()
    {
        KnowledgeAttachmentValidationResult result = KnowledgeAttachmentPolicy.Validate(
            new[]
            {
                Candidate(@"\\?\C:\Knowledge\guide.txt", 12),
                Candidate(@"\\?\UNC\server\share\notes.md", 24)
            },
            Array.Empty<KnowledgeAttachment>());

        Assert.AreEqual(2, result.Accepted.Count);
        Assert.AreEqual("guide.txt", result.Accepted[0].FileName);
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
    public void Validate_RejectsCanonicalDuplicatePathAgainstExistingAttachment()
    {
        KnowledgeAttachmentValidationResult result = KnowledgeAttachmentPolicy.Validate(
            new[] { Candidate(@"C:\Knowledge\docs\..\guide.txt", 12) },
            new[] { new KnowledgeAttachment(@"C:\Knowledge\guide.txt") });

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
    public void Validate_RejectsNullCandidateAndNullOrWhitespacePath()
    {
        KnowledgeAttachmentValidationResult result = KnowledgeAttachmentPolicy.Validate(
            new[]
            {
                null!,
                Candidate(null, 12),
                Candidate(" ", 12),
                Candidate("\t", 12)
            },
            Array.Empty<KnowledgeAttachment>());

        Assert.AreEqual(0, result.Accepted.Count);
        Assert.AreEqual(4, result.Rejections.Count);
        foreach (KnowledgeAttachmentRejection rejection in result.Rejections)
        {
            Assert.AreEqual("attachment-invalid", rejection.Code);
            Assert.AreEqual(string.Empty, rejection.FileName);
        }
    }

    [TestMethod]
    public void Validate_RejectsDriveRelativeRootOnlyAndTrailingSeparatorPathsSafely()
    {
        KnowledgeAttachmentValidationResult result = KnowledgeAttachmentPolicy.Validate(
            new[]
            {
                Candidate("C:secret.txt", 12),
                Candidate(@"C:\", 12),
                Candidate(@"C:\Knowledge\", 12)
            },
            Array.Empty<KnowledgeAttachment>());

        Assert.AreEqual(0, result.Accepted.Count);
        Assert.AreEqual(3, result.Rejections.Count);
        CollectionAssert.AreEqual(
            new[] { "attachment-invalid", "attachment-invalid", "attachment-invalid" },
            result.Rejections.Select(rejection => rejection.Code).ToArray());
        Assert.AreEqual("secret.txt", result.Rejections[0].FileName);
        Assert.AreEqual(string.Empty, result.Rejections[1].FileName);
        Assert.AreEqual(string.Empty, result.Rejections[2].FileName);
    }

    [TestMethod]
    public void Validate_RejectsInvalidCharacterEmbeddedNulAndMalformedPaths()
    {
        KnowledgeAttachmentValidationResult result = KnowledgeAttachmentPolicy.Validate(
            new[]
            {
                Candidate(@"C:\Knowledge\bad|name.txt", 12),
                Candidate("C:\\Knowledge\\bad\0name.txt", 12),
                Candidate(@"C:\Knowledge\bad<name.txt", 12)
            },
            Array.Empty<KnowledgeAttachment>());

        Assert.AreEqual(0, result.Accepted.Count);
        Assert.AreEqual(3, result.Rejections.Count);
        CollectionAssert.AreEqual(
            new[] { "attachment-invalid", "attachment-invalid", "attachment-invalid" },
            result.Rejections.Select(rejection => rejection.Code).ToArray());
        Assert.AreEqual("badname.txt", result.Rejections[0].FileName);
        Assert.AreEqual("badname.txt", result.Rejections[1].FileName);
        Assert.AreEqual("badname.txt", result.Rejections[2].FileName);
    }

    [TestMethod]
    public void Validate_UsesPrivacySafeLeafNamesForEveryRejection()
    {
        string unsupportedPath = @"C:\Private\unsupported.pdf";
        string inaccessiblePath = @"C:\Private\inaccessible.md";
        string driveRelativePath = "C:private.txt";

        KnowledgeAttachmentValidationResult result = KnowledgeAttachmentPolicy.Validate(
            new[]
            {
                Candidate(unsupportedPath, 12),
                Candidate(inaccessiblePath, 12, isAccessible: false),
                Candidate(driveRelativePath, 12)
            },
            Array.Empty<KnowledgeAttachment>());

        CollectionAssert.AreEqual(
            new[] { "unsupported.pdf", "inaccessible.md", "private.txt" },
            result.Rejections.Select(rejection => rejection.FileName).ToArray());
        Assert.IsTrue(result.Rejections.All(rejection =>
            !rejection.FileName.Contains(@"C:\", StringComparison.OrdinalIgnoreCase) &&
            !rejection.FileName.Contains("C:", StringComparison.OrdinalIgnoreCase) &&
            !rejection.FileName.Contains('\\') &&
            !rejection.FileName.Contains('/')));
    }

    [TestMethod]
    public void Validate_RejectsDuplicateAfterEarlierEmptyCandidate()
    {
        KnowledgeAttachmentValidationResult result = KnowledgeAttachmentPolicy.Validate(
            new[]
            {
                Candidate(@"C:\Knowledge\empty.txt", 0),
                Candidate(@"C:\Knowledge\empty.txt", 12)
            },
            Array.Empty<KnowledgeAttachment>());

        AssertRejectionCodes(result, "attachment-empty", "attachment-duplicate");
    }

    [TestMethod]
    public void Validate_RejectsDuplicateAfterEarlierUnsupportedCandidate()
    {
        KnowledgeAttachmentValidationResult result = KnowledgeAttachmentPolicy.Validate(
            new[]
            {
                Candidate(@"C:\Knowledge\unsupported.pdf", 12),
                Candidate(@"C:\Knowledge\unsupported.pdf", 12)
            },
            Array.Empty<KnowledgeAttachment>());

        AssertRejectionCodes(result, "attachment-unsupported-type", "attachment-duplicate");
    }

    [TestMethod]
    public void Validate_RejectsDuplicateAfterEarlierCountRejectedCandidate()
    {
        KnowledgeAttachment[] existing = Enumerable.Range(1, 8)
            .Select(index => new KnowledgeAttachment($@"C:\Knowledge\existing-{index}.txt"))
            .ToArray();

        KnowledgeAttachmentValidationResult result = KnowledgeAttachmentPolicy.Validate(
            new[]
            {
                Candidate(@"C:\Knowledge\full.txt", 12),
                Candidate(@"C:\Knowledge\full.txt", 12)
            },
            existing);

        AssertRejectionCodes(result, "attachment-count-exceeded", "attachment-duplicate");
    }

    [TestMethod]
    public void Validate_AppliesInvalidDuplicateInaccessibleAndUnsupportedPrecedence()
    {
        KnowledgeAttachmentValidationResult result = KnowledgeAttachmentPolicy.Validate(
            new[]
            {
                Candidate(@"C:\Knowledge\bad|name.txt", 12, isAccessible: false),
                Candidate(@"C:\Knowledge\existing.txt", 12, isAccessible: false),
                Candidate(@"C:\Knowledge\inaccessible.pdf", 12, isAccessible: false),
                Candidate(@"C:\Knowledge\inaccessible-negative.txt", -1, isAccessible: false),
                Candidate(@"C:\Knowledge\unsupported.pdf", 0)
            },
            new[] { new KnowledgeAttachment(@"C:\Knowledge\existing.txt") });

        AssertRejectionCodes(
            result,
            "attachment-invalid",
            "attachment-duplicate",
            "attachment-inaccessible",
            "attachment-inaccessible",
            "attachment-unsupported-type");
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
        string? path,
        long sizeInBytes,
        bool isAccessible = true) => new(path, sizeInBytes, isAccessible);

    private static void AssertRejectionCodes(
        KnowledgeAttachmentValidationResult result,
        params string[] expectedCodes)
    {
        Assert.AreEqual(0, result.Accepted.Count);
        CollectionAssert.AreEqual(
            expectedCodes,
            result.Rejections.Select(rejection => rejection.Code).ToArray());
    }

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
