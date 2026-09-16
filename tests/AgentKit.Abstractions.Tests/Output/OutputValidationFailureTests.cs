// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Output;

/// <summary>Verifies OutputValidationFailure behavior and contracts.</summary>
public sealed class OutputValidationFailureTests
{
    [Fact]
    public void Constructor_WhenKindIsUndefined_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new OutputValidationFailure((OutputValidationFailureKind) 99, "message", [])).ParamName.ShouldBe("kind");

    [Fact]
    public void Constructor_WhenSafeMessageIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new OutputValidationFailure(OutputValidationFailureKind.Unknown, " ", [])).ParamName.ShouldBe("safeMessage");

    [Fact]
    public void Constructor_WhenIssuesIsDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new OutputValidationFailure(OutputValidationFailureKind.Unknown, "message", default)).ParamName.ShouldBe("issues");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var issue = OutputTestData.Issue();
        var failure = new OutputValidationFailure(OutputValidationFailureKind.ValidatorFailed, "Validation failed.", [issue]);
        failure.Kind.ShouldBe(OutputValidationFailureKind.ValidatorFailed);
        failure.SafeMessage.ShouldBe("Validation failed.");
        failure.Issues.ShouldBe([issue]);
    }

    [Fact]
    public void Equality_WhenEquivalentArraysDifferByInstance_IsStructurallyEqual()
    {
        var left = new OutputValidationFailure(OutputValidationFailureKind.ValidatorFailed, "Validation failed.", [OutputTestData.Issue()]);
        var right = new OutputValidationFailure(OutputValidationFailureKind.ValidatorFailed, "Validation failed.", [OutputTestData.Issue()]);
        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = OutputTestData.Failure();
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
