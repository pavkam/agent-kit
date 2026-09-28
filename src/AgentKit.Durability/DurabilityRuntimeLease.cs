// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Owns the components activated for one durable execution or recovery attempt.</summary>
/// <remarks>
/// The lease borrows container-owned components for the lifetime of one attempt. Disposal releases the activation
/// scope, which releases scoped components only; it never revokes a fencing token, cancels a handed-off backend
/// operation, or undoes an already completed external effect. Disposal is idempotent and must not race the attempt
/// that borrowed the components.
/// </remarks>
internal sealed class DurabilityRuntimeLease: IDurabilityRuntimeLease
{
    private readonly IServiceScope _scope;
    private bool _disposed;

    /// <summary>Captures one activated durability runtime and the scope that owns its scoped components.</summary>
    /// <param name="scope">The non-null activation scope released on disposal.</param>
    /// <param name="context">The non-null captured durability composition this activation matched.</param>
    /// <param name="backend">The non-null selected durable execution backend.</param>
    /// <param name="journal">The non-null selected durable operation journal.</param>
    /// <param name="leaseManager">The non-null selected durable lease manager.</param>
    /// <param name="recoveryPolicy">The non-null selected recovery policy.</param>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public DurabilityRuntimeLease(
        IServiceScope scope,
        DurableExecutionContext context,
        IDurableExecutionBackend backend,
        IDurableOperationJournal journal,
        IDurableLeaseManager leaseManager,
        IRecoveryPolicy recoveryPolicy)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentNullException.ThrowIfNull(journal);
        ArgumentNullException.ThrowIfNull(leaseManager);
        ArgumentNullException.ThrowIfNull(recoveryPolicy);
        _scope = scope;
        Context = context;
        Backend = backend;
        Journal = journal;
        LeaseManager = leaseManager;
        RecoveryPolicy = recoveryPolicy;
    }

    /// <inheritdoc/>
    public DurableExecutionContext Context { get; }

    /// <inheritdoc/>
    public IDurableExecutionBackend Backend { get; }

    /// <inheritdoc/>
    public IDurableOperationJournal Journal { get; }

    /// <inheritdoc/>
    public IDurableLeaseManager LeaseManager { get; }

    /// <inheritdoc/>
    public IRecoveryPolicy RecoveryPolicy { get; }

    /// <inheritdoc/>
    /// <remarks>Repeated disposal is harmless. Releasing the scope never revokes fencing or external effects.</remarks>
    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        return _scope is IAsyncDisposable asyncScope
            ? asyncScope.DisposeAsync()
            : DisposeSynchronously();
    }

    private ValueTask DisposeSynchronously()
    {
        _scope.Dispose();
        return ValueTask.CompletedTask;
    }
}
