// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Immutable identity and version metadata for one sandbox profile implementation.</summary>
public sealed record SandboxDescriptor
{
    /// <summary>Initializes sandbox profile metadata.</summary>
    /// <param name="profileId">The stable profile identity.</param>
    /// <param name="version">The non-negative profile version.</param>
    /// <param name="displayName">The non-empty human-readable profile name.</param>
    /// <exception cref="ArgumentException">An identity or name is invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> is negative.</exception>
    public SandboxDescriptor(SandboxProfileId profileId, long version, string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId.Value, nameof(profileId));
        ArgumentOutOfRangeException.ThrowIfNegative(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ProfileId = profileId;
        Version = version;
        DisplayName = displayName;
    }

    /// <summary>Gets the stable profile identity.</summary>
    public SandboxProfileId ProfileId { get; init; }

    /// <summary>Gets the non-negative profile version.</summary>
    public long Version { get; init; }

    /// <summary>Gets the human-readable profile name.</summary>
    public string DisplayName { get; init; }
}
