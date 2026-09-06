// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Identifies one produced <see cref="CompactionManifest"/>, distinct from
/// the logical <see cref="CompactionId"/> it belongs to (a retried attempt
/// can produce a new manifest under the same checkpoint identity).
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over
/// <see cref="Value"/>, safe to share across threads without
/// synchronization.
/// </remarks>
public readonly record struct CompactionManifestId
{
    /// <summary>Initializes a new instance of the <see cref="CompactionManifestId"/> struct.</summary>
    /// <param name="value">The non-empty underlying globally unique identifier.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is <see cref="Guid.Empty"/>.
    /// </exception>
    public CompactionManifestId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty, nameof(value));
        Value = value;
    }

    /// <summary>Gets the underlying globally unique identifier.</summary>
    public Guid Value { get; }

    /// <summary>Returns the canonical text form of this identity.</summary>
    public override string ToString() => Value.ToString("D");
}
