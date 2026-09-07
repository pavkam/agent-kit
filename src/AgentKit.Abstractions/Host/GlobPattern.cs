// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Represents a workspace-relative glob in the pinned AgentKit simple-glob dialect.</summary>
/// <remarks>The dialect supports <c>*</c> and <c>?</c> within one path segment and <c>**</c> as a complete recursive segment. Character classes, braces, rooted paths, traversal, and backslashes are rejected.</remarks>
public readonly record struct GlobPattern
{
    /// <summary>The maximum accepted UTF-16 pattern length.</summary>
    public const int MaximumLength = 4_096;

    /// <summary>The maximum number of path segments accepted by the dialect.</summary>
    public const int MaximumSegments = 256;

    /// <summary>Initializes and validates a simple glob pattern.</summary>
    /// <param name="value">The non-empty forward-slash-separated relative pattern.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is blank, rooted, contains traversal or unsupported syntax, or embeds <c>**</c> within a segment.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> exceeds a dialect complexity bound.</exception>
    public GlobPattern(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value.Length, MaximumLength, nameof(value));
        if (value[0] == '/' || value.Contains('\\', StringComparison.Ordinal))
        {
            throw new ArgumentException("Glob patterns must be relative and use forward slashes.", nameof(value));
        }

        var segments = value.Split('/', StringSplitOptions.None);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(segments.Length, MaximumSegments, nameof(value));
        if (segments.Any(static segment => segment.Length == 0 || segment is "." or "..")
            || value.IndexOfAny(['[', ']', '{', '}']) >= 0
            || segments.Any(static segment => segment.Contains("**", StringComparison.Ordinal) && segment != "**"))
        {
            throw new ArgumentException("Glob pattern contains traversal, empty segments, or unsupported syntax.", nameof(value));
        }

        Value = value;
    }

    /// <summary>Gets the canonical pattern text.</summary>
    public string Value { get; }

    /// <summary>Returns the canonical pattern text.</summary>
    public override string ToString() => Value;
}
