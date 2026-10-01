// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Activates the exact keyed collaborators one memory profile version names for one operation.</summary>
/// <remarks>
/// The selector is the single engine-wide routing boundary. It validates the requested key and version, activates a keyed
/// scope, captures one immutable model catalog snapshot, and returns an owned lease. It never exposes
/// <see cref="IServiceProvider"/> and never falls back to another profile or version.
/// </remarks>
public interface IMemoryProfileRuntimeSelector
{
    /// <summary>Activates the runtime for the profile key and version in an operation context.</summary>
    /// <param name="context">The operation context naming the exact profile key and version.</param>
    /// <param name="cancellationToken">Cancels activation.</param>
    /// <returns>An owned lease the caller must dispose, or a typed unavailable result returned before any I/O.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<MemoryProfileRuntimeSelectionResult> SelectAsync(MemoryOperationContext context, CancellationToken cancellationToken = default);
}
