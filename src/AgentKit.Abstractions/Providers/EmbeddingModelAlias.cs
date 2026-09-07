// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// An application-facing selection key for one configured
/// <see cref="EmbeddingModelDescriptor"/>, such as "default-embeddings",
/// independent of the actual <see cref="ProviderId"/>,
/// <see cref="ApiFamilyId"/>, and <see cref="ModelId"/> it currently
/// resolves to.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural (ordinal,
/// textual) equality over <see cref="Value"/>. It carries no mutable state
/// itself and is safe to share and compare across threads without
/// synchronization.
/// </para>
/// <para>
/// This is a distinct alias type from <see cref="ModelAlias"/>, not a
/// reuse of it, because aliases are unique within their operation kind: the
/// same alias text can independently name a conversational model and an
/// embedding model without merging their contracts or selection paths.
/// </para>
/// </remarks>
public readonly record struct EmbeddingModelAlias
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddingModelAlias"/>
    /// struct, validating that it carries usable selection text.
    /// </summary>
    /// <param name="value">The non-empty canonical alias text.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public EmbeddingModelAlias(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical alias text.</summary>
    public string Value { get; }

    /// <summary>Returns the canonical alias text.</summary>
    public override string ToString() => Value;
}
