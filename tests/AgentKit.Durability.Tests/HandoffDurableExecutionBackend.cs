// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Tests;

/// <summary>A backend that claims external handoff so the dispatch and reconciliation stages can be exercised.</summary>
/// <remarks>
/// The process-local backend deliberately advertises no handoff, so it cannot reach those stages. This double claims
/// both capabilities and answers with whatever the test scripted.
/// </remarks>
internal sealed class HandoffDurableExecutionBackend: IDurableExecutionBackend
{
    private readonly ExternalOperationReference? _reference;
    private readonly bool _refuse;
    private readonly SideEffectCertainty? _reconciledCertainty;

    /// <summary>Initializes a handoff-capable backend.</summary>
    /// <param name="key">The nonblank backend identity.</param>
    /// <param name="reference">The reference a successful handoff returns, or null when none is scripted.</param>
    /// <param name="refuse">True to refuse every handoff attempt.</param>
    /// <param name="reconciledCertainty">The certainty reconciliation establishes, or null to refuse reconciliation.</param>
    internal HandoffDurableExecutionBackend(
        DurableBackendKey key,
        ExternalOperationReference? reference = null,
        bool refuse = false,
        SideEffectCertainty? reconciledCertainty = null)
    {
        _reference = reference;
        _refuse = refuse;
        _reconciledCertainty = reconciledCertainty;
        Descriptor = new DurableBackendDescriptor(
            key,
            new DurableBackendCapabilities(
                SupportsDistributedOwnership: true,
                SupportsExternalHandoff: true,
                SupportsReconciliation: reconciledCertainty is not null),
            [],
            supportsFencing: true,
            supportsReconciliation: reconciledCertainty is not null);
    }

    /// <inheritdoc/>
    public DurableBackendDescriptor Descriptor { get; }

    /// <inheritdoc/>
    public ValueTask<DurableDispatchResult> DispatchAsync(
        DurableDispatchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<DurableDispatchResult>(_refuse || _reference is null
            ? new DurableDispatchFailed("The test backend refused the handoff.")
            : new DurableDispatched(_reference));
    }

    /// <inheritdoc/>
    public ValueTask<DurableReconciliationResult> ReconcileAsync(
        DurableReconciliationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<DurableReconciliationResult>(_reconciledCertainty is { } certainty
            ? new DurableReconciled(certainty)
            : new DurableReconciliationFailed("The test backend cannot reconcile."));
    }
}
