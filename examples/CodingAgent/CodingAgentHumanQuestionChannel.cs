// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent;

/// <summary>Authenticates one terminal selection as the local human after the broker authorizes publication.</summary>
internal sealed class CodingAgentHumanQuestionChannel(
    IHumanQuestionPrompt prompt,
    ExecutionIdentity identity,
    TimeProvider timeProvider): IHumanQuestionChannel
{
    /// <inheritdoc/>
    public async ValueTask<HumanQuestionResult> AskAsync(
        HumanQuestionPrompt question,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(question);
        cancellationToken.ThrowIfCancellationRequested();
        if (question.Identity != identity)
        {
            return new HumanQuestionUnavailable(question.Id, "The question does not belong to the authenticated local terminal identity.");
        }

        var remaining = question.Deadline - timeProvider.GetUtcNow();
        if (remaining <= TimeSpan.Zero)
        {
            return new HumanQuestionTimedOut(question.Id);
        }

        using var deadline = new CancellationTokenSource(remaining, timeProvider);
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        HumanQuestionSelection selection;
        try
        {
            selection = await prompt.AskAsync(question, wait.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            return new HumanQuestionTimedOut(question.Id);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return new HumanQuestionUnavailable(question.Id, "The interactive question channel failed.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (timeProvider.GetUtcNow() >= question.Deadline)
        {
            return new HumanQuestionTimedOut(question.Id);
        }

        if (!question.Options.Any(option => option.Id == selection.OptionId))
        {
            return new HumanQuestionUnavailable(question.Id, "The terminal returned an option that was not presented.");
        }
        if (!question.AllowsFreeText && !string.IsNullOrWhiteSpace(selection.FreeText))
        {
            return new HumanQuestionUnavailable(question.Id, "The terminal returned free text for a closed-choice question.");
        }

        var answer = new HumanQuestionAnswer(
            selection.OptionId,
            string.IsNullOrWhiteSpace(selection.FreeText) ? null : selection.FreeText,
            identity,
            timeProvider.GetUtcNow());
        return new HumanQuestionAnswered(question.Id, answer);
    }
}
