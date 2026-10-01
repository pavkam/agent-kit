// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Acknowledges that a result is recorded, either freshly or as an idempotent replay.</summary>
public sealed record EvaluationStoreAppended: EvaluationStoreResult
{
    /// <summary>Initializes the acknowledgement.</summary>
    /// <param name="receipt">The identity of the recorded result.</param>
    /// <param name="replayed"><see langword="true"/> when an identical result was already recorded and nothing was written.</param>
    /// <exception cref="ArgumentNullException"><paramref name="receipt"/> is <see langword="null"/>.</exception>
    public EvaluationStoreAppended(EvaluationResultReceipt receipt, bool replayed)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        Receipt = receipt;
        Replayed = replayed;
    }

    /// <summary>Gets the identity of the recorded result.</summary>
    public EvaluationResultReceipt Receipt { get; }

    /// <summary>Gets whether an identical result was already recorded.</summary>
    public bool Replayed { get; }
}
