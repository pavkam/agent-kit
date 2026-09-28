// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests one external durable handoff for a recoverable operation.</summary>
public sealed record DurableDispatchRequest
{
    /// <summary>Initializes a dispatch request.</summary>
    /// <param name="descriptor">The operation declaration.</param>
    /// <param name="executionContext">The captured durability composition.</param>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public DurableDispatchRequest(RecoverableOperationDescriptor descriptor, DurableExecutionContext executionContext)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(executionContext);
        Descriptor = descriptor;
        ExecutionContext = executionContext;
    }

    /// <summary>Gets the operation declaration.</summary>
    public RecoverableOperationDescriptor Descriptor { get; }

    /// <summary>Gets the captured durability composition.</summary>
    public DurableExecutionContext ExecutionContext { get; }
}
