// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations;

/// <summary>Admits and runs one conversational turn through the engine facade.</summary>
public interface IConversationTurnExecutor
{
    /// <summary>Creates a session when none is bound yet.</summary>
    /// <param name="agentId">The composed agent identity.</param>
    /// <param name="identity">The authenticated caller.</param>
    /// <param name="existingSessionId">The already-bound session, or null to create one.</param>
    /// <param name="cancellationToken">Cancels creation.</param>
    /// <returns>The session to use for subsequent turns.</returns>
    public Task<SessionId> EnsureSessionAsync(
        AgentId agentId,
        ExecutionIdentity identity,
        SessionId? existingSessionId,
        CancellationToken cancellationToken = default);

    /// <summary>Runs one turn and returns either a finished envelope or a typed rejection.</summary>
    /// <typeparam name="TOutput">The requested output projection.</typeparam>
    /// <param name="request">The turn to run.</param>
    /// <param name="cancellationToken">Cancels the caller's wait.</param>
    /// <returns>The engine's terminal typed result.</returns>
    public Task<AgentRunResult<TOutput>> RunAsync<TOutput>(
        ConversationTurnRunRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Runs one observed turn through the engine's <see cref="Agent.SendAsync"/> surface.</summary>
    /// <param name="request">The turn to run.</param>
    /// <param name="cancellationToken">Cancels the caller's wait.</param>
    /// <returns>The loop's terminal result after settlement.</returns>
    public Task<AgentLoopResult> SendObservedAsync(
        ConversationTurnRunRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Immutable evidence for one turn executed through the engine.</summary>
/// <param name="AgentId">The composed agent identity.</param>
/// <param name="Identity">The authenticated caller.</param>
/// <param name="SessionId">The session to continue, or null to create one on this turn.</param>
/// <param name="UserText">The plain-text user message.</param>
/// <param name="MaxTurns">The narrowed turn limit for this run.</param>
/// <param name="AttemptTimeout">The narrowed attempt timeout for this run.</param>
/// <param name="Observer">The optional live observer wired into engine admission.</param>
public sealed record ConversationTurnRunRequest(
    AgentId AgentId,
    ExecutionIdentity Identity,
    SessionId? SessionId,
    string UserText,
    int MaxTurns,
    TimeSpan AttemptTimeout,
    IAgentRunObserver? Observer);
