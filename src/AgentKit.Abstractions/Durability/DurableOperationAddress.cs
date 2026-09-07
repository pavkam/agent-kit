// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

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
    private readonly AgentId _agentId;
    private readonly SessionId _sessionId;
    private readonly RunId _runId;
    private readonly OperationId _operationId;

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

        _agentId = agentId;
        _sessionId = sessionId;
        _runId = runId;
        _operationId = operationId;
        TurnId = turnId;
    }

    /// <summary>Gets the agent that owns the operation.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An initializer attempts to set the default, empty identity.
    /// </exception>
    public AgentId AgentId
    {
        get => _agentId;
        init
        {
            ArgumentOutOfRangeException.ThrowIfEqual(value, default, nameof(AgentId));
            _agentId = value;
        }
    }

    /// <summary>Gets the session the operation belongs to.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An initializer attempts to set the default, empty identity.
    /// </exception>
    public SessionId SessionId
    {
        get => _sessionId;
        init
        {
            ArgumentOutOfRangeException.ThrowIfEqual(value, default, nameof(SessionId));
            _sessionId = value;
        }
    }

    /// <summary>Gets the run that created the operation.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An initializer attempts to set the default, empty identity.
    /// </exception>
    public RunId RunId
    {
        get => _runId;
        init
        {
            ArgumentOutOfRangeException.ThrowIfEqual(value, default, nameof(RunId));
            _runId = value;
        }
    }

    /// <summary>Gets the operation's stable identity.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An initializer attempts to set the default, empty identity.
    /// </exception>
    public OperationId OperationId
    {
        get => _operationId;
        init
        {
            ArgumentOutOfRangeException.ThrowIfEqual(value, default, nameof(OperationId));
            _operationId = value;
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
