// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A file path expressed relative to whatever root directory a concrete
/// <see cref="IFileSystem"/> implementation is configured with, structurally
/// validated to reject the most obvious forms of directory traversal.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural (ordinal,
/// textual) equality over <see cref="Value"/>, safe to share and compare
/// across threads without synchronization.
/// </para>
/// <para>
/// This constructor rejects an empty path, an absolute (rooted) path, and
/// any path segment equal to <c>..</c>. This is a structural invariant, not
/// the authoritative sandbox boundary: a concrete <see cref="IFileSystem"/>
/// implementation still resolves this path against its configured root and
/// re-validates that the resulting absolute path stays within it before
/// touching disk, so a higher-level check here can never substitute for the
/// low-level implementation's own enforcement.
/// </para>
/// </remarks>
public readonly record struct FileSystemPath
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemPath"/>
    /// struct, validating that it is a relative path without directory
    /// traversal segments.
    /// </summary>
    /// <param name="value">The non-empty, relative, traversal-free path text.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null, empty, or consists only of
    /// whitespace; is rooted (absolute); or contains a <c>..</c> segment.
    /// </exception>
    public FileSystemPath(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (Path.IsPathRooted(value))
        {
            throw new ArgumentException("Path must be relative, not rooted.", nameof(value));
        }

        var segments = value.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        if (Array.Exists(segments, static segment => segment == ".."))
        {
            throw new ArgumentException("Path must not contain a '..' traversal segment.", nameof(value));
        }

        Value = value;
    }

    /// <summary>Gets the relative path text.</summary>
    public string Value { get; }

    /// <summary>Returns the relative path text.</summary>
    public override string ToString() => Value;
}
