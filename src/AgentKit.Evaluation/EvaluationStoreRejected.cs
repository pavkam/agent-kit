// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Reports that a store refused to record a result.</summary>
public sealed record EvaluationStoreRejected: EvaluationStoreResult
{
    /// <summary>Initializes the rejection.</summary>
    /// <param name="failure">The typed failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is <see langword="null"/>.</exception>
    public EvaluationStoreRejected(EvaluationStoreFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        Failure = failure;
    }

    /// <summary>Gets the typed failure.</summary>
    public EvaluationStoreFailure Failure { get; }
}
