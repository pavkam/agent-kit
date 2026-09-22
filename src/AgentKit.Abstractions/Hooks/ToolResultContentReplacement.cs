// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Collections.Immutable;

/// <summary>Permitted bounded replacement content for one terminal tool result at the tool-result hook point.</summary>
/// <remarks>
/// Hooks may supply replacement normalized content only; terminal status, security evidence, and side-effect
/// certainty remain read-only on <see cref="ToolResultHookEventArgs.OriginalResult"/>.
/// </remarks>
public sealed record ToolResultContentReplacement
{
    /// <summary>Initializes bounded replacement content.</summary>
    /// <param name="content">The initialized replacement parts obeying the captured normalization bounds.</param>
    /// <exception cref="ArgumentException"><paramref name="content"/> is uninitialized or contains null.</exception>
    public ToolResultContentReplacement(ImmutableArray<ToolResultContent> content)
    {
        ArgumentException.ThrowIfDefault(content);
        ArgumentException.ThrowIfContainsNull(content);
        Content = content;
    }

    /// <summary>Gets the replacement normalized content.</summary>
    /// <value>An initialized, nonnull sequence of bounded parts.</value>
    public ImmutableArray<ToolResultContent> Content { get; }
}
