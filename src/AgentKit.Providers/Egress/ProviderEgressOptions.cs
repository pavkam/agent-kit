// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Egress;

/// <summary>Host-configurable bounds and classification applied to every provider-egress attempt.</summary>
/// <remarks>
/// The options describe bounds the host chooses for provider traffic; they are not authority. The effective
/// per-attempt response timeout is always the attempt deadline, never longer than the request's own
/// <see cref="LlmModelRequest.Deadline"/>. Redirects are never followed by provider egress, so no redirect limit is
/// configurable: a provider endpoint that answers with a redirect fails the attempt rather than forwarding
/// credentials to another origin.
/// </remarks>
public sealed class ProviderEgressOptions
{
    /// <summary>Gets or sets the maximum time allowed to resolve and connect, clamped to the attempt deadline.</summary>
    /// <value>A positive duration. The default is thirty seconds.</value>
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets the largest request body, in bytes, a provider attempt may transmit.</summary>
    /// <value>A positive byte count. The default is 32 MiB, which matches <see cref="NetworkBounds.DefaultMaximumRequestBytes"/>.</value>
    public long MaximumRequestBytes { get; set; } = NetworkBounds.DefaultMaximumRequestBytes;

    /// <summary>Gets or sets the largest response body, in bytes, a provider attempt may receive, enforced while streaming.</summary>
    /// <value>A positive byte count. The default is 64 MiB.</value>
    public long MaximumResponseBytes { get; set; } = 64L * 1024 * 1024;

    /// <summary>Gets or sets the data classification declared for every provider payload.</summary>
    /// <value>
    /// A defined classification. The default is <see cref="NetworkDataClassification.Confidential"/> because prompts,
    /// tool results, and embedded documents routinely carry customer data. Provider egress has no per-message
    /// classification mapping yet, so a host that sends only public data lowers this explicitly.
    /// </value>
    public NetworkDataClassification Classification { get; set; } = NetworkDataClassification.Confidential;
}
