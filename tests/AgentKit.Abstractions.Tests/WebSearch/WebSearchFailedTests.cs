// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.WebSearch;



/// <summary>Verifies WebSearchFailed behavior and contracts.</summary>
public sealed class WebSearchFailedTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var id = RequestId();
        var failed = new WebSearchFailed(id, "failed");
        failed.RequestId.ShouldBe(id);
        failed.SafeMessage.ShouldBe("failed");
    }

    [Fact]
    public void Constructor_WhenSafeMessageIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new WebSearchFailed(RequestId(), " "));
        exception.ParamName.ShouldBe("safeMessage");
    }

    [Fact]
    public void With_WhenApplied_ProducesIndependentCopy()
    {
        var original = new WebSearchFailed(RequestId(), "failed");
        var copy = original with { SafeMessage = "still failed" };
        copy.SafeMessage.ShouldBe("still failed");
        original.SafeMessage.ShouldBe("failed");
    }

    private static WebSearchRequestId RequestId() => new(Guid.Parse("10000000-0000-0000-0000-000000000001"));
}
