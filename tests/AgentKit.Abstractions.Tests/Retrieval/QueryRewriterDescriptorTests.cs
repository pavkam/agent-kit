// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Retrieval;

/// <summary>Verifies <see cref="QueryRewriterDescriptor"/> constraints.</summary>
public sealed class QueryRewriterDescriptorTests
{
    [Fact]
    public void Constructor_WhenValid_PreservesKeyAndVersion()
    {
        var descriptor = new QueryRewriterDescriptor(new QueryRewriterKey("r"), new QueryRewriterVersion("2"));

        descriptor.Key.ShouldBe(new QueryRewriterKey("r"));
        descriptor.Version.ShouldBe(new QueryRewriterVersion("2"));
    }

    [Fact]
    public void Constructor_WhenKeyOrVersionIsDefault_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => new QueryRewriterDescriptor(default, new QueryRewriterVersion("1"))).ParamName.ShouldBe("key");
        Should.Throw<ArgumentNullException>(() => new QueryRewriterDescriptor(new QueryRewriterKey("r"), default)).ParamName.ShouldBe("version");
    }
}
