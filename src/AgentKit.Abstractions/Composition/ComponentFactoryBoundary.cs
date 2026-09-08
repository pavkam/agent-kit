// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Documents the separately scoped operation created by a component-owned factory.</summary>
/// <remarks>A boundary permits lifetime isolation only after composition validation proves that the operation root is acyclic and cannot re-enter its owner. The owner is responsible for the operation and the disposal contract identifies the disposable operation value it must complete and release.</remarks>
public sealed record ComponentFactoryBoundary
{
    /// <summary>Initializes the evidence required to validate one owned operation scope.</summary>
    /// <param name="owner">The component that owns and disposes the operation scope.</param>
    /// <param name="operationRoot">The singular registration resolved at the operation scope root.</param>
    /// <param name="disposalContractType">The closed <see cref="IDisposable"/> or <see cref="IAsyncDisposable"/> contract implemented by the operation root.</param>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> or <paramref name="operationRoot"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="disposalContractType"/> is open or is not a supported disposal contract.</exception>
    public ComponentFactoryBoundary(ComponentContractReference owner, ComponentContractReference operationRoot, Type disposalContractType)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(operationRoot);
        ArgumentException.ThrowIfNotDisposalContract(disposalContractType);

        Owner = owner;
        OperationRoot = operationRoot;
        DisposalContractType = disposalContractType;
    }

    /// <summary>Gets the component that owns the operation lifetime.</summary>
    public ComponentContractReference Owner { get; }

    /// <summary>Gets the registration resolved as the operation's root.</summary>
    public ComponentContractReference OperationRoot { get; }

    /// <summary>Gets the disposal contract the operation root must implement.</summary>
    public Type DisposalContractType { get; }
}
