// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A deliberate, policy-driven decision not to produce a compaction
/// candidate, distinct from an unexpected <see cref="CompactionFailure"/>.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// <see cref="SafeMessage"/> never contains source session content.
/// </remarks>
public sealed record CompactionRejection
{
    /// <summary>Initializes a new instance of the <see cref="CompactionRejection"/> record.</summary>
    /// <param name="kind">The category of this rejection.</param>
    /// <param name="safeMessage">A human-readable, non-sensitive explanation.</param>
    /// <param name="details">Additional caller-specific or forward-compatible detail.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="safeMessage"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="details"/> is null.</exception>
    public CompactionRejection(CompactionRejectionKind kind, string safeMessage, ExtensionData details)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        ArgumentNullException.ThrowIfNull(details);

        Kind = kind;
        SafeMessage = safeMessage;
        Details = details;
    }

    /// <summary>Gets the category of this rejection.</summary>
    public CompactionRejectionKind Kind { get; init; }

    /// <summary>Gets a human-readable, non-sensitive explanation.</summary>
    public string SafeMessage { get; init; }

    /// <summary>Gets additional caller-specific or forward-compatible detail.</summary>
    public ExtensionData Details { get; init; }
}
