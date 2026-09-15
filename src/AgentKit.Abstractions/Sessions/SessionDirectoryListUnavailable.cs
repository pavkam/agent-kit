// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that bounded directory discovery was denied, unavailable, or unsupported.</summary>
public sealed record SessionDirectoryListUnavailable: SessionDirectoryListResult
{
    /// <summary>Initializes an unavailable scan outcome.</summary>
    /// <param name="safeMessage">The nonblank content-safe reason.</param>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is blank.</exception>
    public SessionDirectoryListUnavailable(string safeMessage)
    { ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage); SafeMessage = safeMessage; }
    /// <summary>Gets the content-safe reason.</summary><value>Nonblank text without protected state.</value>
    public string SafeMessage { get; }
}
