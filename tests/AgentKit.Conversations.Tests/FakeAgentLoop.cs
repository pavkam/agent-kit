// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations.Tests;

/// <summary>A deterministic, configurable <see cref="IAgentLoop"/> fake used to drive <see cref="DefaultConversationSession"/> tests.</summary>
/// <remarks>
/// Also implements <see cref="IAsyncDisposable"/> (but deliberately not <see cref="IDisposable"/>) so a test can
/// register this fake as a scoped keyed service and observe how the disposing scope handles an
/// IAsyncDisposable-only occupant.
/// </remarks>
internal sealed class FakeAgentLoop: IAgentLoop, IAsyncDisposable
{
    /// <summary>Gets the number of times <see cref="RunAsync"/> was called.</summary>
    public int CallCount { get; private set; }

    /// <summary>Gets the number of times <see cref="DisposeAsync"/> was called.</summary>
    public int DisposeAsyncCallCount { get; private set; }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        DisposeAsyncCallCount++;
        return ValueTask.CompletedTask;
    }

    /// <summary>Gets the most recent request observed.</summary>
    public AgentLoopRunRequest? LastRequest { get; private set; }

    /// <summary>Gets the collaborator bundle supplied with the most recent call.</summary>
    public AgentRunServices? LastServices { get; private set; }

    /// <summary>Gets or sets the result factory used to build the run result; a trivial idle-turn result by default.</summary>
    public Func<AgentLoopRunRequest, AgentLoopResult>? ResultFactory { get; set; }

    /// <summary>Gets or sets a gate <see cref="RunAsync"/> awaits before completing, for serialization tests.</summary>
    public TaskCompletionSource? Gate { get; set; }

    /// <summary>Gets or sets a signal set the moment <see cref="RunAsync"/> is entered, before it awaits <see cref="Gate"/>.</summary>
    public TaskCompletionSource? EnteredSignal { get; set; }

    /// <summary>Gets or sets provisional progress delivered before the completion gate is released.</summary>
    public AgentRunEvent? ProgressEvent { get; set; }

    public async Task<AgentLoopResult> RunAsync(
        AgentLoopRunRequest request,
        AgentRunServices services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(services);
        CallCount++;
        LastRequest = request;
        LastServices = services;
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

    private static AgentLoopResult DefaultResult(AgentLoopRunRequest request) =>
        new(
            request.AgentId,
            request.SessionId,
            request.BranchId,
            request.RunId,
            new RunSucceeded(),
            [],
            new SessionVersion(1),
            null,
            new RunUsage(request.RunId, []),
            new RunSettlementCompleted());
}
