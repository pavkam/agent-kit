// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The stable identity of one contributor of model descriptors to the
/// engine-wide catalog, such as a static registration, a vendor feed
/// snapshot, or credential-scoped runtime discovery.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural (ordinal, textual)
/// equality over <see cref="Value"/>. It carries no mutable state and is safe
/// to share and compare across threads without synchronization.
/// </para>
/// <para>
/// Source identity is retained on composition diagnostics so a duplicate or
/// conflicting alias can be attributed to the registration that produced it,
/// rather than surfacing as an anonymous catalog error.
/// </para>
/// </remarks>
public readonly record struct ModelDescriptorSourceId
{
    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="ModelDescriptorSourceId"/> struct, validating that it
    /// carries usable identity text.
    /// </summary>
    /// <param name="value">The non-empty canonical source identity.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public ModelDescriptorSourceId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical source identity text.</summary>
    public string Value { get; }

    /// <summary>
    /// Returns the canonical source identity text, suitable for logging and
    /// catalog-composition diagnostics.
    /// </summary>
    public override string ToString() => Value;
}
