// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Documents;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="DocumentDeleteRequest"/> constraints.</summary>
public sealed class DocumentDeleteRequestTests
{
    [Fact]
    public void Constructor_WhenValid_PreservesEveryValue()
    {
        var owner = MemoryTestData.NewOwner();
        var id = new DocumentId(Guid.NewGuid());

        var request = new DocumentDeleteRequest(id, DocumentDeleteMode.Purge, new IdempotencyKey("k"), MemoryTestData.Now, MemoryTestData.Grant(owner));

        request.Id.ShouldBe(id);
        request.Mode.ShouldBe(DocumentDeleteMode.Purge);
        request.At.ShouldBe(MemoryTestData.Now);
    }

    [Fact]
    public void Constructor_WhenIdIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new DocumentDeleteRequest(default, DocumentDeleteMode.Tombstone, new IdempotencyKey("k"), MemoryTestData.Now, MemoryTestData.Grant(MemoryTestData.NewOwner()))).ParamName.ShouldBe("id");

    [Fact]
    public void Constructor_WhenModeIsUndefined_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new DocumentDeleteRequest(new DocumentId(Guid.NewGuid()), (DocumentDeleteMode) 9, new IdempotencyKey("k"), MemoryTestData.Now, MemoryTestData.Grant(MemoryTestData.NewOwner()))).ParamName.ShouldBe("mode");

    [Fact]
    public void Constructor_WhenKeyIsDefault_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new DocumentDeleteRequest(new DocumentId(Guid.NewGuid()), DocumentDeleteMode.Tombstone, default, MemoryTestData.Now, MemoryTestData.Grant(MemoryTestData.NewOwner()))).ParamName.ShouldBe("idempotencyKey");

    [Fact]
    public void Constructor_WhenGrantIsNull_ThrowsArgumentNullException()
    {
        var owner = MemoryTestData.NewOwner();

        Should.Throw<ArgumentNullException>(() => new DocumentDeleteRequest(new DocumentId(Guid.NewGuid()), DocumentDeleteMode.Tombstone, new IdempotencyKey("k"), MemoryTestData.Now, null!)).ParamName.ShouldBe("grant");
    }
}
