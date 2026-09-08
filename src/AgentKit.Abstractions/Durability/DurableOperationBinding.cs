// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Immutably binds one durable address to the complete authorization and durability capture that owns it.</summary>
/// <remarks>The binding is the sole replacement unit for durable coordinates. It prevents a record copy from pairing an address with authorization captured for another agent, session, operation, run, or turn. It supports exact in-run work and after-run follow-up work whose address retains the causal run with no turn; sessionless before-run work requires a future address shape.</remarks>
public sealed record DurableOperationBinding
{
    /// <summary>Initializes an exact durable binding.</summary>
    /// <param name="address">The non-null durable operation coordinates.</param>
    /// <param name="executionContext">The non-null full captured durability and authorization context.</param>
    /// <exception cref="ArgumentNullException"><paramref name="address"/> or <paramref name="executionContext"/> is null.</exception>
    /// <exception cref="ArgumentException">The captured authorization scope cannot describe the address's agent, session, operation, run, or turn.</exception>
    public DurableOperationBinding(DurableOperationAddress address, DurableExecutionContext executionContext)
    {
        ArgumentException.ThrowIfInvalidDurableOperationBinding(address, executionContext);
        Address = address;
        ExecutionContext = executionContext;
    }

    /// <summary>Gets the exact durable coordinates retained by this binding.</summary>
    /// <value>The address that was validated against <see cref="ExecutionContext"/> and cannot be replaced independently.</value>
    public DurableOperationAddress Address { get; }

    /// <summary>Gets the complete captured durability and authorization context retained by this binding.</summary>
    /// <value>The context validated against <see cref="Address"/> and cannot be replaced independently.</value>
    public DurableExecutionContext ExecutionContext { get; }
}
