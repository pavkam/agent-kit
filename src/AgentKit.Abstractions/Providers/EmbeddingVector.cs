// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The immutable base for one decoded embedding vector.
/// </summary>
/// <remarks>
/// <para>
/// This is a closed discriminated hierarchy. The concrete kinds are
/// <see cref="DenseFloatVector"/>, <see cref="QuantizedByteVector"/>, and
/// <see cref="PackedBinaryVector"/>. Its constructor is
/// <see langword="private protected"/>, so no assembly outside
/// AgentKit.Abstractions can add a fourth kind.
/// </para>
/// <para>
/// A wire encoding such as base64 is never surfaced as its own vector
/// kind: it is a transport detail decoded into one of the typed kinds
/// above before it reaches application code, since a base64 string is an
/// encoding, not a distinct embedding space.
/// </para>
/// </remarks>
public abstract record EmbeddingVector
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddingVector"/>
    /// record. This constructor is <see langword="private protected"/> so
    /// only the closed set of kinds declared in this assembly can extend
    /// the hierarchy.
    /// </summary>
    private protected EmbeddingVector()
    {
    }
}
