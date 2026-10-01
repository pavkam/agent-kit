// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Retrieval;

/// <summary>Verifies <see cref="QueryRewriterReference"/> constraints and matching.</summary>
public sealed class QueryRewriterReferenceTests
{
    [Fact]
    public void Constructor_WhenKeyOrVersionIsDefault_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => new QueryRewriterReference(default, new QueryRewriterVersion("1"))).ParamName.ShouldBe("key");
        Should.Throw<ArgumentNullException>(() => new QueryRewriterReference(new QueryRewriterKey("r"), default)).ParamName.ShouldBe("version");
    }

    [Fact]
    public void Matches_WhenKeyAndVersionAgree_ReturnsTrueAndOtherwiseFalse()
    {
        var reference = new QueryRewriterReference(new QueryRewriterKey("r"), new QueryRewriterVersion("1"));

        reference.Matches(new QueryRewriterDescriptor(new QueryRewriterKey("r"), new QueryRewriterVersion("1"))).ShouldBeTrue();
        reference.Matches(new QueryRewriterDescriptor(new QueryRewriterKey("r"), new QueryRewriterVersion("2"))).ShouldBeFalse();
        reference.Matches(new QueryRewriterDescriptor(new QueryRewriterKey("x"), new QueryRewriterVersion("1"))).ShouldBeFalse();
    }

    [Fact]
    public void Matches_WhenDescriptorIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new QueryRewriterReference(new QueryRewriterKey("r"), new QueryRewriterVersion("1")).Matches(null!)).ParamName.ShouldBe("descriptor");
}
