// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// The typed coordinates that locate one recoverable operation's durable
/// records without needing the operation's input or live run state.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// The address exists so a recovering worker can ask for evidence about work
/// it did not start and knows nothing else about. Turn identity is optional
/// because some durable work — settlement, compaction, or deferred approval
/// resolution — legitimately occurs between turns; when present it is
/// preserved unchanged rather than being invented to fill the field.
/// </para>
/// </remarks>
public sealed record DurableOperationAddress
{
    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="DurableOperationAddress"/> record.
    /// </summary>
    /// <param name="agentId">The agent that owns the operation.</param>
    /// <param name="sessionId">The session the operation belongs to.</param>
    /// <param name="runId">The run that created the operation.</param>
    /// <param name="operationId">The operation's stable identity.</param>
    /// <param name="turnId">
    /// The causal turn when the operation belongs to one, or
    /// <see langword="null"/> for work that occurs between turns.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="agentId"/>, <paramref name="sessionId"/>,
    /// <paramref name="runId"/>, or <paramref name="operationId"/> is its
    /// default, empty identity, or <paramref name="turnId"/> is supplied as a
    /// default, empty identity.
    /// </exception>
    public DurableOperationAddress(
        AgentId agentId,
        SessionId sessionId,
        RunId runId,
        OperationId operationId,
        TurnId? turnId = null)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default, nameof(agentId));
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default, nameof(sessionId));
        ArgumentOutOfRangeException.ThrowIfEqual(runId, default, nameof(runId));
        ArgumentOutOfRangeException.ThrowIfEqual(operationId, default, nameof(operationId));
        ThrowIfDefaultTurn(turnId, nameof(turnId));

        AgentId = agentId;
        SessionId = sessionId;
        RunId = runId;
        OperationId = operationId;
        TurnId = turnId;
    }

    /// <summary>
    /// Derives the one address a captured authorization can be durably
    /// addressed under, or reports that the present address shape cannot
    /// represent it.
    /// </summary>
    /// <param name="authorization">The non-null captured authorization whose scope names the operation.</param>
    /// <param name="address">
    /// The derived address when this method returns <see langword="true"/>; otherwise <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the scope carries a session and an in-run or after-run correlation;
    /// <see langword="false"/> for sessionless or before-run work.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="authorization"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Deriving the address rather than accepting one alongside an authorization removes the only way the two could
    /// disagree. A binding validates that agreement anyway, so a caller that built both by hand would simply learn
    /// about its mistake later and less clearly.
    /// </para>
    /// <para>
    /// Before-run and sessionless work is refused rather than given an invented run identity. Fabricating one would
    /// make recovery report work as belonging to a run that never contained it.
    /// </para>
    /// </remarks>
    public static bool TryCreateFrom(
        SecurityAuthorizationContext authorization,
        [NotNullWhen(true)] out DurableOperationAddress? address)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        var scope = authorization.Scope;
        address = scope switch
        {
            { SessionId: { } session, Correlation: InRunOperationCorrelation inRun } =>
                new DurableOperationAddress(scope.AgentId, session, inRun.RunId, inRun.OperationId, inRun.TurnId),
            { SessionId: { } session, Correlation: AfterRunOperationCorrelation afterRun } =>
                new DurableOperationAddress(scope.AgentId, session, afterRun.CausalRunId, afterRun.OperationId),
            _ => null,
        };
        return address is not null;
    }

    /// <summary>Gets the agent that owns the operation.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An initializer attempts to set the default, empty identity.
    /// </exception>
    public AgentId AgentId
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfEqual(value, default, nameof(AgentId));
            field = value;
        }
    }

    /// <summary>Gets the session the operation belongs to.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An initializer attempts to set the default, empty identity.
    /// </exception>
    public SessionId SessionId
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfEqual(value, default, nameof(SessionId));
            field = value;
        }
    }

    /// <summary>Gets the run retained by this durable operation address.</summary>
    /// <value>
    /// The active run only when the paired authorization correlation is in-run;
    /// for after-run follow-up it is the causal run and does not claim active
    /// ownership. The binding establishes which meaning applies.
    /// </value>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An initializer attempts to set the default, empty identity.
    /// </exception>
    public RunId RunId
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfEqual(value, default, nameof(RunId));
            field = value;
        }
    }

    /// <summary>Gets the operation's stable identity.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An initializer attempts to set the default, empty identity.
    /// </exception>
    public OperationId OperationId
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfEqual(value, default, nameof(OperationId));
            field = value;
        }
    }

    /// <summary>
    /// Gets the causal turn, or <see langword="null"/> for durable work that
    /// occurs between turns.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An initializer supplies a default, empty identity. Absent turn
    /// identity is expressed as <see langword="null"/>, never as an empty
    /// identity.
    /// </exception>
    public TurnId? TurnId
    {
        get;
        init
        {
            ThrowIfDefaultTurn(value, nameof(TurnId));
            field = value;
        }
    }

    private static void ThrowIfDefaultTurn(TurnId? turnId, string paramName)
    {
        if (turnId is { } value)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(value, default, paramName);
        }
    }
}
