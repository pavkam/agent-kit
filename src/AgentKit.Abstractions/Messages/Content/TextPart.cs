// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A content part carrying plain, markdown, or code text — the most common
/// content part, used for ordinary conversational text from any role.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural (ordinal) equality
/// over its fields. It carries no mutable state and is safe to share across
/// threads without synchronization.
/// </remarks>
public sealed record TextPart: ContentPart
{
    /// <summary>Initializes a new instance of the <see cref="TextPart"/> record.</summary>
    /// <param name="text">The literal text content.</param>
    /// <param name="semantics">How the text should be interpreted or rendered.</param>
    /// <param name="extensions">Provider-specific or forward-compatible data.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="text"/> or <paramref name="extensions"/> is null.
    /// </exception>
    public TextPart(string text, TextSemantics semantics, ExtensionData extensions)
        : base(extensions)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text;
        Semantics = semantics;
    }

    /// <summary>Gets the literal text content.</summary>
    public string Text { get; init; }

    /// <summary>Gets how the text should be interpreted or rendered.</summary>
    public TextSemantics Semantics { get; init; }
}
