// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.WebSearch;



/// <summary>Verifies WebSearchUnavailable behavior and contracts.</summary>
public sealed class WebSearchUnavailableTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var id = RequestId();
        var unavailable = new WebSearchUnavailable(id, "unavailable");
        unavailable.RequestId.ShouldBe(id);
        unavailable.SafeMessage.ShouldBe("unavailable");
    }

    [Fact]
    public void Constructor_WhenSafeMessageIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new WebSearchUnavailable(RequestId(), " "));
        exception.ParamName.ShouldBe("safeMessage");
    }

    [Fact]
    public void With_WhenApplied_ProducesIndependentCopy()
    {
        var original = new WebSearchUnavailable(RequestId(), "unavailable");
        var copy = original with { SafeMessage = "still unavailable" };
        copy.SafeMessage.ShouldBe("still unavailable");
        original.SafeMessage.ShouldBe("unavailable");
    }

    private static WebSearchRequestId RequestId() => new(Guid.Parse("10000000-0000-0000-0000-000000000001"));
}
