// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

/// <summary>Registration metadata for one compaction strategy.</summary>
public sealed record CompactionStrategyRegistration
{
    /// <summary>Initializes a new instance of the <see cref="CompactionStrategyRegistration"/> record.</summary>
    public CompactionStrategyRegistration(
        CompactionStrategyDescriptor descriptor,
        int order,
        ImmutableArray<CompactionStrategyKey> before,
        ImmutableArray<CompactionStrategyKey> after,
        ServiceLifetime lifetime)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentException.ThrowIfDefault(before);
        ArgumentException.ThrowIfDefault(after);
        Descriptor = descriptor;
        Order = order;
        Before = before;
        After = after;
        Lifetime = lifetime;
    }

    /// <summary>Gets the strategy descriptor.</summary>
    public CompactionStrategyDescriptor Descriptor { get; }

    /// <summary>Gets the registration order.</summary>
    public int Order { get; }

    /// <summary>Gets keys that must run before this strategy.</summary>
    public ImmutableArray<CompactionStrategyKey> Before { get; }

    /// <summary>Gets keys that must run after this strategy.</summary>
    public ImmutableArray<CompactionStrategyKey> After { get; }

    /// <summary>Gets the DI lifetime.</summary>
    public ServiceLifetime Lifetime { get; }
}
