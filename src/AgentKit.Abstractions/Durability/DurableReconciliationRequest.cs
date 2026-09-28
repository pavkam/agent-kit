// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests reconciliation of one operation's unknown external effect state.</summary>
public sealed record DurableReconciliationRequest
{
    /// <summary>Initializes a reconciliation request.</summary>
    /// <param name="descriptor">The operation declaration.</param>
    /// <param name="evidence">The durable evidence to reconcile against.</param>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public DurableReconciliationRequest(RecoverableOperationDescriptor descriptor, RecoveryEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(evidence);
        Descriptor = descriptor;
        Evidence = evidence;
    }

    /// <summary>Gets the operation declaration.</summary>
    public RecoverableOperationDescriptor Descriptor { get; }

    /// <summary>Gets the evidence assembled by the journal.</summary>
    public RecoveryEvidence Evidence { get; }
}
