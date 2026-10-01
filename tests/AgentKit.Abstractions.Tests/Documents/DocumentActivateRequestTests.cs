// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Documents;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="DocumentActivateRequest"/> constraints.</summary>
public sealed class DocumentActivateRequestTests
{
    private static readonly DocumentId _id = new(Guid.NewGuid());

    [Fact]
    public void Constructor_WhenValid_PreservesEveryValue()
    {
        var owner = MemoryTestData.NewOwner();

        var request = new DocumentActivateRequest(_id, new DocumentVersion("v2"), new DocumentVersion("v1"), new IdempotencyKey("k"), MemoryTestData.Now, MemoryTestData.Grant(owner));

        request.Id.ShouldBe(_id);
        request.Version.ShouldBe(new DocumentVersion("v2"));
        request.ExpectedActiveVersion.ShouldBe(new DocumentVersion("v1"));
    }

    [Fact]
    public void Constructor_WhenNoActiveVersionIsExpected_AcceptsNull()
    {
        var owner = MemoryTestData.NewOwner();

        new DocumentActivateRequest(_id, new DocumentVersion("v1"), null, new IdempotencyKey("k"), MemoryTestData.Now, MemoryTestData.Grant(owner)).ExpectedActiveVersion.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WhenIdIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new DocumentActivateRequest(default, new DocumentVersion("v"), null, new IdempotencyKey("k"), MemoryTestData.Now, MemoryTestData.Grant(MemoryTestData.NewOwner()))).ParamName.ShouldBe("id");

    [Fact]
    public void Constructor_WhenVersionsAreDefault_ThrowsArgumentNullException()
    {
        var owner = MemoryTestData.NewOwner();

        Should.Throw<ArgumentNullException>(() => new DocumentActivateRequest(_id, default, null, new IdempotencyKey("k"), MemoryTestData.Now, MemoryTestData.Grant(owner))).ParamName.ShouldBe("version");
        Should.Throw<ArgumentNullException>(() => new DocumentActivateRequest(_id, new DocumentVersion("v"), default(DocumentVersion), new IdempotencyKey("k"), MemoryTestData.Now, MemoryTestData.Grant(owner))).ParamName.ShouldBe("expectedActiveVersion");
    }

    [Fact]
    public void Constructor_WhenKeyIsDefault_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new DocumentActivateRequest(_id, new DocumentVersion("v"), null, default, MemoryTestData.Now, MemoryTestData.Grant(MemoryTestData.NewOwner()))).ParamName.ShouldBe("idempotencyKey");

    [Fact]
    public void Constructor_WhenGrantIsNull_ThrowsArgumentNullException()
    {
        var owner = MemoryTestData.NewOwner();

        Should.Throw<ArgumentNullException>(() => new DocumentActivateRequest(_id, new DocumentVersion("v"), null, new IdempotencyKey("k"), MemoryTestData.Now, null!)).ParamName.ShouldBe("grant");
    }
}
