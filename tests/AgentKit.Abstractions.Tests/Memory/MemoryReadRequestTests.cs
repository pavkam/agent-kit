// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Memory;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="MemoryReadRequest"/> constraints.</summary>
public sealed class MemoryReadRequestTests
{
    [Fact]
    public void Constructor_WhenValid_PreservesIdentityAndGrant()
    {
        var owner = MemoryTestData.NewOwner();
        var id = new MemoryId(Guid.NewGuid());
        var grant = MemoryTestData.Grant(owner);

        var request = new MemoryReadRequest(id, grant);

        request.Id.ShouldBe(id);
        request.Grant.ShouldBe(grant);
    }

    [Fact]
    public void Constructor_WhenIdIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new MemoryReadRequest(default, MemoryTestData.Grant(MemoryTestData.NewOwner()))).ParamName.ShouldBe("id");

    [Fact]
    public void Constructor_WhenGrantIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new MemoryReadRequest(new MemoryId(Guid.NewGuid()), null!)).ParamName.ShouldBe("grant");
}
