// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Usage;
/// <summary>Verifies UsagePricingReference behavior and contracts.</summary>
public sealed class UsagePricingReferenceTests
{
    [Theory]
    [InlineData(null, "source")]
    [InlineData("", "source")]
    [InlineData(" ", "source")]
    [InlineData(null, "version")]
    [InlineData("", "version")]
    [InlineData(" ", "version")]
    public void Constructor_WhenPricingProvenanceIsBlank_RejectsExactArgument(string? invalid, string parameter)
    {
        var exception = Should.Throw<ArgumentException>(() => new UsagePricingReference(parameter == "source" ? invalid! : "catalog", parameter == "version" ? invalid! : "v1"));
        exception.ParamName.ShouldBe(parameter);
        exception.GetType().ShouldBe(invalid is null ? typeof(ArgumentNullException) : typeof(ArgumentException));
    }

    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var pricing = new UsagePricingReference("catalog", "v1");
        pricing.Source.ShouldBe("catalog");
        pricing.Version.ShouldBe("v1");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new UsagePricingReference("catalog", "v1");
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
