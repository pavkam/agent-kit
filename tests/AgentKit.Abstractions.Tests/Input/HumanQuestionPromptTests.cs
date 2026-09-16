// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Input;

using AgentKit;

using Shouldly;

/// <summary>Verifies HumanQuestionPrompt behavior and contracts.</summary>
public sealed class HumanQuestionPromptTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(11)]
    public void HumanQuestionPrompt_WhenOptionCountOutsideBoundary_ThrowsBeforeConstruction(int count)
    {
        var options = Enumerable.Range(0, count).Select(index => new HumanQuestionOption(new QuestionOptionId($"option-{index}"), "Label", "Description.")).ToImmutableArray();
        var action = () => Prompt(options);
        action.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe("options.Length");
    }

    [Fact]
    public void Constructor_WhenCorrelationIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new HumanQuestionPrompt(InputTestData.QuestionId, InputTestData.AgentId, null, InputTestData.ToolCallId, null!, InputTestData.Identity(), "Choose.", InputTestData.Options(), false, DateTimeOffset.UnixEpoch)).ParamName.ShouldBe("correlation");

    [Fact]
    public void Constructor_WhenIdentityIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new HumanQuestionPrompt(InputTestData.QuestionId, InputTestData.AgentId, null, InputTestData.ToolCallId, InputTestData.Correlation(), null!, "Choose.", InputTestData.Options(), false, DateTimeOffset.UnixEpoch)).ParamName.ShouldBe("identity");

    [Fact]
    public void Constructor_WhenPromptIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new HumanQuestionPrompt(InputTestData.QuestionId, InputTestData.AgentId, null, InputTestData.ToolCallId, InputTestData.Correlation(), InputTestData.Identity(), " ", InputTestData.Options(), false, DateTimeOffset.UnixEpoch)).ParamName.ShouldBe("prompt");

    [Fact]
    public void Constructor_WhenOptionsContainNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new HumanQuestionPrompt(InputTestData.QuestionId, InputTestData.AgentId, null, InputTestData.ToolCallId, InputTestData.Correlation(), InputTestData.Identity(), "Choose.", [null!, null!], false, DateTimeOffset.UnixEpoch)).ParamName.ShouldBe("options");

    [Fact]
    public void Constructor_WhenOptionsHaveDuplicateIds_ThrowsExactParameter()
    {
        ImmutableArray<HumanQuestionOption> options = [new HumanQuestionOption(new QuestionOptionId("yes"), "Yes", "Proceed."), new HumanQuestionOption(new QuestionOptionId("yes"), "Yes again", "Proceed.")];
        Should.Throw<ArgumentException>(() => new HumanQuestionPrompt(InputTestData.QuestionId, InputTestData.AgentId, null, InputTestData.ToolCallId, InputTestData.Correlation(), InputTestData.Identity(), "Choose.", options, false, DateTimeOffset.UnixEpoch)).ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var prompt = InputTestData.Prompt();
        prompt.Id.ShouldBe(InputTestData.QuestionId);
        prompt.AgentId.ShouldBe(InputTestData.AgentId);
        prompt.SessionId.ShouldBeNull();
        prompt.ToolCallId.ShouldBe(InputTestData.ToolCallId);
        prompt.Prompt.ShouldBe("Choose.");
        prompt.Options.ShouldBe(InputTestData.Options());
        prompt.AllowsFreeText.ShouldBeFalse();
        prompt.Deadline.ShouldBe(DateTimeOffset.UnixEpoch.AddMinutes(1));
    }

    [Fact]
    public void Equality_WhenEquivalentOptionArraysDifferByInstance_IsStructurallyEqual()
    {
        var left = InputTestData.Prompt();
        var right = InputTestData.Prompt();
        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = InputTestData.Prompt();
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static HumanQuestionPrompt Prompt(ImmutableArray<HumanQuestionOption> options) => new(new QuestionId(Guid.Parse("10000000-0000-0000-0000-000000000001")), new AgentId(Guid.Parse("20000000-0000-0000-0000-000000000002")), null, new ToolCallId(Guid.Parse("30000000-0000-0000-0000-000000000003")), new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("40000000-0000-0000-0000-000000000004")), null), TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human), "Choose.", options, false, DateTimeOffset.UnixEpoch.AddMinutes(1));
}
