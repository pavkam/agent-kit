// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Retains the source and version used to calculate a cost estimate.</summary>
/// <remarks>This is immutable provenance, not a price catalog, exchange-rate service, credential, or authorization capability. The measurement's unit identifies currency.</remarks>
public sealed record UsagePricingReference
{
    /// <summary>Captures nonblank source and version evidence without interpreting either value.</summary>
    /// <param name="source">The nonblank pricing-source description or external reference.</param>
    /// <param name="version">The nonblank captured pricing version.</param>
    /// <exception cref="ArgumentException">Either argument is null, empty, or whitespace.</exception>
    public UsagePricingReference(string source, string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        Source = source;
        Version = version;
    }

    /// <summary>Gets the captured pricing source without fetching or refreshing it.</summary>
    /// <value>The original nonblank provenance text; it must contain no credentials.</value>
    public string Source { get; }
    /// <summary>Gets the pricing version used for this estimate.</summary>
    /// <value>The original nonblank version, retained when a later revision changes pricing.</value>
    public string Version { get; }
}
