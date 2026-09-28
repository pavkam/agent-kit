// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Sqlite;

/// <summary>Owns durable operations on one host, coordinated through a shared local SQLite database.</summary>
/// <remarks>
/// <para>
/// This backend lets several processes on one machine run the durability runtime against one durable store without an
/// external workflow service. Its honest capability set is host-local multi-process exclusion with fencing: the lease
/// manager allocates monotonic tokens inside the database and the journal enforces them, so a restarted or second
/// process cannot write under a stale generation.
/// </para>
/// <para>
/// It claims neither distributed ownership nor external handoff nor reconciliation. A shared file is not a distributed
/// lease: two hosts pointed at separate databases exclude nothing, and a networked filesystem does not make SQLite's
/// locking a cross-host guarantee. Because no external handoff is advertised, a correct coordinator never asks this
/// backend to dispatch, and <see cref="DispatchAsync"/> refuses if it is called anyway. Returning an invented handle
/// would be worse than refusing: recovery treats a recorded external reference as proof that another system may hold
/// the effect. <see cref="ReconcileAsync"/> likewise always refuses, because no authority exists that knows more about
/// an unknown effect than the journal already records, so an unknown effect escalates to an operator rather than being
/// optimistically retried.
/// </para>
/// <para>
/// Instances are immutable and safe for concurrent use.
/// </para>
/// </remarks>
public sealed class SqliteDurableExecutionBackend: IDurableExecutionBackend
{
    /// <summary>Initializes a host-local backend under one exact key.</summary>
    /// <param name="key">The nonblank backend identity a durability profile uses to select this backend.</param>
    /// <param name="supportedOperations">The operation names this backend may own; an empty array places no restriction, which is appropriate for a local owner that performs no protocol-specific translation.</param>
    /// <exception cref="ArgumentException"><paramref name="key"/> carries no key text, or <paramref name="supportedOperations"/> contains a blank or duplicate name.</exception>
    public SqliteDurableExecutionBackend(
        DurableBackendKey key,
        ImmutableArray<DurableOperationName> supportedOperations = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        var operations = supportedOperations.IsDefault ? [] : supportedOperations;
        ThrowIfOperationsAreNotDistinct(operations, nameof(supportedOperations));
        Descriptor = new DurableBackendDescriptor(
            key,
            new DurableBackendCapabilities(
                SupportsDistributedOwnership: false,
                SupportsExternalHandoff: false,
                SupportsReconciliation: false),
            operations,
            supportsFencing: true,
            supportsReconciliation: false);
    }

    /// <inheritdoc/>
    /// <value>
    /// A descriptor claiming fencing but neither distributed ownership, external handoff, nor reconciliation. Fencing is
    /// claimed because the database allocates monotonic tokens that the journal enforces; the exclusion those tokens
    /// provide is host-local and covers only processes sharing this exact database file.
    /// </value>
    public DurableBackendDescriptor Descriptor { get; }

    /// <inheritdoc/>
    /// <param name="request">The non-null dispatch request whose declaration is validated but never forwarded.</param>
    /// <param name="cancellationToken">Cancels before the refusal is produced.</param>
    /// <returns>Always a <see cref="DurableDispatchFailed"/> carrying content-free text, because this backend advertises no external handoff.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was already cancelled.</exception>
    public ValueTask<DurableDispatchResult> DispatchAsync(
        DurableDispatchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<DurableDispatchResult>(
            new DurableDispatchFailed(
                "The host-local SQLite durable backend owns operations on this host and performs no external handoff."));
    }

    /// <inheritdoc/>
    /// <param name="request">The non-null reconciliation request.</param>
    /// <param name="cancellationToken">Cancels before the refusal is produced.</param>
    /// <returns>Always a <see cref="DurableReconciliationFailed"/> carrying content-free text, because claiming reconciliation this backend cannot perform would let an unknown effect be retried as though it had been proven absent.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was already cancelled.</exception>
    public ValueTask<DurableReconciliationResult> ReconcileAsync(
        DurableReconciliationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<DurableReconciliationResult>(
            new DurableReconciliationFailed(
                "The host-local SQLite durable backend cannot reconcile an external effect it never dispatched."));
    }

    private static void ThrowIfOperationsAreNotDistinct(
        ImmutableArray<DurableOperationName> operations,
        string paramName)
    {
        var seen = new HashSet<string>(operations.Length, StringComparer.Ordinal);
        foreach (var operation in operations)
        {
            if (string.IsNullOrWhiteSpace(operation.Value))
            {
                throw new ArgumentException("A supported durable operation name must carry name text.", paramName);
            }

            if (!seen.Add(operation.Value))
            {
                throw new ArgumentException("Supported durable operation names must be distinct.", paramName);
            }
        }
    }
}
