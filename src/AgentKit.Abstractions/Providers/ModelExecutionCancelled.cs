// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>A model execution that was canceled and must not be retried.</summary>
public sealed record ModelExecutionCancelled: ModelExecutionResult
{
    /// <summary>Initializes a canceled execution.</summary>
    /// <param name="selection">The selection that was canceled.</param>
    /// <param name="attempts">The positive number of attempts already started.</param>
    /// <param name="cancellation">The normalized cancellation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="selection"/> or <paramref name="cancellation"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="attempts"/> is less than one.</exception>
    public ModelExecutionCancelled(ModelSelectionDecision selection, int attempts, ProviderFailure cancellation)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(attempts);
        ArgumentNullException.ThrowIfNull(cancellation);
        Selection = selection;
        Attempts = attempts;
        Cancellation = cancellation;
    }

    /// <summary>Gets the canceled selection.</summary>
    public ModelSelectionDecision Selection { get; }

    /// <summary>Gets the positive attempt count.</summary>
    public int Attempts { get; }

    /// <summary>Gets the normalized cancellation.</summary>
    public ProviderFailure Cancellation { get; }
}
