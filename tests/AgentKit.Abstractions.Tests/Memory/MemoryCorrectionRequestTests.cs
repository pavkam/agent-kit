// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Memory;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="MemoryCorrectionRequest"/> constraints.</summary>
public sealed class MemoryCorrectionRequestTests
{
    private static readonly MemoryId _id = new(Guid.NewGuid());
    private static readonly MemoryId _replacement = new(Guid.NewGuid());
    private static readonly Provenance _provenance = new("user");

    [Fact]
    public void Constructor_WhenValid_PreservesEveryValue()
    {
        var owner = MemoryTestData.NewOwner();

        var request = new MemoryCorrectionRequest(owner.Context, _id, new VersionToken("2"), _replacement, new MemoryContent("fixed"), _provenance, new IdempotencyKey("k"));

        request.Context.ShouldBe(owner.Context);
        request.Id.ShouldBe(_id);
        request.ExpectedVersion.ShouldBe(new VersionToken("2"));
        request.ReplacementId.ShouldBe(_replacement);
        request.Content.Text.ShouldBe("fixed");
    }

    [Fact]
    public void Constructor_WhenContextIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new MemoryCorrectionRequest(null!, _id, new VersionToken("1"), _replacement, new MemoryContent("c"), _provenance, new IdempotencyKey("k"))).ParamName.ShouldBe("context");

    [Fact]
    public void Constructor_WhenIdentitiesAreDefaultOrEqual_ThrowsArgumentOutOfRangeException()
    {
        var owner = MemoryTestData.NewOwner();

        Should.Throw<ArgumentOutOfRangeException>(() => new MemoryCorrectionRequest(owner.Context, default, new VersionToken("1"), _replacement, new MemoryContent("c"), _provenance, new IdempotencyKey("k"))).ParamName.ShouldBe("id");
        Should.Throw<ArgumentOutOfRangeException>(() => new MemoryCorrectionRequest(owner.Context, _id, new VersionToken("1"), default, new MemoryContent("c"), _provenance, new IdempotencyKey("k"))).ParamName.ShouldBe("replacementId");
        Should.Throw<ArgumentOutOfRangeException>(() => new MemoryCorrectionRequest(owner.Context, _id, new VersionToken("1"), _id, new MemoryContent("c"), _provenance, new IdempotencyKey("k"))).ParamName.ShouldBe("replacementId");
    }

    [Fact]
    public void Constructor_WhenVersionOrKeyIsDefault_ThrowsArgumentNullException()
    {
        var owner = MemoryTestData.NewOwner();

        Should.Throw<ArgumentNullException>(() => new MemoryCorrectionRequest(owner.Context, _id, default, _replacement, new MemoryContent("c"), _provenance, new IdempotencyKey("k"))).ParamName.ShouldBe("expectedVersion");
        Should.Throw<ArgumentNullException>(() => new MemoryCorrectionRequest(owner.Context, _id, new VersionToken("1"), _replacement, new MemoryContent("c"), _provenance, default)).ParamName.ShouldBe("idempotencyKey");
    }

    [Fact]
    public void Constructor_WhenContentOrProvenanceIsNull_ThrowsArgumentNullException()
    {
        var owner = MemoryTestData.NewOwner();

        Should.Throw<ArgumentNullException>(() => new MemoryCorrectionRequest(owner.Context, _id, new VersionToken("1"), _replacement, null!, _provenance, new IdempotencyKey("k"))).ParamName.ShouldBe("content");
        Should.Throw<ArgumentNullException>(() => new MemoryCorrectionRequest(owner.Context, _id, new VersionToken("1"), _replacement, new MemoryContent("c"), null!, new IdempotencyKey("k"))).ParamName.ShouldBe("provenance");
    }
}
