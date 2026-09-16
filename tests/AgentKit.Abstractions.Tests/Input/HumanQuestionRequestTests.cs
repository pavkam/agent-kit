// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Input;

/// <summary>Verifies HumanQuestionRequest behavior and contracts.</summary>
public sealed class HumanQuestionRequestTests
{
    [Fact]
    public void Constructor_WhenCorrelationIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new HumanQuestionRequest(InputTestData.QuestionId, InputTestData.AgentId, null, InputTestData.ToolCallId, null!, InputTestData.Identity(), "Choose.", InputTestData.Options(), false, DateTimeOffset.UnixEpoch, SecurityTestData.Grant())).ParamName.ShouldBe("correlation");

    [Fact]
    public void Constructor_WhenIdentityIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new HumanQuestionRequest(InputTestData.QuestionId, InputTestData.AgentId, null, InputTestData.ToolCallId, InputTestData.Correlation(), null!, "Choose.", InputTestData.Options(), false, DateTimeOffset.UnixEpoch, SecurityTestData.Grant())).ParamName.ShouldBe("identity");

    [Fact]
    public void Constructor_WhenPromptIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new HumanQuestionRequest(InputTestData.QuestionId, InputTestData.AgentId, null, InputTestData.ToolCallId, InputTestData.Correlation(), InputTestData.Identity(), " ", InputTestData.Options(), false, DateTimeOffset.UnixEpoch, SecurityTestData.Grant())).ParamName.ShouldBe("prompt");

    [Fact]
    public void Constructor_WhenOptionsContainNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new HumanQuestionRequest(InputTestData.QuestionId, InputTestData.AgentId, null, InputTestData.ToolCallId, InputTestData.Correlation(), InputTestData.Identity(), "Choose.", [null!, null!], false, DateTimeOffset.UnixEpoch, SecurityTestData.Grant())).ParamName.ShouldBe("options");

    [Fact]
    public void Constructor_WhenOptionCountIsOutsideBoundary_ThrowsExactParameter() => Should.Throw<ArgumentOutOfRangeException>(() => new HumanQuestionRequest(InputTestData.QuestionId, InputTestData.AgentId, null, InputTestData.ToolCallId, InputTestData.Correlation(), InputTestData.Identity(), "Choose.", [], false, DateTimeOffset.UnixEpoch, SecurityTestData.Grant())).ParamName.ShouldBe("options.Length");

    [Fact]
    public void Constructor_WhenOptionsHaveDuplicateIds_ThrowsExactParameter()
    {
        ImmutableArray<HumanQuestionOption> options = [new HumanQuestionOption(new QuestionOptionId("yes"), "Yes", "Proceed."), new HumanQuestionOption(new QuestionOptionId("yes"), "Yes again", "Proceed.")];
        Should.Throw<ArgumentException>(() => new HumanQuestionRequest(InputTestData.QuestionId, InputTestData.AgentId, null, InputTestData.ToolCallId, InputTestData.Correlation(), InputTestData.Identity(), "Choose.", options, false, DateTimeOffset.UnixEpoch, SecurityTestData.Grant())).ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenGrantIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new HumanQuestionRequest(InputTestData.QuestionId, InputTestData.AgentId, null, InputTestData.ToolCallId, InputTestData.Correlation(), InputTestData.Identity(), "Choose.", InputTestData.Options(), false, DateTimeOffset.UnixEpoch, null!)).ParamName.ShouldBe("grant");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var request = InputTestData.Request();
        request.Id.ShouldBe(InputTestData.QuestionId);
        request.AgentId.ShouldBe(InputTestData.AgentId);
        request.SessionId.ShouldBeNull();
        request.ToolCallId.ShouldBe(InputTestData.ToolCallId);
        request.Prompt.ShouldBe("Choose.");
        request.Options.ShouldBe(InputTestData.Options());
        request.AllowsFreeText.ShouldBeFalse();
        request.Deadline.ShouldBe(DateTimeOffset.UnixEpoch.AddMinutes(1));
    }

    [Fact]
    public void Equality_WhenEquivalentOptionArraysDifferByInstance_IsStructurallyEqual()
    {
        var left = InputTestData.Request();
        var right = InputTestData.Request();
        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = InputTestData.Request();
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
