// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>A compaction attempt that failed unexpectedly.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// </remarks>
public sealed record CompactionFailed: CompactionResult
{
    /// <summary>Initializes a new instance of the <see cref="CompactionFailed"/> record.</summary>
    /// <param name="context">The operation context this outcome resulted from.</param>
    /// <param name="failure">The unexpected failure.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/> or <paramref name="failure"/> is null.
    /// </exception>
    public CompactionFailed(CompactionOperationContext context, CompactionFailure failure)
        : base(context)
    {
        ArgumentNullException.ThrowIfNull(failure);
        Failure = failure;
    }

    /// <summary>Gets the unexpected failure.</summary>
    public CompactionFailure Failure { get; init; }
}
