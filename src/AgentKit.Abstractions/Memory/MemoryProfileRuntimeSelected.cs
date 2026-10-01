// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports a successfully activated profile runtime.</summary>
/// <remarks>The caller owns the lease and must dispose it after the operation, including authorization, provider calls, storage, required observation, and settlement.</remarks>
public sealed record MemoryProfileRuntimeSelected: MemoryProfileRuntimeSelectionResult
{
    /// <summary>Initializes a selected result.</summary>
    /// <param name="runtime">The owned lease.</param>
    /// <exception cref="ArgumentNullException"><paramref name="runtime"/> is null.</exception>
    public MemoryProfileRuntimeSelected(IMemoryProfileRuntimeLease runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        Runtime = runtime;
    }

    /// <summary>Gets the owned lease.</summary>
    public IMemoryProfileRuntimeLease Runtime { get; }
}
