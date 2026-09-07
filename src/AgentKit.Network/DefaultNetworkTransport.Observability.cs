// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network;

public sealed partial class DefaultNetworkTransport
{
    /// <inheritdoc/>
    public ValueTask<NetworkSendResult> SendAsync(
        NetworkRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return NetworkObservability.ObserveAsync(
            _logger,
            AgentKitActivityNames.NetworkSend,
            "send",
            request.Id,
            token => SendCoreAsync(request, token),
            static result => result is NetworkResponseReceived,
            cancellationToken);
    }
}
