// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Same-model retries are exhausted for a failure the fallback policy may
/// send to another candidate.
/// </summary>
/// <remarks>
/// This is not a terminal turn failure. The loop decides whether another
/// selected candidate exists. The executor does not select that candidate.
/// </remarks>
public sealed record ModelFallbackRequired: ModelExecutionResult
{
    /// <summary>Initializes a fallback signal.</summary>
    /// <param name="selection">The selection whose attempts were exhausted.</param>
    /// <param name="attempts">The positive number of attempts already made.</param>
    /// <param name="failure">The last normalized failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="selection"/> or <paramref name="failure"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="attempts"/> is less than one.</exception>
    public ModelFallbackRequired(ModelSelectionDecision selection, int attempts, ProviderFailure failure)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(attempts);
        ArgumentNullException.ThrowIfNull(failure);
        Selection = selection;
        Attempts = attempts;
        Failure = failure;
    }

    /// <summary>Gets the exhausted selection.</summary>
    public ModelSelectionDecision Selection { get; }

    /// <summary>Gets the positive attempt count.</summary>
    public int Attempts { get; }

    /// <summary>Gets the last failure.</summary>
    public ProviderFailure Failure { get; }
}
