// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts;

/// <summary>Configures one logical artifact profile: the directory-to-backend routes, default retention, and what content it admits.</summary>
/// <remarks>
/// A profile is a logical policy and directory map. Its backend routes appear only here and in the immutable profile snapshot; they
/// never appear in a request or reference. Changing a route requires a new <see cref="Version"/>, and every retained version keeps
/// resolving the references it created.
/// </remarks>
public sealed class ArtifactProfileOptions
{
    /// <summary>Gets or sets the positive profile revision.</summary>
    /// <value>The revision a finalized reference captures. The default is 1.</value>
    public ArtifactProfileVersion Version { get; set; } = new(1);

    /// <summary>Gets or sets the logical directory used when a caller names none, such as complete process output.</summary>
    /// <value>A directory that must also be routed. It has no default: a profile declares it explicitly.</value>
    public ArtifactDirectoryId? DefaultDirectory { get; set; }

    /// <summary>Gets the map from logical directory to the backend that stores its artifacts.</summary>
    public Dictionary<ArtifactDirectoryId, ArtifactBackendKey> Routes { get; } = [];

    /// <summary>Gets or sets the retention applied when an artifact requests none that supersedes it.</summary>
    /// <value>The default is a <c>default</c> policy with neither expiry nor legal hold.</value>
    public ArtifactRetention? DefaultRetention { get; set; }

    /// <summary>Gets the mutability modes this profile admits.</summary>
    /// <value>Only <see cref="ArtifactMutability.Immutable"/> by default.</value>
    public HashSet<ArtifactMutability> AllowedMutability { get; } = [ArtifactMutability.Immutable];

    /// <summary>Gets or sets whether this profile admits externally owned content.</summary>
    /// <value><see langword="false"/> by default.</value>
    public bool AllowExternalOwnership { get; set; }
}
