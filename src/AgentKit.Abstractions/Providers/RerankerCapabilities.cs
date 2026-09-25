// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Portable behaviors one reranker descriptor supports.</summary>
public sealed record RerankerCapabilities
{
    /// <summary>Initializes reranker capabilities.</summary>
    /// <param name="supportsTopCount">Whether the reranker accepts a top-count limit.</param>
    /// <param name="extensions">Provider-specific capability data.</param>
    /// <exception cref="ArgumentNullException"><paramref name="extensions"/> is null.</exception>
    public RerankerCapabilities(bool supportsTopCount, ExtensionData extensions)
    {
        ArgumentNullException.ThrowIfNull(extensions);
        SupportsTopCount = supportsTopCount;
        Extensions = extensions;
    }

    /// <summary>Gets whether a top-count limit is supported.</summary>
    public bool SupportsTopCount { get; init; }

    /// <summary>Gets provider-specific capability data.</summary>
    public ExtensionData Extensions { get; init; }
}
