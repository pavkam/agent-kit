// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Describes one retrieval source's identity, version, and what it needs from the pipeline.</summary>
/// <remarks>The version is folded into every candidate's source identity so exposure decisions and citations bind exactly the source revision that produced them.</remarks>
public sealed record RetrievalSourceDescriptor
{
    /// <summary>Initializes a validated descriptor.</summary>
    /// <param name="key">The source's registration key.</param>
    /// <param name="version">The non-blank source version.</param>
    /// <param name="requiresEmbedding"><see langword="true"/> when the source searches a vector space and needs the query embedded.</param>
    /// <param name="securityAudience">The component identity that read grants for this source must name.</param>
    /// <exception cref="ArgumentException">A key, version, or audience is blank.</exception>
    public RetrievalSourceDescriptor(RetrievalSourceKey key, string version, bool requiresEmbedding, ComponentId securityAudience)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(securityAudience.Value, nameof(securityAudience));
        Key = key;
        Version = version;
        RequiresEmbedding = requiresEmbedding;
        SecurityAudience = securityAudience;
    }

    /// <summary>Gets the source's registration key.</summary>
    public RetrievalSourceKey Key { get; }

    /// <summary>Gets the source version.</summary>
    public string Version { get; }

    /// <summary>Gets a value indicating whether the source needs the query embedded.</summary>
    public bool RequiresEmbedding { get; }

    /// <summary>Gets the component identity read grants must name.</summary>
    public ComponentId SecurityAudience { get; }
}
