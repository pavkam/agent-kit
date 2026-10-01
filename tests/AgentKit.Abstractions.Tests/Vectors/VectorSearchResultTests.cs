// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Vectors;

/// <summary>Verifies <see cref="VectorSearchResult"/> factories.</summary>
public sealed class VectorSearchResultTests
{
    [Fact]
    public void Searched_WhenValid_ReportsMatchesAndWatermark()
    {
        var match = new VectorMatch(new ChunkId(Guid.NewGuid()), new DocumentId(Guid.NewGuid()), new DocumentVersion("v"), 1, new ContentHash("h"));

        var result = VectorSearchResult.Searched([match], 4);

        result.IsSearched.ShouldBeTrue();
        result.Matches.ShouldBe([match]);
        result.Watermark.ShouldBe(4);
        result.Failure.ShouldBeNull();
    }

    [Fact]
    public void Searched_WhenArgumentsAreInvalid_Throws()
    {
        Should.Throw<ArgumentException>(() => VectorSearchResult.Searched(default, 0)).ParamName.ShouldBe("matches");
        Should.Throw<ArgumentException>(() => VectorSearchResult.Searched([null!], 0)).ParamName.ShouldBe("matches");
        Should.Throw<ArgumentOutOfRangeException>(() => VectorSearchResult.Searched([], -1)).ParamName.ShouldBe("watermark");
    }

    [Fact]
    public void Rejected_WhenFailureIsSupplied_CarriesNoMatches()
    {
        var failure = new MemoryStoreFailure(MemoryStoreFailureKind.IncompatibleVectorSpace, "space");

        var result = VectorSearchResult.Rejected(failure);

        result.IsSearched.ShouldBeFalse();
        result.Matches.ShouldBeEmpty();
        result.Failure.ShouldBe(failure);
    }

    [Fact]
    public void Rejected_WhenFailureIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => VectorSearchResult.Rejected(null!)).ParamName.ShouldBe("failure");
}
