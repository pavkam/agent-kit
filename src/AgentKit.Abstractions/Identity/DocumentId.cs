// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A caller-supplied correlation identity for one document in a
/// <see cref="RerankRequest"/>.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over
/// <see cref="Value"/>. It carries no mutable state and is safe to share
/// across threads without synchronization.
/// </remarks>
public readonly record struct DocumentId
{
    /// <summary>Initializes a new document identity.</summary>
    /// <param name="value">The non-empty underlying globally unique identifier.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is <see cref="Guid.Empty"/>.</exception>
    public DocumentId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty, nameof(value));
        Value = value;
    }

    /// <summary>Gets the underlying globally unique identifier.</summary>
    public Guid Value { get; }

    /// <inheritdoc/>
    public override string ToString() => Value.ToString("D");
}
