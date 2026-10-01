// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Memory;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="MemorySecurityBinding"/> resources and fingerprints bind exactly one operation.</summary>
public sealed class MemorySecurityBindingTests
{
    private static readonly IdempotencyKey _key = new("k");

    [Fact]
    public void Resource_WhenIdIsSupplied_NamesOneApplicationStateResource()
    {
        var id = new MemoryId(Guid.NewGuid());

        var resource = MemorySecurityBinding.Resource(id);

        resource.Kind.ShouldBe(ProtectedResourceKind.ApplicationState);
        resource.Identifier.ShouldBe($"memory:{id}");
    }

    [Fact]
    public void Resource_WhenIdIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => MemorySecurityBinding.Resource(default)).ParamName.ShouldBe("id");

    [Fact]
    public void CollectionResource_WhenAgentIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => MemorySecurityBinding.CollectionResource(default)).ParamName.ShouldBe("agentId");

    [Fact]
    public void WriteFingerprint_WhenRecordChanges_ChangesTheFingerprint()
    {
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Record(owner);

        MemorySecurityBinding.WriteFingerprint(record, _key).ShouldBe(MemorySecurityBinding.WriteFingerprint(record, _key));
        MemorySecurityBinding.WriteFingerprint(record, _key).ShouldNotBe(MemorySecurityBinding.WriteFingerprint(MemoryTestData.Record(owner), _key));
        MemorySecurityBinding.WriteFingerprint(record, _key).ShouldNotBe(MemorySecurityBinding.WriteFingerprint(record, new IdempotencyKey("other")));
    }

    [Fact]
    public void WriteFingerprint_WhenArgumentsAreInvalid_Throws()
    {
        Should.Throw<ArgumentNullException>(() => MemorySecurityBinding.WriteFingerprint(null!, _key)).ParamName.ShouldBe("record");
        Should.Throw<ArgumentNullException>(() => MemorySecurityBinding.WriteFingerprint(MemoryTestData.Record(MemoryTestData.NewOwner()), default)).ParamName.ShouldBe("idempotencyKey");
    }

    [Fact]
    public void ReadFingerprint_WhenIdDiffers_ChangesTheFingerprint()
    {
        var first = new MemoryId(Guid.NewGuid());

        MemorySecurityBinding.ReadFingerprint(first).ShouldNotBe(MemorySecurityBinding.ReadFingerprint(new MemoryId(Guid.NewGuid())));
        Should.Throw<ArgumentOutOfRangeException>(() => MemorySecurityBinding.ReadFingerprint(default)).ParamName.ShouldBe("id");
    }

    [Fact]
    public void ListFingerprint_WhenFiltersAreEquivalentlyOrdered_IsStableAndOtherwiseChanges()
    {
        var baseline = MemorySecurityBinding.ListFingerprint(null, default, ["b", "a"], 0, 10);

        baseline.ShouldBe(MemorySecurityBinding.ListFingerprint(null, [MemoryLifecycleState.Active], ["a", "b"], 0, 10));
        baseline.ShouldNotBe(MemorySecurityBinding.ListFingerprint(null, default, ["a", "b"], 1, 10));
        baseline.ShouldNotBe(MemorySecurityBinding.ListFingerprint(new MemoryNamespace("n"), default, ["a", "b"], 0, 10));
        baseline.ShouldNotBe(MemorySecurityBinding.ListFingerprint(null, default, ["a", "b"], 0, 11));
    }

    [Fact]
    public void ListFingerprint_WhenBoundsAreInvalid_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => MemorySecurityBinding.ListFingerprint(null, default, default, -1, 1)).ParamName.ShouldBe("afterSequence");
        Should.Throw<ArgumentOutOfRangeException>(() => MemorySecurityBinding.ListFingerprint(null, default, default, 0, 0)).ParamName.ShouldBe("limit");
    }

    [Fact]
    public void TransitionFingerprint_WhenAnyPartDiffers_ChangesTheFingerprint()
    {
        var id = new MemoryId(Guid.NewGuid());
        var baseline = MemorySecurityBinding.TransitionFingerprint(id, MemoryLifecycleState.Expired, new VersionToken("1"), null, _key, MemoryTestData.Now);

        baseline.ShouldBe(MemorySecurityBinding.TransitionFingerprint(id, MemoryLifecycleState.Expired, new VersionToken("1"), null, _key, MemoryTestData.Now));
        baseline.ShouldNotBe(MemorySecurityBinding.TransitionFingerprint(id, MemoryLifecycleState.Active, new VersionToken("1"), null, _key, MemoryTestData.Now));
        baseline.ShouldNotBe(MemorySecurityBinding.TransitionFingerprint(id, MemoryLifecycleState.Expired, new VersionToken("2"), null, _key, MemoryTestData.Now));
        baseline.ShouldNotBe(MemorySecurityBinding.TransitionFingerprint(id, MemoryLifecycleState.Expired, new VersionToken("1"), null, _key, MemoryTestData.Now.AddTicks(1)));
    }

    [Fact]
    public void TransitionFingerprint_WhenArgumentsAreInvalid_Throws()
    {
        var id = new MemoryId(Guid.NewGuid());

        Should.Throw<ArgumentOutOfRangeException>(() => MemorySecurityBinding.TransitionFingerprint(default, MemoryLifecycleState.Expired, new VersionToken("1"), null, _key, MemoryTestData.Now)).ParamName.ShouldBe("id");
        Should.Throw<ArgumentNullException>(() => MemorySecurityBinding.TransitionFingerprint(id, MemoryLifecycleState.Expired, default, null, _key, MemoryTestData.Now)).ParamName.ShouldBe("expectedVersion");
        Should.Throw<ArgumentNullException>(() => MemorySecurityBinding.TransitionFingerprint(id, MemoryLifecycleState.Expired, new VersionToken("1"), null, default, MemoryTestData.Now)).ParamName.ShouldBe("idempotencyKey");
    }

    [Fact]
    public void DeleteFingerprint_WhenModeOrVersionDiffers_ChangesTheFingerprint()
    {
        var id = new MemoryId(Guid.NewGuid());
        var baseline = MemorySecurityBinding.DeleteFingerprint(id, null, MemoryDeleteMode.Tombstone, _key, MemoryTestData.Now);

        baseline.ShouldBe(MemorySecurityBinding.DeleteFingerprint(id, null, MemoryDeleteMode.Tombstone, _key, MemoryTestData.Now));
        baseline.ShouldNotBe(MemorySecurityBinding.DeleteFingerprint(id, null, MemoryDeleteMode.Purge, _key, MemoryTestData.Now));
        baseline.ShouldNotBe(MemorySecurityBinding.DeleteFingerprint(id, new VersionToken("1"), MemoryDeleteMode.Tombstone, _key, MemoryTestData.Now));
    }

    [Fact]
    public void DeleteFingerprint_WhenArgumentsAreInvalid_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => MemorySecurityBinding.DeleteFingerprint(default, null, MemoryDeleteMode.Tombstone, _key, MemoryTestData.Now)).ParamName.ShouldBe("id");
        Should.Throw<ArgumentNullException>(() => MemorySecurityBinding.DeleteFingerprint(new MemoryId(Guid.NewGuid()), null, MemoryDeleteMode.Tombstone, default, MemoryTestData.Now)).ParamName.ShouldBe("idempotencyKey");
    }
}
