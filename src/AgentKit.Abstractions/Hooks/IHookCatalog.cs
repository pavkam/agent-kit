// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures one immutable <see cref="HookCatalogSnapshot"/> for a profile by discovering and merging every registered <see cref="IHookRegistrationSource"/>.</summary>
/// <remarks>
/// The captured snapshot is immutable for its documented engine, agent, run, turn, or operation scope; a new
/// capture is produced only when the caller requests one again, typically at a declared
/// <see cref="HookReloadBoundary"/>. Capturing never changes the sequence a dispatch already started with.
/// </remarks>
public interface IHookCatalog
{
    /// <summary>Captures one immutable snapshot for the requested profile.</summary>
    /// <param name="request">The profile to capture.</param>
    /// <param name="cancellationToken">Cancels the capture.</param>
    /// <returns>The merged, deterministic capture for the requested profile.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public ValueTask<HookCatalogSnapshot> CaptureAsync(
        HookCatalogRequest request,
        CancellationToken cancellationToken);
}
