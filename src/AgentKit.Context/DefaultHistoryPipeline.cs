// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context;

/// <summary>
/// The default <see cref="IHistoryPipeline"/>: repairs loaded history to complete messages only, validates
/// structural role/part rules and tool-call causality, and returns a bounded <see cref="HistoryView"/>.
/// </summary>
internal sealed class DefaultHistoryPipeline: IHistoryPipeline
{
    /// <inheritdoc/>
    public Task<HistoryPreparationResult> PrepareAsync(
        HistoryPreparationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var repairedMessages = RepairHistory(
            request.Messages,
            out var repairs,
            out _,
            out _);

        if (repairedMessages.IsEmpty)
        {
            return Task.FromResult<HistoryPreparationResult>(new RejectedHistory(new HistoryFailure(
                HistoryFailureKind.EmptyHistory,
                "The eligible conversation history contains no complete messages to send.",
                ExtensionData.Empty)));
        }

        var structuralFailure = HistoryMessageValidation.ValidateRolePartCombinations(repairedMessages)
            ?? HistoryMessageValidation.ValidateToolCallCausality(repairedMessages);
        if (structuralFailure is not null)
        {
            return Task.FromResult<HistoryPreparationResult>(new RejectedHistory(structuralFailure));
        }

        var alignedMessages = HistoryMessageCoordinateAlignment.Align(repairedMessages, request.SourceCursor);
        var view = new HistoryView(request.SourceCursor, alignedMessages, repairs);
        return Task.FromResult<HistoryPreparationResult>(new PreparedHistory(view));
    }

    /// <summary>
    /// Retains only complete messages and excludes any system or developer message found in history.
    /// </summary>
    private static ImmutableArray<AgentMessage> RepairHistory(
        ImmutableArray<AgentMessage> history,
        out ImmutableArray<HistoryRepair> repairs,
        out int excludedIncompleteMessages,
        out int excludedInstructionMessages)
    {
        Debug.Assert(!history.IsDefault, "The request contract guarantees an initialized history.");
        excludedIncompleteMessages = 0;
        excludedInstructionMessages = 0;
        var builder = ImmutableArray.CreateBuilder<AgentMessage>(history.Length);
        var repairBuilder = ImmutableArray.CreateBuilder<HistoryRepair>();
        foreach (var message in history)
        {
            if (message.State != MessageState.Complete)
            {
                excludedIncompleteMessages++;
                repairBuilder.Add(new HistoryRepair(
                    [message.Id],
                    message is AssistantMessage
                        ? HistoryRepairKind.ExcludedIncompleteAssistantContent
                        : HistoryRepairKind.ExcludedIncompleteMessage,
                    $"Excluded a message whose state is {message.State} because only complete messages are sent to a provider.",
                    ExtensionData.Empty));
                continue;
            }

            if (message is SystemMessage or DeveloperMessage)
            {
                excludedInstructionMessages++;
                repairBuilder.Add(new HistoryRepair(
                    [message.Id],
                    HistoryRepairKind.ExcludedInstructionMessage,
                    "Excluded a system or developer message found in history because history never carries instruction authority.",
                    ExtensionData.Empty));
                continue;
            }

            builder.Add(message);
        }

        repairs = repairBuilder.ToImmutable();
        return builder.ToImmutable();
    }
}
