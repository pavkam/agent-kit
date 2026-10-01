// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Egress;

/// <summary>Reports that every egress boundary admitted the request and the provider answered.</summary>
public sealed record ProviderEgressSent: ProviderEgressResult
{
    /// <summary>Initializes a new instance of the <see cref="ProviderEgressSent"/> record.</summary>
    /// <param name="response">The owned response handle.</param>
    /// <exception cref="ArgumentNullException"><paramref name="response"/> is null.</exception>
    public ProviderEgressSent(ProviderEgressResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        Response = response;
    }

    /// <summary>Gets the response handle.</summary>
    /// <value>The non-null handle. The recipient owns it and disposes it asynchronously exactly once.</value>
    public ProviderEgressResponse Response { get; init; }
}
