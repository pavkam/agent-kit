// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Vectors;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="VectorSearchRequest"/> constraints.</summary>
public sealed class VectorSearchRequestTests
{
    private static SecurityGrant Grant() => MemoryTestData.Grant(MemoryTestData.NewOwner());

    [Fact]
    public void Constructor_WhenValid_PreservesEveryValue()
    {
        var document = new DocumentId(Guid.NewGuid());

        var request = new VectorSearchRequest(MemoryTestData.Space(), [1f, 0f, 0f], 5, [document], Grant());

        request.Query.ShouldBe([1f, 0f, 0f]);
        request.TopK.ShouldBe(5);
        request.Documents.ShouldBe([document]);
    }

    [Fact]
    public void Constructor_WhenDocumentsAreDefault_SearchesEveryVisibleDocument() =>
        new VectorSearchRequest(MemoryTestData.Space(), [1f, 0f, 0f], 5, default, Grant()).Documents.ShouldBeEmpty();

    [Fact]
    public void Constructor_WhenSpaceIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new VectorSearchRequest(null!, [1f], 1, default, Grant())).ParamName.ShouldBe("space");

    [Fact]
    public void Constructor_WhenQueryIsDefaultOrEmpty_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => new VectorSearchRequest(MemoryTestData.Space(), default, 1, default, Grant())).ParamName.ShouldBe("query");
        Should.Throw<ArgumentException>(() => new VectorSearchRequest(MemoryTestData.Space(), [], 1, default, Grant())).ParamName.ShouldBe("query");
    }

    [Fact]
    public void Constructor_WhenQueryHasTheWrongDimension_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new VectorSearchRequest(MemoryTestData.Space(dimensions: 3), [1f, 0f], 1, default, Grant())).ParamName.ShouldBe("query");

    [Fact]
    public void Constructor_WhenQueryComponentIsNotFinite_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new VectorSearchRequest(MemoryTestData.Space(), [1f, float.NaN, 0f], 1, default, Grant())).ParamName.ShouldBe("query");

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(VectorSearchRequest.MaximumTopK + 1)]
    public void Constructor_WhenTopKIsOutOfRange_ThrowsArgumentOutOfRangeException(int topK) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new VectorSearchRequest(MemoryTestData.Space(), [1f, 0f, 0f], topK, default, Grant())).ParamName.ShouldBe("topK");

    [Fact]
    public void Constructor_WhenADocumentIdentityIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new VectorSearchRequest(MemoryTestData.Space(), [1f, 0f, 0f], 1, [default], Grant())).ParamName.ShouldBe("documents");

    [Fact]
    public void Constructor_WhenGrantIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new VectorSearchRequest(MemoryTestData.Space(), [1f, 0f, 0f], 1, default, null!)).ParamName.ShouldBe("grant");
}
