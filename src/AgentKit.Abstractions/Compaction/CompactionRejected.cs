// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>A compaction attempt that was deliberately rejected before activation.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// </remarks>
public sealed record CompactionRejected: CompactionResult
{
    /// <summary>Initializes a new instance of the <see cref="CompactionRejected"/> record.</summary>
    /// <param name="context">The operation context this outcome resulted from.</param>
    /// <param name="rejection">Why this attempt was rejected.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/> or <paramref name="rejection"/> is null.
    /// </exception>
    public CompactionRejected(CompactionOperationContext context, CompactionRejection rejection)
        : base(context)
    {
        ArgumentNullException.ThrowIfNull(rejection);
        Rejection = rejection;
    }

    /// <summary>Gets why this attempt was rejected.</summary>
    public CompactionRejection Rejection { get; init; }
}
