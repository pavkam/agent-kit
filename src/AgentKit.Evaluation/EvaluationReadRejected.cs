// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Reports that a store could not serve a read.</summary>
public sealed record EvaluationReadRejected: EvaluationReadResult
{
    /// <summary>Initializes the rejection.</summary>
    /// <param name="failure">The typed failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is <see langword="null"/>.</exception>
    public EvaluationReadRejected(EvaluationStoreFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        Failure = failure;
    }

    /// <summary>Gets the typed failure.</summary>
    public EvaluationStoreFailure Failure { get; }
}
