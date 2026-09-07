// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A complete terminal result already exists, so recovery publishes it
/// without reinvoking the effect.
/// </summary>
/// <remarks>
/// <para>
/// This is the decision for the
/// <see cref="DurableOperationState.OutcomeReady"/> case: the work finished,
/// but the process died before its result was committed to history or
/// released in source order.
/// </para>
/// <para>
/// Reinvocation here would duplicate a completed effect, so an implementation
/// acting on this decision must commit idempotently and must never call the
/// operation again.
/// </para>
/// </remarks>
public sealed record RecoveryCommitRecordedResult: RecoveryDecision
{
    private readonly DurableOperationResult _result;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="RecoveryCommitRecordedResult"/> record.
    /// </summary>
    /// <param name="result">The recorded terminal result to publish.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="result"/> is <see langword="null"/>.
    /// </exception>
    public RecoveryCommitRecordedResult(DurableOperationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        _result = result;
    }

    /// <summary>Gets the recorded terminal result to publish.</summary>
    /// <exception cref="ArgumentNullException">
    /// An initializer attempts to set <see langword="null"/>.
    /// </exception>
    public DurableOperationResult Result
    {
        get => _result;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Result));
            _result = value;
        }
    }
}
