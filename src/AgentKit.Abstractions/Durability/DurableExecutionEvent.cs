// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Base type for immutable durable execution observation events.</summary>
public abstract record DurableExecutionEvent
{
    /// <summary>Initializes a durable execution event.</summary>
    /// <param name="binding">The operation binding the event describes.</param>
    /// <param name="occurredAt">When the event occurred.</param>
    /// <exception cref="ArgumentNullException"><paramref name="binding"/> is null.</exception>
    protected DurableExecutionEvent(DurableOperationBinding binding, DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(binding);
        Binding = binding;
        OccurredAt = occurredAt;
    }

    /// <summary>Gets the operation binding.</summary>
    public DurableOperationBinding Binding { get; }

    /// <summary>Gets when the event occurred.</summary>
    public DateTimeOffset OccurredAt { get; }
}
