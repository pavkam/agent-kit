// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Documents a separately scoped operation created by a component-owned factory.</summary>
/// <remarks>A boundary permits declared lifetime isolation only after composition validation proves that the operation root is acyclic and cannot re-enter its owner. The named owner is responsible for completing and releasing the operation value through the declared disposal boundary. This metadata does not execute the factory or prove that its implementation obeys the ownership declaration.</remarks>
public sealed record ComponentFactoryBoundary
{
    /// <summary>Initializes declared evidence for validating one owned operation scope.</summary>
    /// <param name="owner">The non-null component registration address that owns and must dispose the operation value.</param>
    /// <param name="operationRoot">The non-null singular registration address resolved at the operation scope root.</param>
    /// <param name="disposalContractType">The closed <see cref="IDisposable"/> or <see cref="IAsyncDisposable"/> contract the operation root declares it implements.</param>
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

    /// <summary>Gets the component registration address that owns the operation lifetime.</summary>
    /// <value>A non-null immutable address whose implementation is responsible for completing and disposing the operation value.</value>
    public ComponentContractReference Owner { get; }

    /// <summary>Gets the singular registration address resolved as the operation root.</summary>
    /// <value>A non-null immutable address that may not re-enter <see cref="Owner"/> through its declared graph.</value>
    public ComponentContractReference OperationRoot { get; }

    /// <summary>Gets the disposal contract declared for the operation root.</summary>
    /// <value>A closed <see cref="IDisposable"/> or <see cref="IAsyncDisposable"/> contract defining how the owner must release the operation value.</value>
    public Type DisposalContractType { get; }
}
