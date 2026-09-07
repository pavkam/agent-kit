// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// References one optional capability and configured profile an agent
/// definition selects, such as a durability, memory, or goal axis that sits
/// outside the mandatory runnable spine every agent must select.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// capability and profile identities. It carries no mutable state and is safe to share and
/// compare across threads without synchronization.
/// </para>
/// <para>
/// Optional capabilities make demonstrated extension axes explicit instead
/// of smuggling ad-hoc behavior through untyped extension data. Once a
/// definition includes an <see cref="AgentCapabilityReference"/>,
/// composition validation treats that capability as if it were mandatory
/// for this agent: it must resolve to its registered package, profile,
/// store or transport, security policy, and every other required
/// collaborator, or the definition fails validation before the engine
/// becomes runnable.
/// </para>
/// Optionality is represented only by omitting a reference. Every reference
/// present in a definition is required to resolve completely.
/// </remarks>
public sealed record AgentCapabilityReference
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AgentCapabilityReference"/>
    /// record.
    /// </summary>
    /// <param name="capabilityId">The referenced capability.</param>
    /// <param name="profileId">
    /// The configured profile of <paramref name="capabilityId"/> this
    /// definition wants, distinguishing it from other profiles of the same
    /// capability configured for different agents.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="capabilityId"/> or <paramref name="profileId"/> is the
    /// default identity and therefore has no usable identifier text.
    /// </exception>
    public AgentCapabilityReference(
        CapabilityId capabilityId,
        CapabilityProfileId profileId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityId.Value, nameof(capabilityId));
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId.Value, nameof(profileId));

        CapabilityId = capabilityId;
        ProfileId = profileId;
    }

    /// <summary>Gets the referenced capability.</summary>
    public CapabilityId CapabilityId { get; }

    /// <summary>
    /// Gets the configured profile of <see cref="CapabilityId"/> this
    /// definition wants.
    /// </summary>
    public CapabilityProfileId ProfileId { get; }
}
