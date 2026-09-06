// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>A checkpoint-production attempt that failed unexpectedly.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// </remarks>
public sealed record CompactionStrategyFailed: CompactionStrategyResult
{
    /// <summary>Initializes a new instance of the <see cref="CompactionStrategyFailed"/> record.</summary>
    /// <param name="failure">The unexpected failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public CompactionStrategyFailed(CompactionFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        Failure = failure;
    }

    /// <summary>Gets the unexpected failure.</summary>
    public CompactionFailure Failure { get; init; }
}
