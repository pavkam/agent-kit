// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Asks a document store to read one document version and, optionally, its chunk set.</summary>
/// <remarks>Without a version the read resolves the document's active version, so a caller never sees a staged or superseded version by accident. An item outside the authorized tenant, agent, or principal visibility is reported as not found.</remarks>
public sealed record DocumentReadRequest
{
    /// <summary>Initializes a validated read request.</summary>
    /// <param name="id">The document identity.</param>
    /// <param name="version">The exact version to read, or <see langword="null"/> for the active version.</param>
    /// <param name="includeChunks">Whether the version's complete chunk set is returned.</param>
    /// <param name="grant">The single-use grant for this exact read.</param>
    /// <exception cref="ArgumentNullException"><paramref name="grant"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is default.</exception>
    /// <exception cref="ArgumentException">A version is blank or the grant lacks captured authorization.</exception>
    public DocumentReadRequest(DocumentId id, DocumentVersion? version, bool includeChunks, SecurityGrant grant)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default, nameof(id));
        if (version is { } requested)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(requested.Value, nameof(version));
        }

        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(grant.Authorization, nameof(grant));
        Id = id;
        Version = version;
        IncludeChunks = includeChunks;
        Grant = grant;
    }

    /// <summary>Gets the document identity.</summary>
    public DocumentId Id { get; }

    /// <summary>Gets the exact version to read, or <see langword="null"/> for the active version.</summary>
    public DocumentVersion? Version { get; }

    /// <summary>Gets a value indicating whether the complete chunk set is returned.</summary>
    public bool IncludeChunks { get; }

    /// <summary>Gets the single-use grant for this exact read.</summary>
    public SecurityGrant Grant { get; }
}
