// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Is the durable retention fence that covers one pending reference-commit window.</summary>
/// <remarks>While the pin is held and its intent is pending, neither expiry nor an orphan sweep may collect the object.</remarks>
public sealed record ArtifactPin
{
    /// <summary>Initializes a retention fence.</summary>
    /// <param name="intentId">The intent the pin covers.</param>
    /// <param name="pinnedAt">The instant the fence was established.</param>
    /// <param name="heldUntil">The instant before which collection is refused; it must be after <paramref name="pinnedAt"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="intentId"/> is empty or <paramref name="heldUntil"/> is not after <paramref name="pinnedAt"/>.</exception>
    public ArtifactPin(ArtifactReferenceCommitIntentId intentId, DateTimeOffset pinnedAt, DateTimeOffset heldUntil)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(intentId, default);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(heldUntil, pinnedAt);
        IntentId = intentId;
        PinnedAt = pinnedAt;
        HeldUntil = heldUntil;
    }

    /// <summary>Gets the intent the pin covers.</summary>
    public ArtifactReferenceCommitIntentId IntentId { get; }

    /// <summary>Gets the instant the fence was established.</summary>
    public DateTimeOffset PinnedAt { get; }

    /// <summary>Gets the instant before which collection is refused.</summary>
    public DateTimeOffset HeldUntil { get; }
}
