// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports the closed outcome of terminal result normalization.</summary>
public abstract record ToolResultNormalizationResult
{
    /// <summary>Initializes the closed normalization outcome discriminator.</summary>
    private protected ToolResultNormalizationResult() =>
        ArgumentException.ThrowIfNotEqual(this is ToolResultNormalized or ToolResultNormalizationFailed, true, "result");

    /// <summary>Copies an existing normalization outcome.</summary>
    /// <param name="original">The outcome to copy.</param>
    /// <exception cref="ArgumentNullException"><paramref name="original"/> is null.</exception>
    protected ToolResultNormalizationResult(ToolResultNormalizationResult original)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentException.ThrowIfNotEqual(this is ToolResultNormalized or ToolResultNormalizationFailed, true, "result");
    }
}
