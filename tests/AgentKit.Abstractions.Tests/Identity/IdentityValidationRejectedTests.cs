// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;

/// <summary>Verifies IdentityValidationRejected behavior and contracts.</summary>
public sealed class IdentityValidationRejectedTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var failure = Failure();
        var rejected = new IdentityValidationRejected(failure);
        rejected.Failure.ShouldBe(failure);
    }

    [Fact]
    public void Constructor_WhenFailureIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new IdentityValidationRejected(null!));
        exception.ParamName.ShouldBe("failure");
    }

    [Fact]
    public void Equals_WhenComparedThroughBaseType_UsesValueEquality()
    {
        IdentityValidationResult first = new IdentityValidationRejected(Failure());
        IdentityValidationResult second = new IdentityValidationRejected(Failure());
        first.Equals(second).ShouldBeTrue();
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new IdentityValidationRejected(Failure());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static IdentityFailure Failure() => new(IdentityFailureKind.Malformed, "malformed");
}
