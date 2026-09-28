// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.InMemory;

/// <summary>Owns durable operations in the calling process instead of handing them to an external durable runtime.</summary>
/// <remarks>
/// <para>
/// This backend exists so a fully local composition can run the durability runtime without an external workflow
/// service. It claims no distributed ownership, no external handoff, and no reconciliation, which is the honest
/// capability set for a process-local owner: there is no second system that could hold an effect this process cannot
/// see.
/// </para>
/// <para>
/// Because the descriptor advertises no external handoff, a correct coordinator never asks this backend to dispatch,
/// and <see cref="DispatchAsync"/> refuses if it is called anyway. Returning a success with an invented handle would be
/// worse than refusing: recovery treats a recorded external reference as proof that some other system may hold the
/// effect, and would route an unknown outcome into a reconciliation nobody can answer.
/// <see cref="ReconcileAsync"/> likewise always refuses, because an in-process backend cannot query an authority that
/// knows more about an unknown effect than the journal already records. An unknown effect under this backend escalates
/// to an operator rather than being optimistically retried.
/// </para>
/// <para>
/// Instances are immutable and safe for concurrent use.
/// </para>
/// </remarks>
public sealed class InMemoryDurableExecutionBackend: IDurableExecutionBackend
{
    /// <summary>Initializes a process-local backend under one exact key.</summary>
    /// <param name="key">The nonblank backend identity a durability profile uses to select this backend.</param>
    /// <param name="supportedOperations">
    /// The operation names this backend may own. An empty array means the backend places no restriction, which is
    /// appropriate for a local owner that performs no protocol-specific translation.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="key"/> carries no key text, or <paramref name="supportedOperations"/> contains a blank or duplicate name.</exception>
    public InMemoryDurableExecutionBackend(
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
    /// A descriptor claiming fencing but neither distributed ownership, external handoff, nor reconciliation.
    /// Fencing is claimed because the process-local lease manager does issue monotonic tokens that the journal
    /// enforces; the tokens are authoritative only inside this process.
    /// </value>
    public DurableBackendDescriptor Descriptor { get; }

    /// <inheritdoc/>
    /// <param name="request">The non-null dispatch request whose declaration is validated but never forwarded.</param>
    /// <param name="cancellationToken">Cancels before the refusal is produced.</param>
    /// <returns>
    /// Always a <see cref="DurableDispatchFailed"/> carrying content-free text, because this backend advertises no
    /// external handoff and has no external owner to hand the operation to.
    /// </returns>
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
                "The process-local durable backend owns operations in this process and performs no external handoff."));
    }

    /// <inheritdoc/>
    /// <param name="request">The non-null reconciliation request.</param>
    /// <param name="cancellationToken">Cancels before the refusal is produced.</param>
    /// <returns>
    /// Always a <see cref="DurableReconciliationFailed"/> carrying content-free text. Refusing is the fail-closed
    /// answer: claiming reconciliation this backend cannot perform would let an unknown effect be retried as though it
    /// had been proven absent.
    /// </returns>
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
                "The process-local durable backend cannot reconcile an external effect it never dispatched."));
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
                throw new ArgumentException(
                    "A supported durable operation name must carry name text.", paramName);
            }

            if (!seen.Add(operation.Value))
            {
                throw new ArgumentException(
                    "Supported durable operation names must be distinct.", paramName);
            }
        }
    }
}
