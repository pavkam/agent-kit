// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network;

public sealed partial class DefaultNetworkNameResolver
{
    /// <inheritdoc/>
    public ValueTask<NetworkResolutionResult> ResolveAsync(
        NetworkResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return NetworkObservability.ObserveAsync(
            _logger,
            AgentKitActivityNames.NetworkResolve,
            "resolve",
            request.Id,
            token => ResolveCoreAsync(request, token),
            static result => result is NetworkResolved,
            cancellationToken);
    }
}
