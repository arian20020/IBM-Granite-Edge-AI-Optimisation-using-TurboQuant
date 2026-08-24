using System.Reflection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.Contracts;

[TestClass]
public sealed class InspectedModelFactsTests
{
    [TestMethod]
    public void Create_PreservesEveryFact()
    {
        InspectedModelFacts facts = InspectedModelFacts.Create(
            fileLength: ByteCount.FromBytes(4_000_000_000),
            layerCount: 32,
            embeddingSize: 4096,
            attentionHeadCount: 32,
            keyValueHeadCount: 8,
            declaredContextLimit: 8192,
            fileType: 15,
            quantisationVersion: 2);

        Assert.AreEqual(4_000_000_000UL, facts.FileLength.Bytes);
        Assert.AreEqual(32, facts.LayerCount);
        Assert.AreEqual(4096, facts.EmbeddingSize);
        Assert.AreEqual(32, facts.AttentionHeadCount);
        Assert.AreEqual(8, facts.KeyValueHeadCount);
        Assert.AreEqual(8192, facts.DeclaredContextLimit);
        Assert.AreEqual(15, facts.FileType);
        Assert.AreEqual(2, facts.QuantisationVersion);
    }

    [TestMethod]
    public void Create_AllowsEveryArchitecturalFactToBeAbsent()
    {
        // A quick scan may establish the file length and nothing else. That must
        // be representable, because the alternative is inventing architecture.
        InspectedModelFacts facts = InspectedModelFacts.Create(
            fileLength: ByteCount.FromBytes(1024),
            layerCount: null,
            embeddingSize: null,
            attentionHeadCount: null,
            keyValueHeadCount: null,
            declaredContextLimit: null,
            fileType: null,
            quantisationVersion: null);

        Assert.IsNull(facts.LayerCount);
        Assert.IsNull(facts.EmbeddingSize);
        Assert.IsNull(facts.AttentionHeadCount);
        Assert.IsNull(facts.KeyValueHeadCount);
        Assert.IsNull(facts.DeclaredContextLimit);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void Create_RejectsNonPositiveLayerCount(int layers)
    {
        // Zero is not "unknown"; null is. Accepting zero would make a model with
        // no layers indistinguishable from one whose layer count was never read.
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => InspectedModelFacts.Create(
                ByteCount.FromBytes(1024), layers, 4096, 32, 8, 8192, 15, 2));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void Create_RejectsNonPositiveEmbeddingSize(int embedding)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => InspectedModelFacts.Create(
                ByteCount.FromBytes(1024), 32, embedding, 32, 8, 8192, 15, 2));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void Create_RejectsNonPositiveAttentionHeadCount(int heads)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => InspectedModelFacts.Create(
                ByteCount.FromBytes(1024), 32, 4096, heads, 8, 8192, 15, 2));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void Create_RejectsNonPositiveKeyValueHeadCount(int heads)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => InspectedModelFacts.Create(
                ByteCount.FromBytes(1024), 32, 4096, 32, heads, 8192, 15, 2));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void Create_RejectsNonPositiveDeclaredContextLimit(int limit)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => InspectedModelFacts.Create(
                ByteCount.FromBytes(1024), 32, 4096, 32, 8, limit, 15, 2));
    }

    [TestMethod]
    public void Create_RejectsZeroFileLength()
    {
        // File length is the one measured quantity the weight estimate rests on.
        // A zero-length model would produce a free configuration.
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => InspectedModelFacts.Create(
                ByteCount.Zero, 32, 4096, 32, 8, 8192, 15, 2));
    }

    [TestMethod]
    public void Facts_CarryNoStringMember()
    {
        // Privacy canary. Section 14 forbids any path, filename or model name
        // reaching a C1 record. No string member means no place to put one.
        PropertyInfo[] strings = typeof(InspectedModelFacts)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(property => property.PropertyType == typeof(string))
            .ToArray();

        Assert.AreEqual(
            0,
            strings.Length,
            "InspectedModelFacts must expose no string member: "
            + string.Join(", ", strings.Select(property => property.Name)));
    }
}
