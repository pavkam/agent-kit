// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Chooses one configured hook profile for a requesting scope.</summary>
/// <remarks>
/// The engine-wide default implementation supplied by <c>AgentKit.Hooks</c> resolves every request to its single
/// configured default profile; a host that registers named profiles through <c>AddHookProfile</c> replaces this
/// selector, or the default's fallback behavior, to honor <see cref="HookProfileSelectionRequest.RequestedProfile"/>.
/// </remarks>
public interface IHookProfileSelector
{
    /// <summary>Selects one profile for the requesting scope.</summary>
    /// <param name="request">The scope identities and explicit request available at this stage.</param>
    /// <param name="cancellationToken">Cancels the selection.</param>
    /// <returns>The selected profile, or an unavailable result when the request names an unknown profile.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public ValueTask<HookProfileSelectionResult> SelectAsync(
        HookProfileSelectionRequest request,
        CancellationToken cancellationToken);
}
