// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A validated non-empty semantic key naming one configured file root within a
/// file-system profile.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural (ordinal, textual)
/// equality over <see cref="Value"/>. It is not a generated operation identity.
/// </remarks>
public readonly record struct FileRootId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FileRootId"/> struct,
    /// validating that it carries usable key text.
    /// </summary>
    /// <param name="value">The non-empty root key text.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null, empty, or consists only of whitespace.
    /// </exception>
    public FileRootId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the root key text.</summary>
    public string Value { get; }

    /// <inheritdoc/>
    public override string ToString() => Value;
}
