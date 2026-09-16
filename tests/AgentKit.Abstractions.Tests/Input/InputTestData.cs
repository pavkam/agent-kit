// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Input;

internal static class InputTestData
{
    public static QuestionId QuestionId => new(Guid.Parse("10000000-0000-0000-0000-000000000001"));
    public static AgentId AgentId => new(Guid.Parse("20000000-0000-0000-0000-000000000002"));
    public static ToolCallId ToolCallId => new(Guid.Parse("30000000-0000-0000-0000-000000000003"));

    public static OperationCorrelation Correlation() =>
        new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("40000000-0000-0000-0000-000000000004")), null);

    public static ExecutionIdentity Identity() =>
        TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);

    public static ImmutableArray<HumanQuestionOption> Options() =>
        [new HumanQuestionOption(new QuestionOptionId("yes"), "Yes", "Proceed."), new HumanQuestionOption(new QuestionOptionId("no"), "No", "Stop.")];

    public static HumanQuestionPrompt Prompt() => new(
        QuestionId, AgentId, null, ToolCallId, Correlation(), Identity(), "Choose.", Options(), false, DateTimeOffset.UnixEpoch.AddMinutes(1));

    public static HumanQuestionRequest Request() => new(
        QuestionId, AgentId, null, ToolCallId, Correlation(), Identity(), "Choose.", Options(), false, DateTimeOffset.UnixEpoch.AddMinutes(1), SecurityTestData.Grant());

    public static HumanQuestionAnswer Answer() => new(new QuestionOptionId("yes"), "free text", Identity(), DateTimeOffset.UnixEpoch);
}
