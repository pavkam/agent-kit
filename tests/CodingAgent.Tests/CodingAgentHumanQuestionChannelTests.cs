// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent.Tests;

using AgentKit;

/// <summary>Verifies the terminal channel authenticates one exact bounded human answer.</summary>
public sealed class CodingAgentHumanQuestionChannelTests
{
    [Fact]
    public async Task AskAsync_WhenSelectionIsPresented_ReturnsOneAuthenticatedAnswer()
    {
        var identity = ApprovalTestData.Identity();
        var question = Question(identity);
        var channel = new CodingAgentHumanQuestionChannel(
            new DelegateHumanQuestionPrompt((actual, _) =>
            {
                actual.ShouldBeSameAs(question);
                return ValueTask.FromResult(new HumanQuestionSelection(actual.Options[1].Id, "Prefer the safer change."));
            }),
            identity,
            TimeProvider.System);

        var result = await channel.AskAsync(question, TestContext.Current.CancellationToken);

        var answer = result.ShouldBeOfType<HumanQuestionAnswered>().Answer;
        answer.SelectedOptionId.ShouldBe(question.Options[1].Id);
        answer.FreeText.ShouldBe("Prefer the safer change.");
        answer.Respondent.ShouldBe(identity);
    }

    [Fact]
    public async Task AskAsync_WhenQuestionIdentityDiffers_DoesNotDisplayQuestion()
    {
        var displayed = false;
        var identity = ApprovalTestData.Identity();
        var channel = new CodingAgentHumanQuestionChannel(
            new DelegateHumanQuestionPrompt((_, _) =>
            {
                displayed = true;
                return ValueTask.FromResult(default(HumanQuestionSelection));
            }),
            identity,
            TimeProvider.System);

        var result = await channel.AskAsync(
            Question(ApprovalTestData.Identity("other")),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<HumanQuestionUnavailable>();
        displayed.ShouldBeFalse();
    }

    [Fact]
    public async Task AskAsync_WhenTerminalReturnsUnknownOption_ReturnsUnavailable()
    {
        var identity = ApprovalTestData.Identity();
        var channel = new CodingAgentHumanQuestionChannel(
            new DelegateHumanQuestionPrompt((_, _) =>
                ValueTask.FromResult(new HumanQuestionSelection(new QuestionOptionId("unknown"), null))),
            identity,
            TimeProvider.System);

        var result = await channel.AskAsync(Question(identity), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<HumanQuestionUnavailable>();
    }

    [Fact]
    public async Task AskAsync_WhenCallerCancels_PropagatesWithoutFabricatingAnswer()
    {
        var identity = ApprovalTestData.Identity();
        var channel = new CodingAgentHumanQuestionChannel(
            new DelegateHumanQuestionPrompt((_, cancellationToken) =>
                ValueTask.FromCanceled<HumanQuestionSelection>(cancellationToken)),
            identity,
            TimeProvider.System);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await channel.AskAsync(Question(identity), cancellation.Token));
    }

    [Fact]
    public async Task AskAsync_WhenDeadlineAlreadyPassed_DoesNotDisplayQuestion()
    {
        var displayed = false;
        var identity = ApprovalTestData.Identity();
        var channel = new CodingAgentHumanQuestionChannel(
            new DelegateHumanQuestionPrompt((_, _) =>
            {
                displayed = true;
                return ValueTask.FromResult(default(HumanQuestionSelection));
            }),
            identity,
            TimeProvider.System);

        var result = await channel.AskAsync(
            Question(identity, DateTimeOffset.UnixEpoch.AddMinutes(1)),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<HumanQuestionTimedOut>();
        displayed.ShouldBeFalse();
    }

    private static HumanQuestionPrompt Question(ExecutionIdentity identity, DateTimeOffset? deadline = null) => new(
        new QuestionId(Guid.Parse("70000000-0000-0000-0000-000000000007")),
        new AgentId(Guid.Parse("80000000-0000-0000-0000-000000000008")),
        null,
        new ToolCallId(Guid.Parse("90000000-0000-0000-0000-000000000009")),
        new BeforeRunOperationCorrelation(
            new OperationId(Guid.Parse("a0000000-0000-0000-0000-00000000000a")),
            null),
        identity,
        "Which implementation should I use?",
        [
            new HumanQuestionOption(new QuestionOptionId("small"), "Small patch", "Change the narrowest surface."),
            new HumanQuestionOption(new QuestionOptionId("safe"), "Safer patch", "Include the validation seam."),
        ],
        true,
        deadline ?? DateTimeOffset.UtcNow.AddMinutes(5));
}
