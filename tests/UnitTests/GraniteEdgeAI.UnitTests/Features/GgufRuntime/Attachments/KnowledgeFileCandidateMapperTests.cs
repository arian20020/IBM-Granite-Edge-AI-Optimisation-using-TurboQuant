using GraniteEdgeAI.Features.GgufRuntime.Attachments;
using System.IO;

namespace GraniteEdgeAI.UnitTests.Features.GgufRuntime.Attachments;

[TestClass]
public sealed class KnowledgeFileCandidateMapperTests
{
    [TestMethod]
    public void Map_UsesFileInfoLengthsAndPreservesInputOrder()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string firstPath = Path.Combine(directory, "first.txt");
            string secondPath = Path.Combine(directory, "second.md");
            File.WriteAllBytes(firstPath, [1, 2, 3]);
            File.WriteAllBytes(secondPath, [4, 5, 6, 7, 8]);

            IReadOnlyList<KnowledgeFileCandidate> candidates = KnowledgeFileCandidateMapper.Map(
                [secondPath, firstPath]);

            Assert.AreEqual(2, candidates.Count);
            Assert.AreEqual(secondPath, candidates[0].Path);
            Assert.AreEqual(5L, candidates[0].SizeInBytes);
            Assert.IsTrue(candidates[0].IsAccessible);
            Assert.AreEqual(firstPath, candidates[1].Path);
            Assert.AreEqual(3L, candidates[1].SizeInBytes);
            Assert.IsTrue(candidates[1].IsAccessible);
        }
        finally
        {
            DeleteTemporaryDirectory(directory);
        }
    }

    [TestMethod]
    public void Map_MapsBlankInvalidAndMissingPathsAsInaccessible()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string missingPath = Path.Combine(directory, "missing.txt");

            IReadOnlyList<KnowledgeFileCandidate> candidates = KnowledgeFileCandidateMapper.Map(
                [null, " ", "\0", missingPath]);

            Assert.AreEqual(4, candidates.Count);
            CollectionAssert.AreEqual(
                new string?[] { null, " ", "\0", missingPath },
                candidates.Select(candidate => candidate.Path).ToArray());
            Assert.IsTrue(candidates.All(candidate => !candidate.IsAccessible));
            Assert.IsTrue(candidates.All(candidate => candidate.SizeInBytes == 0));
        }
        finally
        {
            DeleteTemporaryDirectory(directory);
        }
    }

    [TestMethod]
    public void Map_BoundsMetadataReadsAndPreservesCandidatesBeyondTheBound()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string firstPath = Path.Combine(directory, "first.txt");
            string beyondBoundPath = Path.Combine(directory, "beyond-bound.md");
            File.WriteAllBytes(firstPath, [1]);
            File.WriteAllBytes(beyondBoundPath, [1, 2]);
            var paths = new List<string?> { firstPath };
            for (int index = 1; index < KnowledgeFileCandidateMapper.MaximumCandidateMetadataCount; index++)
            {
                paths.Add(Path.Combine(directory, $"missing-{index}.txt"));
            }

            paths.Add(beyondBoundPath);

            IReadOnlyList<KnowledgeFileCandidate> candidates = KnowledgeFileCandidateMapper.Map(paths);

            Assert.AreEqual(KnowledgeFileCandidateMapper.MaximumCandidateMetadataCount + 1, candidates.Count);
            CollectionAssert.AreEqual(paths, candidates.Select(candidate => candidate.Path).ToArray());
            Assert.IsTrue(candidates[0].IsAccessible);
            Assert.AreEqual(1L, candidates[0].SizeInBytes);
            Assert.IsFalse(candidates[^1].IsAccessible);
            Assert.AreEqual(0L, candidates[^1].SizeInBytes);
        }
        finally
        {
            DeleteTemporaryDirectory(directory);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "GraniteEdgeAI",
            "KnowledgeFileCandidateMapperTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void DeleteTemporaryDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
