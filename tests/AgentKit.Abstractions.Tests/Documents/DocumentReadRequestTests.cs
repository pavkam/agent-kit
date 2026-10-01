// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Documents;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="DocumentReadRequest"/> constraints.</summary>
public sealed class DocumentReadRequestTests
{
    [Fact]
    public void Constructor_WhenNoVersionIsNamed_ResolvesTheActiveVersion()
    {
        var owner = MemoryTestData.NewOwner();

        var request = new DocumentReadRequest(new DocumentId(Guid.NewGuid()), null, true, MemoryTestData.Grant(owner));

        request.Version.ShouldBeNull();
        request.IncludeChunks.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WhenIdIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new DocumentReadRequest(default, null, false, MemoryTestData.Grant(MemoryTestData.NewOwner()))).ParamName.ShouldBe("id");

    [Fact]
    public void Constructor_WhenVersionIsDefault_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new DocumentReadRequest(new DocumentId(Guid.NewGuid()), default(DocumentVersion), false, MemoryTestData.Grant(MemoryTestData.NewOwner()))).ParamName.ShouldBe("version");

    [Fact]
    public void Constructor_WhenGrantIsNull_ThrowsArgumentNullException()
    {
        var owner = MemoryTestData.NewOwner();

        Should.Throw<ArgumentNullException>(() => new DocumentReadRequest(new DocumentId(Guid.NewGuid()), null, false, null!)).ParamName.ShouldBe("grant");
    }
}
