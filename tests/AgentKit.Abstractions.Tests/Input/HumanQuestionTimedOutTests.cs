// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Input;

/// <summary>Verifies HumanQuestionTimedOut behavior and contracts.</summary>
public sealed class HumanQuestionTimedOutTests
{
    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsQuestionId()
    {
        var timedOut = new HumanQuestionTimedOut(InputTestData.QuestionId);
        timedOut.QuestionId.ShouldBe(InputTestData.QuestionId);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new HumanQuestionTimedOut(InputTestData.QuestionId);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
