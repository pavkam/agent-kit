// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Executes one bounded search attempt against an explicitly selected external service.</summary>
public interface IWebSearchProvider
{
    /// <summary>Gets the stable provider identity.</summary>
    public ProviderId ProviderId { get; }

    /// <summary>Gets the effecting component audience for egress grants.</summary>
    public ComponentId SecurityAudience { get; }

    /// <summary>Gets the canonical configured service destination without credentials.</summary>
    public ProtectedResource Destination { get; }

    /// <summary>Executes one authorized attempt without hidden retries.</summary>
    /// <param name="request">The bounded request whose grant is consumed before query egress.</param>
    /// <param name="cancellationToken">Cancels this attempt.</param>
    /// <returns>The typed terminal provider result.</returns>
    public Task<WebSearchProviderResult> SearchAsync(
        WebSearchRequest request,
        CancellationToken cancellationToken = default);
}
