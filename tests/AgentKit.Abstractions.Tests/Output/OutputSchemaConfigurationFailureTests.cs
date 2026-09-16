// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Output;
/// <summary>Verifies OutputSchemaConfigurationFailure behavior and contracts.</summary>
public sealed class OutputSchemaConfigurationFailureTests
{
    [Fact]
    public void ClosedResults_WhenRequiredEvidenceIsNull_RejectExactParameter() => Should.Throw<ArgumentException>(() => new OutputSchemaConfigurationFailure(OutputSchemaConfigurationFailureKind.MalformedSchema, "safe", [null!])).ParamName.ShouldBe("issues");

    [Fact]
    public void Constructor_WhenKindIsUndefined_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new OutputSchemaConfigurationFailure((OutputSchemaConfigurationFailureKind) 99, "safe", [])).ParamName.ShouldBe("kind");

    [Fact]
    public void Constructor_WhenSafeMessageIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new OutputSchemaConfigurationFailure(OutputSchemaConfigurationFailureKind.MalformedSchema, " ", [])).ParamName.ShouldBe("safeMessage");

    [Fact]
    public void Constructor_WhenIssuesIsDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new OutputSchemaConfigurationFailure(OutputSchemaConfigurationFailureKind.MalformedSchema, "safe", default)).ParamName.ShouldBe("issues");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var issue = OutputTestData.Issue();
        var failure = new OutputSchemaConfigurationFailure(OutputSchemaConfigurationFailureKind.MalformedSchema, "safe", [issue]);
        failure.Kind.ShouldBe(OutputSchemaConfigurationFailureKind.MalformedSchema);
        failure.SafeMessage.ShouldBe("safe");
        failure.Issues.ShouldBe([issue]);
    }

    [Fact]
    public void Equality_WhenEquivalentArraysDifferByInstance_IsStructurallyEqual()
    {
        var left = OutputTestData.ConfigurationFailure();
        var right = OutputTestData.ConfigurationFailure();
        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = OutputTestData.ConfigurationFailure();
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
