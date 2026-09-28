// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Immutable advertisement of one keyed durable backend registration.</summary>
public sealed record DurableBackendDescriptor
{
    /// <summary>Initializes one backend descriptor.</summary>
    /// <param name="key">The stable backend identity.</param>
    /// <param name="capabilities">Supported capability claims.</param>
    /// <param name="supportedOperations">Operation names the backend can own.</param>
    /// <param name="supportsFencing">Whether writes require monotonic fencing tokens.</param>
    /// <param name="supportsReconciliation">Whether unknown effects can be reconciled through this backend.</param>
    /// <exception cref="ArgumentException"><paramref name="key"/> is blank.</exception>
    public DurableBackendDescriptor(
        DurableBackendKey key,
        DurableBackendCapabilities capabilities,
        ImmutableArray<DurableOperationName> supportedOperations,
        bool supportsFencing,
        bool supportsReconciliation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        Key = key;
        Capabilities = capabilities;
        SupportedOperations = supportedOperations;
        SupportsFencing = supportsFencing;
        SupportsReconciliation = supportsReconciliation;
    }

    /// <summary>Gets the backend identity.</summary>
    public DurableBackendKey Key { get; init; }

    /// <summary>Gets the capability claims.</summary>
    public DurableBackendCapabilities Capabilities { get; init; }

    /// <summary>Gets supported recoverable operation names.</summary>
    public ImmutableArray<DurableOperationName> SupportedOperations { get; init; }

    /// <summary>Gets whether durable writes require fencing tokens.</summary>
    public bool SupportsFencing { get; init; }

    /// <summary>Gets whether reconciliation is available for unknown effects.</summary>
    public bool SupportsReconciliation { get; init; }
}
