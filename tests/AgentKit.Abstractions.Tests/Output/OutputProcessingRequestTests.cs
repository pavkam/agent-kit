// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Output;

/// <summary>Verifies OutputProcessingRequest behavior and contracts.</summary>
public sealed class OutputProcessingRequestTests
{
    [Fact]
    public void Constructor_WhenDefinitionIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new OutputProcessingRequest(null!, OutputTestData.Response(), 1)).ParamName.ShouldBe("definition");

    [Fact]
    public void Constructor_WhenResponseIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new OutputProcessingRequest(OutputTestData.Definition(), null!, 1)).ParamName.ShouldBe("response");

    [Fact]
    public void Constructor_WhenValidationAttemptIsLessThanOne_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new OutputProcessingRequest(OutputTestData.Definition(), OutputTestData.Response(), 0)).ParamName.ShouldBe("validationAttempt");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var definition = OutputTestData.Definition();
        var response = OutputTestData.Response();
        var request = new OutputProcessingRequest(definition, response, 1);
        request.Definition.ShouldBe(definition);
        request.Response.ShouldBe(response);
        request.ValidationAttempt.ShouldBe(1);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new OutputProcessingRequest(OutputTestData.Definition(), OutputTestData.Response(), 1);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
