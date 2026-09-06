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
/// three fields. It carries no mutable state and is safe to share and
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
/// <para>
/// <see cref="Required"/> distinguishes two different failure policies for
/// that resolution: when <see langword="true"/>, an unresolved capability is
/// a hard composition failure; when <see langword="false"/>, the capability
/// is best-effort and its absence is tolerated (though its presence, once
/// registered, still has to be fully resolvable — a partially wired optional
/// capability is still a composition error).
/// </para>
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
    /// <param name="required">
    /// <see langword="true"/> if composition validation must fail when the
    /// capability cannot be resolved; <see langword="false"/> if it should
    /// be treated as best-effort when entirely absent.
    /// </param>
    public AgentCapabilityReference(
        CapabilityId capabilityId,
        CapabilityProfileId profileId,
        bool required)
    {
        CapabilityId = capabilityId;
        ProfileId = profileId;
        Required = required;
    }

    /// <summary>Gets the referenced capability.</summary>
    public CapabilityId CapabilityId { get; init; }

    /// <summary>
    /// Gets the configured profile of <see cref="CapabilityId"/> this
    /// definition wants.
    /// </summary>
    public CapabilityProfileId ProfileId { get; init; }

    /// <summary>
    /// Gets a value indicating whether composition validation must fail
    /// when the capability cannot be resolved, as opposed to tolerating its
    /// complete absence.
    /// </summary>
    public bool Required { get; init; }
}
