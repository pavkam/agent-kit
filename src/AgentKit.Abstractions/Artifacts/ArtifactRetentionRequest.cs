// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Carries the requested and default retention a policy resolves into one decision.</summary>
public sealed record ArtifactRetentionRequest
{
    /// <summary>Initializes a retention resolution request.</summary>
    /// <param name="metadata">The declared artifact metadata carrying the requested retention.</param>
    /// <param name="profileDefault">The profile's default retention.</param>
    /// <param name="now">The evaluation instant.</param>
    /// <exception cref="ArgumentNullException">A reference value is null.</exception>
    public ArtifactRetentionRequest(ArtifactMetadata metadata, ArtifactRetention profileDefault, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(profileDefault);
        Metadata = metadata;
        ProfileDefault = profileDefault;
        Now = now;
    }

    /// <summary>Gets the declared artifact metadata.</summary>
    public ArtifactMetadata Metadata { get; }

    /// <summary>Gets the profile's default retention.</summary>
    public ArtifactRetention ProfileDefault { get; }

    /// <summary>Gets the evaluation instant.</summary>
    public DateTimeOffset Now { get; }
}
