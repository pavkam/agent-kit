// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// One incremental fragment of visible response text.
/// </summary>
public sealed record TextContentDelta: ContentDelta
{
    /// <summary>Initializes a new instance of the <see cref="TextContentDelta"/> record.</summary>
    /// <param name="text">The incremental text fragment.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public TextContentDelta(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text;
    }

    /// <summary>Gets the incremental text fragment.</summary>
    public string Text { get; init; }
}
