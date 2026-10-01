// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Retrieval;

/// <summary>Verifies <see cref="RetrievalSourceIdentity"/> constraints.</summary>
public sealed class RetrievalSourceIdentityTests
{
    [Fact]
    public void Constructor_WhenValid_PreservesEveryValue()
    {
        var identity = new RetrievalSourceIdentity(new RetrievalSourceKey("s"), "2", 5);

        identity.Key.ShouldBe(new RetrievalSourceKey("s"));
        identity.Version.ShouldBe("2");
        identity.IndexWatermark.ShouldBe(5);
    }

    [Fact]
    public void Constructor_WhenKeyIsDefault_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new RetrievalSourceIdentity(default, "1", null)).ParamName.ShouldBe("key");

    [Fact]
    public void Constructor_WhenVersionIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new RetrievalSourceIdentity(new RetrievalSourceKey("s"), " ", null)).ParamName.ShouldBe("version");

    [Fact]
    public void Constructor_WhenWatermarkIsNegative_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new RetrievalSourceIdentity(new RetrievalSourceKey("s"), "1", -1)).ParamName.ShouldBe("indexWatermark");
}
