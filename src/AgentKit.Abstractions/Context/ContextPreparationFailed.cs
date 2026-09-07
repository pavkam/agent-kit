// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Context assembly failed before any provider I/O was attempted.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// </remarks>
public sealed record ContextPreparationFailed: ContextAssemblyResult
{
    /// <summary>Initializes a new instance of the <see cref="ContextPreparationFailed"/> record.</summary>
    /// <param name="failure">Why context assembly failed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public ContextPreparationFailed(ContextPreparationFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        Failure = failure;
    }

    /// <summary>Gets why context assembly failed.</summary>
    public ContextPreparationFailure Failure { get; init; }
}
