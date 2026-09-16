// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

/// <summary>Verifies FileSearchResult behavior and contracts.</summary>
public sealed class FileSearchResultTests
{
    [Fact]
    public void Constructor_WhenStatusIsUndefined_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new FileSearchResult((FileSearchStatus) 99, [], 0, 0, true, null)).ParamName.ShouldBe("status");

    [Fact]
    public void Constructor_WhenMatchesAreDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new FileSearchResult(FileSearchStatus.NoMatches, default, 0, 0, true, null)).ParamName.ShouldBe("matches");

    [Fact]
    public void Constructor_WhenVisitedFilesIsNegative_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new FileSearchResult(FileSearchStatus.NoMatches, [], -1, 0, true, null)).ParamName.ShouldBe("visitedFiles");

    [Fact]
    public void Constructor_WhenVisitedBytesIsNegative_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new FileSearchResult(FileSearchStatus.NoMatches, [], 0, -1, true, null)).ParamName.ShouldBe("visitedBytes");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var match = new FileSearchMatch(new FileSystemPath("a.txt"), new ContentHash("sha256:content"), 1, 0, 0, 4, 0, "text", false);
        var result = new FileSearchResult(FileSearchStatus.Success, [match], 3, 10, true, "Complete.");
        result.Status.ShouldBe(FileSearchStatus.Success);
        result.Matches.ShouldBe([match]);
        result.VisitedFiles.ShouldBe(3);
        result.VisitedBytes.ShouldBe(10);
        result.Complete.ShouldBeTrue();
        result.SafeMessage.ShouldBe("Complete.");
    }

    [Fact]
    public void Equality_WhenEquivalentArraysDifferByInstance_IsStructurallyEqual()
    {
        var match = new FileSearchMatch(new FileSystemPath("a.txt"), new ContentHash("sha256:content"), 1, 0, 0, 4, 0, "text", false);
        var left = new FileSearchResult(FileSearchStatus.Success, [match], 3, 10, true, null);
        var right = new FileSearchResult(FileSearchStatus.Success, [match], 3, 10, true, null);
        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new FileSearchResult(FileSearchStatus.NoMatches, [], 0, 0, true, null);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
