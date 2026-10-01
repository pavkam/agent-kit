// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Retrieval;

/// <summary>Verifies <see cref="RetrievalSourceDescriptor"/> constraints.</summary>
public sealed class RetrievalSourceDescriptorTests
{
    [Fact]
    public void Constructor_WhenValid_PreservesEveryValue()
    {
        var descriptor = new RetrievalSourceDescriptor(new RetrievalSourceKey("s"), "3", true, new ComponentId("audience"));

        descriptor.Key.ShouldBe(new RetrievalSourceKey("s"));
        descriptor.Version.ShouldBe("3");
        descriptor.RequiresEmbedding.ShouldBeTrue();
        descriptor.SecurityAudience.ShouldBe(new ComponentId("audience"));
    }

    [Fact]
    public void Constructor_WhenKeyIsDefault_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new RetrievalSourceDescriptor(default, "1", false, new ComponentId("a"))).ParamName.ShouldBe("key");

    [Fact]
    public void Constructor_WhenVersionIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new RetrievalSourceDescriptor(new RetrievalSourceKey("s"), " ", false, new ComponentId("a"))).ParamName.ShouldBe("version");

    [Fact]
    public void Constructor_WhenAudienceIsDefault_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new RetrievalSourceDescriptor(new RetrievalSourceKey("s"), "1", false, default)).ParamName.ShouldBe("securityAudience");
}
