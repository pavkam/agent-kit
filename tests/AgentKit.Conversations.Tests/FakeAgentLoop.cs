// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations.Tests;

/// <summary>A deterministic, configurable <see cref="IAgentLoop"/> fake used to drive <see cref="DefaultConversationSession"/> tests.</summary>
internal sealed class FakeAgentLoop: IAgentLoop
{
    /// <summary>Gets the number of times <see cref="RunAsync"/> was called.</summary>
    public int CallCount { get; private set; }

    /// <summary>Gets the most recent request observed.</summary>
    public AgentRunRequest? LastRequest { get; private set; }

    /// <summary>Gets or sets the result factory used to build the run result; a trivial idle-turn result by default.</summary>
    public Func<AgentRunRequest, AgentLoopResult>? ResultFactory { get; set; }

    /// <summary>Gets or sets a gate <see cref="RunAsync"/> awaits before completing, for serialization tests.</summary>
    public TaskCompletionSource? Gate { get; set; }

    /// <summary>Gets or sets a signal set the moment <see cref="RunAsync"/> is entered, before it awaits <see cref="Gate"/>.</summary>
    public TaskCompletionSource? EnteredSignal { get; set; }

    /// <summary>Gets or sets provisional progress delivered before the completion gate is released.</summary>
    public AgentRunEvent? ProgressEvent { get; set; }

    public async Task<AgentLoopResult> RunAsync(AgentRunRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        CallCount++;
        LastRequest = request;
        cancellationToken.ThrowIfCancellationRequested();
        _ = EnteredSignal?.TrySetResult();

        if (ProgressEvent is not null && request.Observer is not null)
        {
            await request.Observer.OnEventAsync(ProgressEvent, cancellationToken).ConfigureAwait(false);
        }

        if (Gate is not null)
        {
            await Gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();

        return ResultFactory is not null
            ? ResultFactory(request)
            : DefaultResult(request);
    }

    private static AgentLoopResult DefaultResult(AgentRunRequest request) =>
        new(
            request.AgentId,
            request.SessionId,
            request.BranchId,
            request.RunId,
            new AgentRunCompleted(FakeMessages.Assistant(request, "ok")),
            [],
            new SessionVersion(1));
}
