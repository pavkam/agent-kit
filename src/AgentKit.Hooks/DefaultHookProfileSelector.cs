// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks;

/// <summary>Resolves every hook profile request to the single configured default profile.</summary>
/// <remarks>
/// Named profiles and explicit replacement selectors arrive in WS2-C10. Until then, only
/// <see cref="HookProfileOptions.DefaultProfileKey"/> is supported.
/// </remarks>
public sealed class DefaultHookProfileSelector: IHookProfileSelector
{
    /// <inheritdoc/>
    public ValueTask<HookProfileSelectionResult> SelectAsync(
        HookProfileSelectionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return request.RequestedProfile is { } requested && !requested.Equals(HookProfileOptions.DefaultProfileKey)
            ? new ValueTask<HookProfileSelectionResult>(new HookProfileUnavailable(requested))
            : new ValueTask<HookProfileSelectionResult>(new HookProfileSelected(HookProfileOptions.DefaultProfileKey));
    }
}
