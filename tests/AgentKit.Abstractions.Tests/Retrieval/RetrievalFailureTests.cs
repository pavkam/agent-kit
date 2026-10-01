// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Retrieval;

/// <summary>Verifies <see cref="RetrievalFailure"/> constraints.</summary>
public sealed class RetrievalFailureTests
{
    [Fact]
    public void Constructor_WhenValid_PreservesKindAndMessage()
    {
        var failure = new RetrievalFailure(RetrievalFailureKind.SourcesUnavailable, "none");

        failure.Kind.ShouldBe(RetrievalFailureKind.SourcesUnavailable);
        failure.SafeMessage.ShouldBe("none");
    }

    [Fact]
    public void Constructor_WhenKindIsUndefined_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new RetrievalFailure((RetrievalFailureKind) 99, "m")).ParamName.ShouldBe("kind");

    [Fact]
    public void Constructor_WhenMessageIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new RetrievalFailure(RetrievalFailureKind.Denied, " ")).ParamName.ShouldBe("safeMessage");
}
