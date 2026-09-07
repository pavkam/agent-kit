// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context;

/// <summary>
/// The default <see cref="IContextAssembler"/>: repairs already-loaded
/// history to complete messages only, validates that every tool call and
/// tool result in the repaired history causally match, and combines
/// instructions, history, tools, and settings into one
/// <see cref="ChatRequestContext"/>.
/// </summary>
/// <remarks>
/// <para>
/// This implementation performs no I/O, retrieval, or compaction of its
/// own; it operates entirely over the <see cref="ContextAssemblyRequest.History"/>
/// and <see cref="ContextAssemblyRequest.Instructions"/> the caller
/// supplies. See <see cref="IContextAssembler"/> for the reduced-scope
/// rationale shared by every implementation of this contract.
/// </para>
/// <para>
/// History repair keeps only messages whose <see cref="AgentMessage.State"/>
/// is <see cref="MessageState.Complete"/>: an incomplete, suspended, or
/// interrupted message is never sent to a provider as conversational
/// input. Tool-causality validation then requires the surviving history to
/// carry exactly one <see cref="ToolResultPart"/> for every
/// <see cref="ToolCallPart"/> it contains, and no
/// <see cref="ToolResultPart"/> that references a call absent from that
/// same history; either violation fails assembly with
/// <see cref="ContextPreparationFailureKind.BrokenToolCallCausality"/>
/// rather than sending a provider a request it cannot interpret.
/// </para>
/// </remarks>
public sealed class DefaultContextAssembler: IContextAssembler
{
    /// <inheritdoc/>
    public Task<ContextAssemblyResult> AssembleAsync(
        ContextAssemblyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var repairedHistory = RepairHistory(request.History);

        if (repairedHistory.IsEmpty)
        {
            return Task.FromResult<ContextAssemblyResult>(
                new ContextPreparationFailed(
                    new ContextPreparationFailure(
                        ContextPreparationFailureKind.EmptyHistory,
                        "The eligible conversation history contains no complete messages to send.",
                        ExtensionData.Empty)));
        }

        var causalityFailure = ValidateToolCallCausality(repairedHistory);
        if (causalityFailure is not null)
        {
            return Task.FromResult<ContextAssemblyResult>(new ContextPreparationFailed(causalityFailure));
        }

        var messages = request.Instructions.AddRange(repairedHistory);

        var context = new ChatRequestContext(
            request.ModelRequestId,
            request.Model,
            messages,
            request.Tools,
            request.ToolChoice,
            request.Settings,
            request.Extensions);

        return Task.FromResult<ContextAssemblyResult>(new ContextReady(context));
    }

    private static ImmutableArray<AgentMessage> RepairHistory(ImmutableArray<AgentMessage> history)
    {
        var builder = ImmutableArray.CreateBuilder<AgentMessage>(history.Length);
        foreach (var message in history)
        {
            if (message.State == MessageState.Complete)
            {
                builder.Add(message);
            }
        }

        return builder.ToImmutable();
    }

    private static ContextPreparationFailure? ValidateToolCallCausality(ImmutableArray<AgentMessage> repairedHistory)
    {
        var callIds = new HashSet<ToolCallId>();
        var resultIds = new HashSet<ToolCallId>();

        foreach (var message in repairedHistory)
        {
            foreach (var part in message.Parts)
            {
                switch (part)
                {
                    case ToolCallPart toolCall:
                        _ = callIds.Add(toolCall.CallId);
                        break;
                    case ToolResultPart toolResult:
                        _ = resultIds.Add(toolResult.CallId);
                        break;
                    default:
                        break;
                }
            }
        }

        return callIds.SetEquals(resultIds)
            ? null
            : new ContextPreparationFailure(
                ContextPreparationFailureKind.BrokenToolCallCausality,
                "Every tool call in history must have exactly one matching terminal result, and every " +
                    "result must reference a call present in the same history.",
                ExtensionData.Empty);
    }
}
