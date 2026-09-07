// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Sends one already-resolved network request, enforcing configured response and redirect bounds.</summary>
/// <remarks>
/// See <see cref="NetworkDestinationPolicy"/> for the reduced-scope
/// rationale shared by every implementation of this contract. An
/// implementation follows same-origin and cross-origin redirects up to the
/// request's configured <see cref="NetworkBounds.MaximumRedirects"/>,
/// re-validating the destination policy against every redirect target
/// before following it, and reports
/// <see cref="NetworkRedirectLimitExceeded"/> rather than following one
/// more redirect once that bound is reached.
/// </remarks>
public interface INetworkTransport
{
    /// <summary>Sends one request.</summary>
    /// <param name="request">The request to send.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task producing the terminal send outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public ValueTask<NetworkSendResult> SendAsync(NetworkRequest request, CancellationToken cancellationToken = default);
}
