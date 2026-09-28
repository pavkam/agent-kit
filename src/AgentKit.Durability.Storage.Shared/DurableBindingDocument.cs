// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Storage;

/// <summary>Portable persisted mirror of <see cref="DurableOperationBinding"/>, the inseparable address and captured context.</summary>
/// <remarks>
/// The binding is persisted as one unit because it is the only replacement unit the domain accepts. Reconstruction runs
/// the domain constructor, which revalidates that the persisted authorization scope still describes the persisted
/// address, so a record whose coordinates and authorization were edited apart fails on read rather than being replayed
/// as another agent's, session's, or run's operation.
/// </remarks>
/// <param name="Address">The non-null persisted durable coordinates.</param>
/// <param name="ExecutionContext">The non-null persisted durability composition and authorization evidence.</param>
internal sealed record DurableBindingDocument(
    DurableAddressDocument Address,
    DurableExecutionContextDocument ExecutionContext)
{
    /// <summary>Projects one domain binding into its portable persisted representation.</summary>
    /// <param name="value">The non-null binding to project.</param>
    /// <returns>A document carrying the projected address and context.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    internal static DurableBindingDocument FromDomain(DurableOperationBinding value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new DurableBindingDocument(
            DurableAddressDocument.FromDomain(value.Address),
            DurableExecutionContextDocument.FromDomain(value.ExecutionContext));
    }

    /// <summary>Reconstructs the exact domain binding this document was projected from.</summary>
    /// <returns>A binding equal to the projected original.</returns>
    /// <exception cref="ArgumentNullException"><see cref="Address"/> or <see cref="ExecutionContext"/> is null, which a well-formed document never is.</exception>
    /// <exception cref="ArgumentException">The persisted authorization scope cannot describe the persisted address.</exception>
    internal DurableOperationBinding ToDomain()
    {
        ArgumentNullException.ThrowIfNull(Address);
        ArgumentNullException.ThrowIfNull(ExecutionContext);
        return new DurableOperationBinding(Address.ToDomain(), ExecutionContext.ToDomain());
    }
}
