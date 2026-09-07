// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// An embedding vector of ordinary floating-point elements, one per
/// dimension.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </remarks>
public sealed record DenseFloatVector: EmbeddingVector
{
    /// <summary>Initializes a new instance of the <see cref="DenseFloatVector"/> record.</summary>
    /// <param name="values">The ordered, non-empty vector elements.</param>
    /// <exception cref="ArgumentException"><paramref name="values"/> is a default or empty array.</exception>
    public DenseFloatVector(ImmutableArray<float> values)
    {
        ArgumentException.ThrowIfDefaultOrEmpty(values);
        Values = values;
    }

    /// <summary>Gets the ordered, non-empty vector elements.</summary>
    public ImmutableArray<float> Values { get; init; }

    /// <inheritdoc/>
    public bool Equals(DenseFloatVector? other) => other is not null && Values.SequenceEqual(other.Values);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var value in Values)
        {
            hash.Add(value);
        }

        return hash.ToHashCode();
    }
}
