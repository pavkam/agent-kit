// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Retains one immutable literal part of an application-facing tool presentation.</summary>
/// <remarks>The value contains no terminal markup or ANSI escapes. Applications choose how to render its semantic kind.</remarks>
public sealed record ToolPresentationPart
{
    /// <summary>Initializes one presentation part.</summary>
    /// <param name="kind">The defined semantic kind.</param>
    /// <param name="text">The nonnull literal content.</param>
    /// <param name="language">An optional nonblank code-language hint, valid only for code.</param>
    /// <param name="path">An optional nonblank affected path, valid only for a diff.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is undefined.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="ArgumentException">A hint is blank or is supplied for an incompatible kind.</exception>
    public ToolPresentationPart(ToolPresentationPartKind kind, string text, string? language = null, string? path = null)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentNullException.ThrowIfNull(text);
        if (language is not null) { ArgumentException.ThrowIfNullOrWhiteSpace(language); }
        if (path is not null) { ArgumentException.ThrowIfNullOrWhiteSpace(path); }
        ArgumentException.ThrowIfNotEqual(language is null || kind == ToolPresentationPartKind.Code, true, nameof(language));
        ArgumentException.ThrowIfNotEqual(path is null || kind == ToolPresentationPartKind.Diff, true, nameof(path));
        Kind = kind; Text = text; Language = language; Path = path;
    }

    /// <summary>Gets the semantic rendering kind.</summary><value>A defined kind.</value>
    public ToolPresentationPartKind Kind { get; }
    /// <summary>Gets the literal content.</summary><value>Nonnull text which may be empty.</value>
    public string Text { get; }
    /// <summary>Gets the optional code-language hint.</summary><value>A nonblank hint only for code, or null.</value>
    public string? Language { get; }
    /// <summary>Gets the optional affected path.</summary><value>A nonblank path only for a diff, or null.</value>
    public string? Path { get; }
}
