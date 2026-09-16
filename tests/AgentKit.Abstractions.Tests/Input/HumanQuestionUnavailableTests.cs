// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Input;

/// <summary>Verifies HumanQuestionUnavailable behavior and contracts.</summary>
public sealed class HumanQuestionUnavailableTests
{
    [Fact]
    public void Constructor_WhenSafeMessageIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new HumanQuestionUnavailable(InputTestData.QuestionId, " ")).ParamName.ShouldBe("safeMessage");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var unavailable = new HumanQuestionUnavailable(InputTestData.QuestionId, "Channel unavailable.");
        unavailable.QuestionId.ShouldBe(InputTestData.QuestionId);
        unavailable.SafeMessage.ShouldBe("Channel unavailable.");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new HumanQuestionUnavailable(InputTestData.QuestionId, "Channel unavailable.");
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
