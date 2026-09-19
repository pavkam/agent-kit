// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>A model execution that committed one successful attempt.</summary>
public sealed record ModelExecutionCompleted: ModelExecutionResult
{
    /// <summary>Initializes a successful execution.</summary>
    /// <param name="selection">The selection that produced the attempt.</param>
    /// <param name="attempts">The positive number of attempts, including the successful one.</param>
    /// <param name="result">The committed attempt.</param>
    /// <exception cref="ArgumentNullException"><paramref name="selection"/> or <paramref name="result"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="attempts"/> is less than one.</exception>
    public ModelExecutionCompleted(ModelSelectionDecision selection, int attempts, ModelAttemptCompleted result)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(attempts);
        ArgumentNullException.ThrowIfNull(result);
        Selection = selection;
        Attempts = attempts;
        Result = result;
    }

    /// <summary>Gets the selection that completed.</summary>
    public ModelSelectionDecision Selection { get; }

    /// <summary>Gets the positive attempt count.</summary>
    public int Attempts { get; }

    /// <summary>Gets the committed attempt.</summary>
    public ModelAttemptCompleted Result { get; }
}
