// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

using AgentKit;

/// <summary>Verifies DirectoryEnumerationRequest behavior and contracts.</summary>
public sealed class DirectoryEnumerationRequestTests
{
    [Fact]
    public void DirectoryEnumerationRequest_WhenMaximumEntriesIsZero_ThrowsBeforeGrantAssignment()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new DirectoryEnumerationRequest(null, 0, null, SecurityTestData.Grant()));
        exception.ParamName.ShouldBe("maximumEntries");
    }

    [Fact]
    public void DirectoryEnumerationRequest_WhenGrantIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DirectoryEnumerationRequest(null, 1, null, null!));
        exception.ParamName.ShouldBe("grant");
    }

    [Fact]
    public void DirectoryEnumerationRequest_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var path = new FileSystemPath("src");
        var continuation = new DirectoryEnumerationCursor(new ContentHash("sha256:snapshot"), 1);
        var grant = SecurityTestData.Grant();
        var request = new DirectoryEnumerationRequest(path, 10, continuation, grant);
        request.Path.ShouldBe(path);
        request.MaximumEntries.ShouldBe(10);
        request.Continuation.ShouldBe(continuation);
        request.Grant.ShouldBeSameAs(grant);
    }

    [Fact]
    public void DirectoryEnumerationRequest_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new DirectoryEnumerationRequest(new FileSystemPath("src"), 10, null, SecurityTestData.Grant());
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
