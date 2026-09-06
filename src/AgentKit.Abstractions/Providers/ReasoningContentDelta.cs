// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// One incremental fragment of hidden or partially visible reasoning
/// content.
/// </summary>
public sealed record ReasoningContentDelta: ContentDelta
{
    /// <summary>Initializes a new instance of the <see cref="ReasoningContentDelta"/> record.</summary>
    /// <param name="text">The incremental reasoning text fragment.</param>
    /// <param name="extensions">Provider-specific reasoning data, such as a partial signature.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="text"/> or <paramref name="extensions"/> is null.
    /// </exception>
    public ReasoningContentDelta(string text, ExtensionData extensions)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(extensions);

        Text = text;
        Extensions = extensions;
    }

    /// <summary>Gets the incremental reasoning text fragment.</summary>
    public string Text { get; init; }

    /// <summary>Gets provider-specific reasoning data, such as a partial signature.</summary>
    public ExtensionData Extensions { get; init; }
}
