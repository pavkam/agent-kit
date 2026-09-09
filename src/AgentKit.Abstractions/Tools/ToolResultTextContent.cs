// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Retains bounded textual terminal content with its portable semantics.</summary>
public sealed record ToolResultTextContent: ToolResultContent
{
    /// <summary>Initializes textual terminal content.</summary>
    /// <param name="text">The nonnull text retained by the normalizer.</param>
    /// <param name="semantics">The defined text semantics.</param>
    /// <param name="extensions">Compatible immutable field evidence.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> or <paramref name="extensions"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="semantics"/> is undefined.</exception>
    public ToolResultTextContent(string text, TextSemantics semantics, ExtensionData extensions)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfUndefined(semantics);
        ArgumentNullException.ThrowIfNull(extensions);
        Text = text;
        Semantics = semantics;
        Extensions = extensions;
    }

    /// <summary>Gets the retained text.</summary><value>Nonnull text, including valid empty text.</value>
    public string Text { get; }
    /// <summary>Gets the portable text semantics.</summary><value>A defined semantic classification.</value>
    public TextSemantics Semantics { get; }
    /// <summary>Gets compatible field evidence.</summary><value>A nonnull immutable bag.</value>
    public ExtensionData Extensions { get; }
}
