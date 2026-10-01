// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Storage;

/// <summary>Is the persisted form of an <see cref="EvidenceReference"/>.</summary>
/// <param name="Kind">The evidence category.</param>
/// <param name="Reference">The opaque locator.</param>
/// <param name="Fingerprint">The content fingerprint, or <see langword="null"/>.</param>
internal sealed record GoalEvidenceDocument(string Kind, string Reference, string? Fingerprint)
{
    /// <summary>Converts a reference to its persisted form.</summary>
    /// <param name="value">The non-null reference.</param>
    /// <returns>The document.</returns>
    internal static GoalEvidenceDocument FromDomain(EvidenceReference value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(value.Kind, value.Reference, value.Fingerprint?.Value);
    }

    /// <summary>Restores the reference, re-running its validation.</summary>
    /// <returns>The reference.</returns>
    internal EvidenceReference ToDomain() => new(Kind, Reference, Fingerprint is null ? null : new ContentHash(Fingerprint));
}
