// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Distinguishes how a <see cref="TextPart"/> should be interpreted or
/// rendered.
/// </summary>
/// <remarks>
/// This value influences presentation and downstream processing (for
/// example, whether a UI should render markdown formatting or a tool should
/// treat the text as executable source), not conversational role. Role —
/// who produced the text — is carried by the concrete
/// <see cref="AgentMessage"/> kind the part belongs to, never by this enum.
/// </remarks>
public enum TextSemantics
{
    /// <summary>Unformatted, literal text with no special rendering.</summary>
    Plain,

    /// <summary>Markdown-formatted text intended for rich rendering.</summary>
    Markdown,

    /// <summary>
    /// Source code or another literal, non-prose text block, such as
    /// content that should be rendered in a fixed-width code block rather
    /// than reflowed as prose.
    /// </summary>
    Code
}
