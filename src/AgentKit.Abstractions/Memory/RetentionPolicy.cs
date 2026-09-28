// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Declares how long memory or document content remains readable before expiry or deletion.</summary>
public sealed record RetentionPolicy
{
    /// <summary>Initializes a retention policy with optional absolute expiry.</summary>
    /// <param name="expiresAt">The instant after which the content must not remain active, when set.</param>
    /// <param name="retainUntilExplicitDeletion">
    /// When <see langword="true"/>, content remains until an explicit deletion transition even without <paramref name="expiresAt"/>.
    /// </param>
    public RetentionPolicy(DateTimeOffset? expiresAt = null, bool retainUntilExplicitDeletion = true)
    {
        ExpiresAt = expiresAt;
        RetainUntilExplicitDeletion = retainUntilExplicitDeletion;
    }

    /// <summary>Gets the instant after which the content must not remain active, when set.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Gets whether content remains until an explicit deletion transition.</summary>
    public bool RetainUntilExplicitDeletion { get; init; }
}
