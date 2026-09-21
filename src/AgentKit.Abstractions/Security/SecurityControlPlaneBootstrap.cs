// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Host-supplied evidence that authorizes opening or creating one fixed security control-plane persistence target
/// without recursively requesting grants or required audit from the authority being bootstrapped.
/// </summary>
/// <param name="CapabilityId">The non-blank stable identifier of the bounded bootstrap capability that issued this evidence.</param>
/// <param name="IssuedAt">The instant the host recorded this bootstrap evidence.</param>
/// <remarks>
/// Bootstrap evidence is configuration-bound, not caller-selected at runtime. Stores validate that initialization
/// was performed with explicit bootstrap proof before accepting grant, approval, or decision writes.
/// </remarks>
public sealed record SecurityControlPlaneBootstrap(string CapabilityId, DateTimeOffset IssuedAt)
{
    /// <summary>Gets the bounded bootstrap capability identifier.</summary>
    /// <exception cref="ArgumentException">The value is blank.</exception>
    public string CapabilityId { get; } = !string.IsNullOrWhiteSpace(CapabilityId)
        ? CapabilityId
        : throw new ArgumentException("The capability identifier cannot be blank.", nameof(CapabilityId));
}
