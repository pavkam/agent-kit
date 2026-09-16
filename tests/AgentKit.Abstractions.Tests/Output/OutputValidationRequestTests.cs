// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Output;

/// <summary>Verifies OutputValidationRequest behavior and contracts.</summary>
public sealed class OutputValidationRequestTests
{
    [Fact]
    public void Constructor_WhenDefinitionIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new OutputValidationRequest(null!, OutputTestData.Candidate(), 1)).ParamName.ShouldBe("definition");

    [Fact]
    public void Constructor_WhenCandidateIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new OutputValidationRequest(OutputTestData.Definition(), null!, 1)).ParamName.ShouldBe("candidate");

    [Fact]
    public void Constructor_WhenValidationAttemptIsLessThanOne_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new OutputValidationRequest(OutputTestData.Definition(), OutputTestData.Candidate(), 0)).ParamName.ShouldBe("validationAttempt");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var definition = OutputTestData.Definition();
        var candidate = OutputTestData.Candidate();
        var request = new OutputValidationRequest(definition, candidate, 1);
        request.Definition.ShouldBe(definition);
        request.Candidate.ShouldBe(candidate);
        request.ValidationAttempt.ShouldBe(1);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new OutputValidationRequest(OutputTestData.Definition(), OutputTestData.Candidate(), 1);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
