// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports successful normalization of invocation evidence under a captured snapshot.</summary>
public sealed record ToolResultNormalized: ToolResultNormalizationResult
{
    /// <summary>Retains normalized terminal content and measured normalization evidence.</summary>
    /// <param name="content">The initialized normalized content within the captured part bound.</param>
    /// <param name="info">The measured normalization evidence for the terminal record.</param>
    /// <exception cref="ArgumentException"><paramref name="content"/> contains a null entry.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="info"/> is null.</exception>
    public ToolResultNormalized(ImmutableArray<ToolResultContent> content, ToolResultNormalizationInfo info)
    {
        ArgumentException.ThrowIfContainsNull(content);
        ArgumentNullException.ThrowIfNull(info);
        Content = content;
        Info = info;
    }

    /// <summary>Gets the normalized terminal content.</summary>
    /// <value>Initialized content owned independently of the invoker's raw parts.</value>
    public ImmutableArray<ToolResultContent> Content { get; }

    /// <summary>Gets the measured normalization evidence.</summary>
    /// <value>Transformation and byte/part accounting selected by the normalizer.</value>
    public ToolResultNormalizationInfo Info { get; }
}
