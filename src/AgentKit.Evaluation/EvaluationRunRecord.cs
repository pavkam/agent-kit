// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Preserves the typed identities and bounded terminal state of the agent run a case repetition produced.</summary>
public sealed record EvaluationRunRecord
{
    /// <summary>Initializes a validated record.</summary>
    /// <param name="runId">The agent run identity.</param>
    /// <param name="sessionId">The case session identity.</param>
    /// <param name="outcome">The non-blank bounded outcome name, such as <c>succeeded</c> or <c>policy_halted</c>.</param>
    /// <param name="settlement">The non-blank bounded settlement name, such as <c>completed</c> or <c>recovery_required</c>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="runId"/> or <paramref name="sessionId"/> is default.</exception>
    /// <exception cref="ArgumentException"><paramref name="outcome"/> or <paramref name="settlement"/> is blank.</exception>
    public EvaluationRunRecord(RunId runId, SessionId sessionId, string outcome, string settlement)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(runId, default, nameof(runId));
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default, nameof(sessionId));
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);
        ArgumentException.ThrowIfNullOrWhiteSpace(settlement);
        RunId = runId;
        SessionId = sessionId;
        Outcome = outcome;
        Settlement = settlement;
    }

    /// <summary>Gets the agent run identity.</summary>
    public RunId RunId { get; }

    /// <summary>Gets the case session identity.</summary>
    public SessionId SessionId { get; }

    /// <summary>Gets the bounded outcome name.</summary>
    public string Outcome { get; }

    /// <summary>Gets the bounded settlement name.</summary>
    public string Settlement { get; }
}
