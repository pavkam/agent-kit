// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A root-relative path that has already passed deterministic lexical
/// normalization and cannot contain traversal or ambient-directory semantics.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural (ordinal, textual)
/// equality over <see cref="Value"/>.
/// </para>
/// <para>
/// Pure lexical normalization precedes authorization. Inspecting links, mounts,
/// or host metadata requires separate bounded observation authority.
/// </para>
/// </remarks>
public readonly record struct NormalizedRelativePath
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NormalizedRelativePath"/>
    /// struct from already normalized relative path text.
    /// </summary>
    /// <param name="value">The non-empty, relative, traversal-free path text.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null, empty, or consists only of whitespace;
    /// contains an embedded NUL character; is rooted; or contains a
    /// <c>..</c> segment.
    /// </exception>
    public NormalizedRelativePath(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentException.ThrowIfContainsNul(value);

        if (Path.IsPathRooted(value))
        {
            throw new ArgumentException("Path must be relative, not rooted.", nameof(value));
        }

        var segments = value.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        if (Array.Exists(segments, static segment => segment == ".."))
        {
            throw new ArgumentException("Path must not contain a '..' traversal segment.", nameof(value));
        }

        Value = string.Join('/', segments.Where(static segment => segment != "."));
        ArgumentException.ThrowIfNullOrWhiteSpace(Value, nameof(value));
    }

    /// <summary>Gets the normalized relative path text.</summary>
    public string Value { get; }

    /// <inheritdoc/>
    public override string ToString() => Value;
}
