// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>A checkpoint-production attempt that deliberately declined to produce a candidate.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// </remarks>
public sealed record CompactionStrategyUnsupported: CompactionStrategyResult
{
    /// <summary>Initializes a new instance of the <see cref="CompactionStrategyUnsupported"/> record.</summary>
    /// <param name="rejection">Why the strategy declined to produce a candidate.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rejection"/> is null.</exception>
    public CompactionStrategyUnsupported(CompactionRejection rejection)
    {
        ArgumentNullException.ThrowIfNull(rejection);
        Rejection = rejection;
    }

    /// <summary>Gets why the strategy declined to produce a candidate.</summary>
    public CompactionRejection Rejection { get; init; }
}
