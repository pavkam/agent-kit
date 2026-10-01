// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Asks a document store to atomically switch a document's active-version pointer to an already staged version.</summary>
/// <remarks>The switch is a compare-and-set on <see cref="ExpectedActiveVersion"/>, so two publishers cannot both believe they replaced the same version. Activating the version that is already active replays.</remarks>
public sealed record DocumentActivateRequest
{
    /// <summary>Initializes a validated activation request.</summary>
    /// <param name="id">The document identity.</param>
    /// <param name="version">The staged version to activate.</param>
    /// <param name="expectedActiveVersion">The version that must currently be active, or <see langword="null"/> when none may be.</param>
    /// <param name="idempotencyKey">The replay key.</param>
    /// <param name="at">The activation instant.</param>
    /// <param name="grant">The single-use grant for this exact operation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="grant"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is default.</exception>
    /// <exception cref="ArgumentException">A version or key is blank, or the grant lacks captured authorization.</exception>
    public DocumentActivateRequest(
        DocumentId id,
        DocumentVersion version,
        DocumentVersion? expectedActiveVersion,
        IdempotencyKey idempotencyKey,
        DateTimeOffset at,
        SecurityGrant grant)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default, nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(version.Value, nameof(version));
        if (expectedActiveVersion is { } expected)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(expected.Value, nameof(expectedActiveVersion));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(grant.Authorization, nameof(grant));
        Id = id;
        Version = version;
        ExpectedActiveVersion = expectedActiveVersion;
        IdempotencyKey = idempotencyKey;
        At = at;
        Grant = grant;
    }

    /// <summary>Gets the document identity.</summary>
    public DocumentId Id { get; }

    /// <summary>Gets the staged version to activate.</summary>
    public DocumentVersion Version { get; }

    /// <summary>Gets the version that must currently be active, or <see langword="null"/> when none may be.</summary>
    public DocumentVersion? ExpectedActiveVersion { get; }

    /// <summary>Gets the replay key.</summary>
    public IdempotencyKey IdempotencyKey { get; }

    /// <summary>Gets the activation instant.</summary>
    public DateTimeOffset At { get; }

    /// <summary>Gets the single-use grant for this exact operation.</summary>
    public SecurityGrant Grant { get; }
}
