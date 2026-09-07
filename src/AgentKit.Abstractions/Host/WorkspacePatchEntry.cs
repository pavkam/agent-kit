// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Base contract for one fully planned and separately authorized patch entry.</summary>
public abstract record WorkspacePatchEntry
{
    /// <summary>Initializes the shared immutable identity, kind, and authority for a concrete entry.</summary>
    /// <param name="id">The non-empty mutation identity.</param>
    /// <param name="kind">The defined concrete entry kind.</param>
    /// <param name="grant">The exact non-null single-entry authority.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is empty or <paramref name="kind"/> is undefined.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="grant"/> is null.</exception>
    private protected WorkspacePatchEntry(
        WorkspaceMutationId id,
        WorkspacePatchEntryKind kind,
        SecurityGrant grant)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id.Value, Guid.Empty, nameof(id));
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentNullException.ThrowIfNull(grant);
        Id = id;
        Kind = kind;
        Grant = grant;
    }

    /// <summary>Gets the stable mutation identity.</summary>
    public WorkspaceMutationId Id { get; init; }
    /// <summary>Gets the concrete entry kind.</summary>
    public WorkspacePatchEntryKind Kind { get; init; }
    /// <summary>Gets the exact single-use authority for this entry and its secondary resources.</summary>
    public SecurityGrant Grant { get; init; }
}
