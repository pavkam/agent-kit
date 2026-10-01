// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Vectors;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="VectorSecurityBinding"/> resources and fingerprints bind exactly one operation.</summary>
public sealed class VectorSecurityBindingTests
{
    private static readonly IdempotencyKey _key = new("k");

    [Fact]
    public void Resource_WhenKeyIsSupplied_NamesOneApplicationStateResource()
    {
        var resource = VectorSecurityBinding.Resource(new VectorIndexKey("idx"));

        resource.Kind.ShouldBe(ProtectedResourceKind.ApplicationState);
        resource.Identifier.ShouldBe("vector-index:idx");
        Should.Throw<ArgumentNullException>(() => VectorSecurityBinding.Resource(default)).ParamName.ShouldBe("key");
    }

    [Fact]
    public void UpsertFingerprint_WhenBatchOrSpaceChanges_ChangesTheFingerprint()
    {
        var owner = MemoryTestData.NewOwner();
        var chunks = MemoryTestData.Chunks(MemoryTestData.Document(owner), 2);
        var space = MemoryTestData.Space();
        ImmutableArray<VectorRecord> one = [MemoryTestData.Vector(owner, chunks[0], 1, 0, 0)];
        ImmutableArray<VectorRecord> both = [MemoryTestData.Vector(owner, chunks[0], 1, 0, 0), MemoryTestData.Vector(owner, chunks[1], 0, 1, 0)];
        var baseline = VectorSecurityBinding.UpsertFingerprint(space, one, _key);

        baseline.ShouldBe(VectorSecurityBinding.UpsertFingerprint(space, one, _key));
        baseline.ShouldNotBe(VectorSecurityBinding.UpsertFingerprint(space, both, _key));
        baseline.ShouldNotBe(VectorSecurityBinding.UpsertFingerprint(MemoryTestData.Space("other"), one, _key));
    }

    [Fact]
    public void UpsertFingerprint_WhenArgumentsAreInvalid_Throws()
    {
        Should.Throw<ArgumentNullException>(() => VectorSecurityBinding.UpsertFingerprint(null!, [], _key)).ParamName.ShouldBe("space");
        Should.Throw<ArgumentException>(() => VectorSecurityBinding.UpsertFingerprint(MemoryTestData.Space(), default, _key)).ParamName.ShouldBe("records");
        Should.Throw<ArgumentNullException>(() => VectorSecurityBinding.UpsertFingerprint(MemoryTestData.Space(), [], default)).ParamName.ShouldBe("idempotencyKey");
    }

    [Fact]
    public void SearchFingerprint_WhenQueryBoundOrRestrictionChanges_ChangesTheFingerprint()
    {
        var space = MemoryTestData.Space();
        var document = new DocumentId(Guid.NewGuid());
        var baseline = VectorSecurityBinding.SearchFingerprint(space, [1f, 0f, 0f], 5, default);

        baseline.ShouldBe(VectorSecurityBinding.SearchFingerprint(space, [1f, 0f, 0f], 5, []));
        baseline.ShouldNotBe(VectorSecurityBinding.SearchFingerprint(space, [0f, 1f, 0f], 5, default));
        baseline.ShouldNotBe(VectorSecurityBinding.SearchFingerprint(space, [1f, 0f, 0f], 6, default));
        baseline.ShouldNotBe(VectorSecurityBinding.SearchFingerprint(space, [1f, 0f, 0f], 5, [document]));
    }

    [Fact]
    public void SearchFingerprint_WhenArgumentsAreInvalid_Throws()
    {
        Should.Throw<ArgumentNullException>(() => VectorSecurityBinding.SearchFingerprint(null!, [1f], 1, default)).ParamName.ShouldBe("space");
        Should.Throw<ArgumentException>(() => VectorSecurityBinding.SearchFingerprint(MemoryTestData.Space(), default, 1, default)).ParamName.ShouldBe("query");
        Should.Throw<ArgumentOutOfRangeException>(() => VectorSecurityBinding.SearchFingerprint(MemoryTestData.Space(), [1f], 0, default)).ParamName.ShouldBe("topK");
    }

    [Fact]
    public void DeleteFingerprint_WhenChunksChange_ChangesTheFingerprint()
    {
        var space = MemoryTestData.Space();
        var first = new ChunkId(Guid.NewGuid());

        VectorSecurityBinding.DeleteFingerprint(space, [first], _key).ShouldBe(VectorSecurityBinding.DeleteFingerprint(space, [first], _key));
        VectorSecurityBinding.DeleteFingerprint(space, [first], _key).ShouldNotBe(VectorSecurityBinding.DeleteFingerprint(space, [new ChunkId(Guid.NewGuid())], _key));
    }

    [Fact]
    public void DeleteFingerprint_WhenArgumentsAreInvalid_Throws()
    {
        Should.Throw<ArgumentNullException>(() => VectorSecurityBinding.DeleteFingerprint(null!, [], _key)).ParamName.ShouldBe("space");
        Should.Throw<ArgumentException>(() => VectorSecurityBinding.DeleteFingerprint(MemoryTestData.Space(), default, _key)).ParamName.ShouldBe("chunkIds");
        Should.Throw<ArgumentNullException>(() => VectorSecurityBinding.DeleteFingerprint(MemoryTestData.Space(), [], default)).ParamName.ShouldBe("idempotencyKey");
    }
}
