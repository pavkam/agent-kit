// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Output;

/// <summary>Verifies OutputConfigurationRejected behavior and contracts.</summary>
public sealed class OutputConfigurationRejectedTests
{
    [Fact]
    public void Constructor_WhenFailureIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new OutputConfigurationRejected(null!)).ParamName.ShouldBe("failure");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsFailure()
    {
        var failure = OutputTestData.ConfigurationFailure();
        var rejected = new OutputConfigurationRejected(failure);
        rejected.Failure.ShouldBe(failure);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new OutputConfigurationRejected(OutputTestData.ConfigurationFailure());
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
