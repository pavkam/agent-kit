// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>A model execution that failed and must not fall back.</summary>
public sealed record ModelExecutionFailed: ModelExecutionResult
{
    /// <summary>Initializes a terminal failure.</summary>
    /// <param name="selection">The selection that failed.</param>
    /// <param name="attempts">The positive number of attempts already made.</param>
    /// <param name="failure">The normalized failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="selection"/> or <paramref name="failure"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="attempts"/> is less than one.</exception>
    public ModelExecutionFailed(ModelSelectionDecision selection, int attempts, ProviderFailure failure)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(attempts);
        ArgumentNullException.ThrowIfNull(failure);
        Selection = selection;
        Attempts = attempts;
        Failure = failure;
    }

    /// <summary>Gets the failed selection.</summary>
    public ModelSelectionDecision Selection { get; }

    /// <summary>Gets the positive attempt count.</summary>
    public int Attempts { get; }

    /// <summary>Gets the normalized failure.</summary>
    public ProviderFailure Failure { get; }
}
