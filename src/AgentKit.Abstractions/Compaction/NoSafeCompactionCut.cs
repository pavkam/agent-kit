// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>A cut-selection attempt that deliberately found no safe cut.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// </remarks>
public sealed record NoSafeCompactionCut: CompactionCutSelectionResult
{
    /// <summary>Initializes a new instance of the <see cref="NoSafeCompactionCut"/> record.</summary>
    /// <param name="rejection">Why no safe cut was found.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rejection"/> is null.</exception>
    public NoSafeCompactionCut(CompactionRejection rejection)
    {
        ArgumentNullException.ThrowIfNull(rejection);
        Rejection = rejection;
    }

    /// <summary>Gets why no safe cut was found.</summary>
    public CompactionRejection Rejection { get; init; }
}
