// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Retrieval;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="RetrievalSourceRequest"/> constraints.</summary>
public sealed class RetrievalSourceRequestTests
{
    private static readonly RetrievalSourceStores _stores = new(null, null, default);

    [Fact]
    public void Constructor_WhenValid_PreservesEveryValue()
    {
        var owner = MemoryTestData.NewOwner();
        var query = MemoryTestData.Query(owner);
        var grant = MemoryTestData.Grant(owner);

        var request = new RetrievalSourceRequest(query, null, 5, grant, _stores);

        request.Query.ShouldBe(query);
        request.Embedding.ShouldBeNull();
        request.Limit.ShouldBe(5);
        request.Grant.ShouldBe(grant);
        request.Stores.ShouldBe(_stores);
    }

    [Fact]
    public void Constructor_WhenQueryOrStoresAreNull_ThrowsArgumentNullException()
    {
        var owner = MemoryTestData.NewOwner();

        Should.Throw<ArgumentNullException>(() => new RetrievalSourceRequest(null!, null, 1, MemoryTestData.Grant(owner), _stores)).ParamName.ShouldBe("query");
        Should.Throw<ArgumentNullException>(() => new RetrievalSourceRequest(MemoryTestData.Query(owner), null, 1, MemoryTestData.Grant(owner), null!)).ParamName.ShouldBe("stores");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Constructor_WhenLimitIsNotPositive_ThrowsArgumentOutOfRangeException(int limit)
    {
        var owner = MemoryTestData.NewOwner();

        Should.Throw<ArgumentOutOfRangeException>(() => new RetrievalSourceRequest(MemoryTestData.Query(owner), null, limit, MemoryTestData.Grant(owner), _stores)).ParamName.ShouldBe("limit");
    }

    [Fact]
    public void Constructor_WhenGrantIsNull_ThrowsArgumentNullException()
    {
        var owner = MemoryTestData.NewOwner();

        Should.Throw<ArgumentNullException>(() => new RetrievalSourceRequest(MemoryTestData.Query(owner), null, 1, null!, _stores)).ParamName.ShouldBe("grant");
    }
}
