// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Declares which capability profiles one capability package registered, so composition validation can prove an
/// <see cref="AgentCapabilityReference"/> resolves without referencing that package.
/// </summary>
/// <remarks>
/// <para>
/// A capability package registers one source per <see cref="CapabilityId"/> it owns, additively. Validation of a
/// published definition requires, for every neutral capability reference, exactly one source for the reference's
/// <see cref="AgentCapabilityReference.CapabilityId"/> whose <see cref="Contains"/> reports the referenced profile.
/// Selection without registration fails definition validation; omission means the agent does not have the capability.
/// </para>
/// <para>
/// Implementations answer synchronously from already-registered, immutable state. They perform no I/O, resolve no
/// secret, and probe no remote endpoint; external availability is checked at the operation boundary. They are
/// thread-safe.
/// </para>
/// </remarks>
public interface IAgentCapabilityProfileSource
{
    /// <summary>Gets the capability this source answers for.</summary>
    /// <value>A nonblank capability identity, unique among registered sources.</value>
    public CapabilityId CapabilityId { get; }

    /// <summary>Determines whether the package registered a complete profile with this identity.</summary>
    /// <param name="profileId">The nonblank profile identity a definition references.</param>
    /// <returns><see langword="true"/> when the profile and its required collaborators are registered.</returns>
    /// <exception cref="ArgumentException"><paramref name="profileId"/> is the default value.</exception>
    public bool Contains(CapabilityProfileId profileId);
}
