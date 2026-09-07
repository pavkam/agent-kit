// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Output;

/// <summary>Marks one keyed output profile whose first captured registration is authoritative.</summary>
internal sealed record OutputProfileRegistration
{
    /// <summary>Initializes a marker for one validated profile key.</summary>
    /// <param name="processorKey">The profile's stable processor key.</param>
    /// <exception cref="ArgumentException"><paramref name="processorKey"/> is default or otherwise uninitialized.</exception>
    public OutputProfileRegistration(ComponentKey<IOutputProcessor> processorKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processorKey.Value, nameof(processorKey));
        ProcessorKey = processorKey;
    }

    /// <summary>Gets the initialized profile key.</summary>
    public ComponentKey<IOutputProcessor> ProcessorKey { get; }
}
