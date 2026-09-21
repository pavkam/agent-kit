// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Event arguments for <see cref="AgentHookPoints.ContextAssembled"/>: the provider-ready context one turn
/// assembled before model dispatch. Read-only; hooks may observe but not mutate the assembled request.
/// </summary>
public sealed class ContextAssembledEventArgs: AgentHookEventArgs, IAgentScopedHookStage
{
    /// <summary>Initializes the arguments.</summary>
    /// <param name="dispatch">The point identity, dispatch identity, causality, and timing facts for this dispatch.</param>
    /// <param name="agentId">The agent being run.</param>
    /// <param name="sessionId">The session the run appends to.</param>
    /// <param name="turn">The one-based turn number within the run.</param>
    /// <param name="context">The assembled request context.</param>
    /// <param name="repairs">History repairs applied while preparing the conversational messages.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dispatch"/> or <paramref name="context"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="turn"/> is not positive.</exception>
    /// <exception cref="ArgumentException"><paramref name="dispatch"/>'s correlation names no turn, or <paramref name="repairs"/> is uninitialized.</exception>
    public ContextAssembledEventArgs(
        HookDispatchMetadata dispatch,
        AgentId agentId,
        SessionId sessionId,
        int turn,
        LlmRequestContext context,
        ImmutableArray<HistoryRepair> repairs)
        : base(dispatch)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(turn);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfContainsNull(repairs);
        ArgumentException.ThrowIfNotEqual(Correlation is InRunOperationCorrelation { TurnId: not null }, true, nameof(dispatch));
        AgentId = agentId;
        SessionId = sessionId;
        Turn = turn;
        Context = context;
        Repairs = repairs;
    }

    /// <inheritdoc/>
    public AgentId AgentId { get; }

    /// <inheritdoc/>
    public SessionId? SessionId { get; }

    /// <summary>Gets the run identity.</summary>
    public RunId RunId => ((InRunOperationCorrelation) Correlation).RunId;

    /// <summary>Gets the turn identity.</summary>
    public TurnId TurnId => ((InRunOperationCorrelation) Correlation).TurnId!.Value;

    /// <summary>Gets the one-based turn number within the run.</summary>
    public int Turn { get; }

    /// <summary>Gets the assembled request context; read-only.</summary>
    public LlmRequestContext Context { get; }

    /// <summary>Gets history repairs applied while preparing conversational messages.</summary>
    public ImmutableArray<HistoryRepair> Repairs { get; }
}
