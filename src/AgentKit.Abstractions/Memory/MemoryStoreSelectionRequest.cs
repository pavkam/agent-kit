// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Asks a selector for the memory store registered under one exact key.</summary>
public sealed record MemoryStoreSelectionRequest
{
    /// <summary>Initializes a validated request.</summary>
    /// <param name="key">The exact store key.</param>
    /// <exception cref="ArgumentException"><paramref name="key"/> is default or blank.</exception>
    public MemoryStoreSelectionRequest(MemoryStoreKey key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        Key = key;
    }

    /// <summary>Gets the exact store key.</summary>
    public MemoryStoreKey Key { get; }
}
