// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Memory;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="MemoryTransitionRequest"/> constraints.</summary>
public sealed class MemoryTransitionRequestTests
{
    private static readonly IdempotencyKey _key = new("k");

    [Fact]
    public void Constructor_WhenPlainTransition_PreservesEveryValue()
    {
        var owner = MemoryTestData.NewOwner();
        var id = new MemoryId(Guid.NewGuid());
        var grant = MemoryTestData.Grant(owner);

        var request = new MemoryTransitionRequest(id, MemoryLifecycleState.Expired, new VersionToken("1"), null, _key, MemoryTestData.Now, grant);

        request.Id.ShouldBe(id);
        request.To.ShouldBe(MemoryLifecycleState.Expired);
        request.ExpectedVersion.ShouldBe(new VersionToken("1"));
        request.Replacement.ShouldBeNull();
        request.At.ShouldBe(MemoryTestData.Now);
        request.Grant.ShouldBe(grant);
    }

    [Fact]
    public void Constructor_WhenCorrectionCarriesAnActiveReplacement_Accepts()
    {
        var owner = MemoryTestData.NewOwner();
        var replacement = MemoryTestData.Record(owner);

        var request = new MemoryTransitionRequest(new MemoryId(Guid.NewGuid()), MemoryLifecycleState.Corrected, new VersionToken("1"), replacement, _key, MemoryTestData.Now, MemoryTestData.Grant(owner));

        request.Replacement.ShouldBe(replacement);
    }

    [Fact]
    public void Constructor_WhenIdIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Create(id: default(MemoryId))).ParamName.ShouldBe("id");

    [Fact]
    public void Constructor_WhenTargetIsUndefined_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Create(to: (MemoryLifecycleState) 99)).ParamName.ShouldBe("to");

    [Fact]
    public void Constructor_WhenTargetIsDeleted_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => Create(to: MemoryLifecycleState.Deleted)).ParamName.ShouldBe("to");

    [Fact]
    public void Constructor_WhenExpectedVersionIsDefault_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => Create(expected: default(VersionToken))).ParamName.ShouldBe("expectedVersion");

    [Fact]
    public void Constructor_WhenCorrectionLacksAReplacement_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => Create(to: MemoryLifecycleState.Corrected)).ParamName.ShouldBe("replacement");

    [Fact]
    public void Constructor_WhenAnotherTransitionCarriesAReplacement_ThrowsArgumentException()
    {
        var owner = MemoryTestData.NewOwner();

        Should.Throw<ArgumentException>(() => Create(replacement: MemoryTestData.Record(owner))).ParamName.ShouldBe("replacement");
    }

    [Fact]
    public void Constructor_WhenReplacementIsNotActive_ThrowsArgumentException()
    {
        var owner = MemoryTestData.NewOwner();

        Should.Throw<ArgumentException>(() => Create(to: MemoryLifecycleState.Corrected, replacement: MemoryTestData.Record(owner, state: MemoryLifecycleState.Accepted))).ParamName.ShouldBe("replacement");
    }

    [Fact]
    public void Constructor_WhenReplacementIsNotAtTheInitialVersion_ThrowsArgumentException()
    {
        var owner = MemoryTestData.NewOwner();

        Should.Throw<ArgumentException>(() => Create(to: MemoryLifecycleState.Corrected, replacement: MemoryTestData.Record(owner, version: "2"))).ParamName.ShouldBe("replacement");
    }

    [Fact]
    public void Constructor_WhenReplacementReusesTheCorrectedIdentity_ThrowsArgumentException()
    {
        var owner = MemoryTestData.NewOwner();
        var id = new MemoryId(Guid.NewGuid());

        Should.Throw<ArgumentException>(() => Create(id: id, to: MemoryLifecycleState.Corrected, replacement: MemoryTestData.Record(owner, id: id))).ParamName.ShouldBe("replacement");
    }

    [Fact]
    public void Constructor_WhenKeyIsDefault_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => Create(key: default(IdempotencyKey))).ParamName.ShouldBe("idempotencyKey");

    [Fact]
    public void Constructor_WhenGrantIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => Create(nullGrant: true)).ParamName.ShouldBe("grant");

    private static MemoryTransitionRequest Create(
        MemoryId? id = null,
        MemoryLifecycleState to = MemoryLifecycleState.Expired,
        VersionToken? expected = null,
        DurableMemoryRecord? replacement = null,
        IdempotencyKey? key = null,
        bool nullGrant = false)
    {
        var owner = MemoryTestData.NewOwner();
        return new(
            id ?? new MemoryId(Guid.NewGuid()), to, expected ?? new VersionToken("1"), replacement, key ?? _key, MemoryTestData.Now,
            nullGrant ? null! : MemoryTestData.Grant(owner));
    }
}
