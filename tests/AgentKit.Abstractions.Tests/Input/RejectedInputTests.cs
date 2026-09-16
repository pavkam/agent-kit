// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Input;

/// <summary>Verifies RejectedInput behavior and contracts.</summary>
public sealed class RejectedInputTests
{
    [Fact]
    public void Constructor_WhenRejectionIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new RejectedInput(null!)).ParamName.ShouldBe("rejection");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsRejection()
    {
        var rejection = new InputRejection(InputRejectionKind.InvalidInput, "Invalid.");
        var rejected = new RejectedInput(rejection);
        rejected.Rejection.ShouldBeSameAs(rejection);
        InputAdmissionResult result = rejected;
        _ = result.ShouldBeOfType<RejectedInput>();
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new RejectedInput(new InputRejection(InputRejectionKind.InvalidInput, "Invalid."));
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
