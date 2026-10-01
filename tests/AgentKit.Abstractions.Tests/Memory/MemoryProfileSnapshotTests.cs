// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Memory;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="MemoryProfileSnapshot"/> constraints on enabled axes and key lists.</summary>
public sealed class MemoryProfileSnapshotTests
{
    private static MemoryProfileSnapshot Create(
        bool durable = true,
        bool retrieval = true,
        bool rewriting = false,
        MemoryStoreKey? store = null,
        ImmutableArray<VectorIndexKey> indexes = default,
        ImmutableArray<RetrievalSourceKey> sources = default,
        QueryRewriterReference? rewriter = null,
        MemoryPolicyProfileKey? policy = null,
        RetrievalBudget? budget = null,
        DataClassification classification = DataClassification.Internal,
        ContentHash? fingerprint = null,
        MemoryProfileKey? key = null,
        MemoryProfileVersion? version = null) => new(
            key ?? new MemoryProfileKey("p"),
            version ?? new MemoryProfileVersion(1),
            durable,
            retrieval,
            rewriting,
            true,
            store ?? new MemoryStoreKey("m"),
            null,
            indexes,
            sources.IsDefault ? [new RetrievalSourceKey("s")] : sources,
            rewriter,
            policy ?? new MemoryPolicyProfileKey("pol"),
            null,
            null,
            budget ?? new RetrievalBudget(1, 1, 1),
            classification,
            fingerprint ?? new ContentHash("h"));

    [Fact]
    public void Constructor_WhenValid_PreservesEveryValue()
    {
        var snapshot = MemoryTestData.Snapshot(documentStore: "docs", vectorIndexes: ["v1", "v2"], sources: ["a", "b"]);

        snapshot.Key.ShouldBe(new MemoryProfileKey("memory"));
        snapshot.DurableMemoryEnabled.ShouldBeTrue();
        snapshot.DocumentStore.ShouldBe(new DocumentStoreKey("docs"));
        snapshot.VectorIndexes.ShouldBe([new VectorIndexKey("v1"), new VectorIndexKey("v2")]);
        snapshot.RetrievalSources.ShouldBe([new RetrievalSourceKey("a"), new RetrievalSourceKey("b")]);
    }

    [Fact]
    public void Constructor_WhenKeyOrVersionIsInvalid_Throws()
    {
        Should.Throw<ArgumentNullException>(() => Create(key: default(MemoryProfileKey))).ParamName.ShouldBe("key");
        Should.Throw<ArgumentOutOfRangeException>(() => Create(version: default(MemoryProfileVersion))).ParamName.ShouldBe("version");
    }

    [Fact]
    public void Constructor_WhenDurableMemoryIsEnabledWithoutAStore_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new MemoryProfileSnapshot(
            new MemoryProfileKey("p"), new MemoryProfileVersion(1), true, false, false, true, null, null, default, default, null,
            new MemoryPolicyProfileKey("pol"), null, null, new RetrievalBudget(1, 1, 1), DataClassification.Public, new ContentHash("h"))).ParamName.ShouldBe("memoryStore");

    [Fact]
    public void Constructor_WhenRetrievalIsEnabledWithoutSources_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new MemoryProfileSnapshot(
            new MemoryProfileKey("p"), new MemoryProfileVersion(1), false, true, false, true, null, null, default, default, null,
            new MemoryPolicyProfileKey("pol"), null, null, new RetrievalBudget(1, 1, 1), DataClassification.Public, new ContentHash("h"))).ParamName.ShouldBe("retrievalSources");

    [Fact]
    public void Constructor_WhenRewritingFlagDisagreesWithTheRewriter_ThrowsArgumentException()
    {
        var rewriter = new QueryRewriterReference(new QueryRewriterKey("r"), new QueryRewriterVersion("1"));

        Should.Throw<ArgumentException>(() => Create(rewriting: true)).ParamName.ShouldBe("queryRewriter");
        Should.Throw<ArgumentException>(() => Create(rewriting: false, rewriter: rewriter)).ParamName.ShouldBe("queryRewriter");
        Create(rewriting: true, rewriter: rewriter).QueryRewriter.ShouldBe(rewriter);
    }

    [Fact]
    public void Constructor_WhenKeyListsRepeatOrContainDefaults_ThrowsArgumentException()
    {
        var index = new VectorIndexKey("v");
        var source = new RetrievalSourceKey("s");

        Should.Throw<ArgumentException>(() => Create(indexes: [index, index])).ParamName.ShouldBe("vectorIndexes");
        Should.Throw<ArgumentException>(() => Create(indexes: [default])).ParamName.ShouldBe("vectorIndexes");
        Should.Throw<ArgumentException>(() => Create(sources: [source, source])).ParamName.ShouldBe("retrievalSources");
    }

    [Fact]
    public void Constructor_WhenPolicyFingerprintBudgetOrClassificationIsInvalid_Throws()
    {
        Should.Throw<ArgumentNullException>(() => Create(policy: default(MemoryPolicyProfileKey))).ParamName.ShouldBe("policyProfile");
        Should.Throw<ArgumentNullException>(() => Create(fingerprint: default(ContentHash))).ParamName.ShouldBe("configurationFingerprint");
        Should.Throw<ArgumentOutOfRangeException>(() => Create(classification: (DataClassification) 9)).ParamName.ShouldBe("maximumClassification");
    }

    [Fact]
    public void Constructor_WhenBudgetIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new MemoryProfileSnapshot(
            new MemoryProfileKey("p"), new MemoryProfileVersion(1), false, false, false, true, null, null, default, default, null,
            new MemoryPolicyProfileKey("pol"), null, null, null!, DataClassification.Public, new ContentHash("h"))).ParamName.ShouldBe("retrievalBudget");

    [Fact]
    public void Equality_WhenKeyListsMatchByContent_IsStructural()
    {
        var first = MemoryTestData.Snapshot(vectorIndexes: ["v"]);
        var second = MemoryTestData.Snapshot(vectorIndexes: ["v"]);

        first.Equals(second).ShouldBeTrue();
        first.GetHashCode().ShouldBe(second.GetHashCode());
        first.Equals(MemoryTestData.Snapshot(vectorIndexes: ["w"])).ShouldBeFalse();
        first.Equals(null).ShouldBeFalse();
    }
}
