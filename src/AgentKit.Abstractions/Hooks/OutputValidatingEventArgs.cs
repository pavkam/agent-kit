// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Event arguments for <see cref="AgentHookPoints.OutputValidating"/>: a provisional validated candidate before the
/// processor accepts it.
/// </summary>
/// <remarks>
/// Hooks may set <see cref="Reject"/> to veto acceptance with a safe message. They cannot widen the declared schema,
/// grant authority, or mutate provider evidence. The processor revalidates schema and semantic validators after hooks
/// run; a hook veto is an additional rejection, not a substitute for structural validation.
/// </remarks>
public sealed class OutputValidatingEventArgs: AgentHookEventArgs, IAgentScopedHookStage
{
    /// <summary>Initializes the arguments.</summary>
    /// <param name="dispatch">The point identity, dispatch identity, causality, and timing facts for this dispatch.</param>
    /// <param name="agentId">The agent being run.</param>
    /// <param name="sessionId">The session the run appends to.</param>
    /// <param name="turn">The one-based turn number within the run.</param>
    /// <param name="definition">The output definition being validated.</param>
    /// <param name="candidate">The provisional candidate that passed local schema validation.</param>
    /// <param name="validationAttempt">The one-based validation attempt number.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dispatch"/>, <paramref name="definition"/>, or <paramref name="candidate"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="turn"/> or <paramref name="validationAttempt"/> is not positive.</exception>
    /// <exception cref="ArgumentException"><paramref name="dispatch"/>'s correlation names no turn.</exception>
    public OutputValidatingEventArgs(
        HookDispatchMetadata dispatch,
        AgentId agentId,
        SessionId sessionId,
        int turn,
        OutputDefinition definition,
        ValidatedOutput candidate,
        int validationAttempt)
        : base(dispatch)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(turn);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentOutOfRangeException.ThrowIfLessThan(validationAttempt, 1);
        ArgumentException.ThrowIfNotEqual(Correlation is InRunOperationCorrelation { TurnId: not null }, true, nameof(dispatch));

        AgentId = agentId;
        SessionId = sessionId;
        Turn = turn;
        Definition = definition;
        Candidate = candidate;
        ValidationAttempt = validationAttempt;
    }

    /// <inheritdoc/>
    public AgentId AgentId { get; }

    /// <inheritdoc/>
    public SessionId? SessionId { get; }

    /// <summary>Gets the run identity.</summary>
    public RunId RunId => ((InRunOperationCorrelation) Correlation).RunId;

    /// <summary>Gets the one-based turn number within the run.</summary>
    public int Turn { get; }

    /// <summary>Gets the output definition being validated.</summary>
    public OutputDefinition Definition { get; }

    /// <summary>Gets the provisional candidate under review.</summary>
    public ValidatedOutput Candidate { get; }

    /// <summary>Gets the one-based validation attempt number.</summary>
    public int ValidationAttempt { get; }

    /// <summary>Gets or sets whether the hook vetoes acceptance of <see cref="Candidate"/>.</summary>
    public bool Reject { get; set; }

    /// <summary>Gets or sets the safe rejection message when <see cref="Reject"/> is true.</summary>
    public string? RejectMessage { get; set; }
}
