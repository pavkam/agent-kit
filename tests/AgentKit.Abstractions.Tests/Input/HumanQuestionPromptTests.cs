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

    private static HumanQuestionPrompt Prompt(ImmutableArray<HumanQuestionOption> options) => new(new QuestionId(Guid.Parse("10000000-0000-0000-0000-000000000001")), new AgentId(Guid.Parse("20000000-0000-0000-0000-000000000002")), null, new ToolCallId(Guid.Parse("30000000-0000-0000-0000-000000000003")), new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("40000000-0000-0000-0000-000000000004")), null), TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human), "Choose.", options, false, DateTimeOffset.UnixEpoch.AddMinutes(1));
}
