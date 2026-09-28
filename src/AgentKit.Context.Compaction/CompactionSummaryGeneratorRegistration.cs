// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

/// <summary>Registration metadata for one summary generator.</summary>
public sealed record CompactionSummaryGeneratorRegistration
{
    /// <summary>Initializes a new instance of the <see cref="CompactionSummaryGeneratorRegistration"/> record.</summary>
    public CompactionSummaryGeneratorRegistration(
        CompactionSummaryGeneratorDescriptor descriptor,
        ServiceLifetime lifetime)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        Descriptor = descriptor;
        Lifetime = lifetime;
    }

    /// <summary>Gets the generator descriptor.</summary>
    public CompactionSummaryGeneratorDescriptor Descriptor { get; }

    /// <summary>Gets the DI lifetime.</summary>
    public ServiceLifetime Lifetime { get; }
}
