// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Correlates an operation, such as a tool call or a model request, that
/// occurred while a specific run was active.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share and compare across threads without
/// synchronization. This is the most common correlation kind: almost every
/// protected operation, security request, and audit record produced during
/// a run's normal execution carries an
/// <see cref="InRunOperationCorrelation"/> so it can be traced back to
/// exactly which run — and, when meaningful, which turn within that run —
/// caused it.
/// </remarks>
public sealed record InRunOperationCorrelation: OperationCorrelation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InRunOperationCorrelation"/>
    /// record.
    /// </summary>
    /// <param name="operationId">The stable identity of the causal operation.</param>
    /// <param name="runId">The run during which the operation occurred.</param>
    /// <param name="turnId">
    /// The turn during which the operation occurred, when the operation is
    /// scoped to a specific turn rather than the run as a whole.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="operationId"/>, <paramref name="runId"/>, or a present <paramref name="turnId"/> is default.</exception>
    public InRunOperationCorrelation(OperationId operationId, RunId runId, TurnId? turnId)
        : base(operationId)
    {
        RunId = runId;
        TurnId = turnId;
    }

    /// <summary>Gets the run during which the operation occurred.</summary>
    /// <value>The nondefault active run identity.</value>
    /// <exception cref="ArgumentOutOfRangeException">An init assignment supplies a default value.</exception>
    public RunId RunId
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfEqual(value.Value, Guid.Empty, "runId");
            field = value;
        }
    }

    /// <summary>
    /// Gets the turn during which the operation occurred, when the
    /// operation is scoped to a specific turn rather than the run as a
    /// whole.
    /// </summary>
    /// <value>The nondefault active turn identity, or <see langword="null"/> for run-scoped work.</value>
    /// <exception cref="ArgumentOutOfRangeException">An init assignment supplies a present default value.</exception>
    public TurnId? TurnId
    {
        get;
        init
        {
            if (value is { } turnId)
            {
                ArgumentOutOfRangeException.ThrowIfEqual(turnId.Value, Guid.Empty, "turnId");
            }

            field = value;
        }
    }
}
