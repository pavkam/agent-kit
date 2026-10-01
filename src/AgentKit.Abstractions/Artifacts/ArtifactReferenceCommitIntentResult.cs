// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports an intent-store outcome together with the authoritative stored intent, when one exists.</summary>
public sealed record ArtifactReferenceCommitIntentResult
{
    /// <summary>Initializes a result.</summary>
    /// <param name="outcome">The bounded outcome.</param>
    /// <param name="intent">The authoritative stored intent; <see langword="null"/> exactly when the outcome is <see cref="ArtifactReferenceCommitIntentOutcome.NotFound"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="outcome"/> is undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="intent"/> is absent unless the outcome is not found, or present when it is.</exception>
    public ArtifactReferenceCommitIntentResult(ArtifactReferenceCommitIntentOutcome outcome, ArtifactReferenceCommitIntent? intent)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(outcome);
        var isNotFound = outcome == ArtifactReferenceCommitIntentOutcome.NotFound;
        var hasIntent = intent is not null;
        if (isNotFound == hasIntent)
        {
            throw new ArgumentException("A stored intent accompanies every outcome except not found.", nameof(intent));
        }

        Outcome = outcome;
        Intent = intent;
    }

    /// <summary>Gets the bounded outcome.</summary>
    public ArtifactReferenceCommitIntentOutcome Outcome { get; }

    /// <summary>Gets the authoritative stored intent, or <see langword="null"/> when none exists.</summary>
    public ArtifactReferenceCommitIntent? Intent { get; }
}
