// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A content part carrying one model reasoning/thinking segment.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization. Reasoning parts are ordered alongside other
/// content within a message's <see cref="ContentPart"/> array, preserving
/// the sequence in which the model actually interleaved reasoning with
/// visible output or tool calls.
/// </remarks>
public sealed record ReasoningPart: ContentPart
{
    /// <summary>Initializes a new instance of the <see cref="ReasoningPart"/> record.</summary>
    /// <param name="content">The reasoning content.</param>
    /// <param name="extensions">Provider-specific or forward-compatible data.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="content"/> or <paramref name="extensions"/> is null.
    /// </exception>
    public ReasoningPart(ReasoningContent content, ExtensionData extensions)
        : base(extensions)
    {
        ArgumentNullException.ThrowIfNull(content);
        Content = content;
    }

    /// <summary>Gets the reasoning content.</summary>
    /// <exception cref="ArgumentNullException">
    /// The value assigned during initialization or non-destructive mutation is null.
    /// </exception>
    public ReasoningContent Content
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }
}
