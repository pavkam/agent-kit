// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.WebSearch;



/// <summary>Verifies WebSearchDenied behavior and contracts.</summary>
public sealed class WebSearchDeniedTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var id = RequestId();
        var denied = new WebSearchDenied(id, "denied");
        denied.RequestId.ShouldBe(id);
        denied.SafeMessage.ShouldBe("denied");
    }

    [Fact]
    public void Constructor_WhenSafeMessageIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new WebSearchDenied(RequestId(), " "));
        exception.ParamName.ShouldBe("safeMessage");
    }

    [Fact]
    public void Equals_WhenComparedThroughBaseType_UsesValueEquality()
    {
        WebSearchProviderResult first = new WebSearchDenied(RequestId(), "denied");
        WebSearchProviderResult second = new WebSearchDenied(RequestId(), "denied");
        first.Equals(second).ShouldBeTrue();
    }

    [Fact]
    public void With_WhenApplied_ProducesIndependentCopy()
    {
        var original = new WebSearchDenied(RequestId(), "denied");
        var copy = original with { SafeMessage = "still denied" };
        copy.SafeMessage.ShouldBe("still denied");
        original.SafeMessage.ShouldBe("denied");
    }

    private static WebSearchRequestId RequestId() => new(Guid.Parse("10000000-0000-0000-0000-000000000001"));
}
