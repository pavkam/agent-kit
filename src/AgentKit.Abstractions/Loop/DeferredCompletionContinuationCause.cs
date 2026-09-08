// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Supplies evidence that a previously suspended deferred operation resolved and may require another request.</summary>
/// <remarks>The resolved operation is identified for correlation only; this cause does not itself advance the suspended run.</remarks>
public sealed record DeferredCompletionContinuationCause: RunContinuationCause
{
    /// <summary>Initializes evidence for one resolved deferred operation.</summary>
    /// <param name="operationId">The non-default identity of the deferred operation that resolved.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="operationId"/> is default.</exception>
    public DeferredCompletionContinuationCause(OperationId operationId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(operationId, default);
        OperationId = operationId;
    }

    /// <summary>Gets the identity of the deferred operation that resolved.</summary>
    /// <value>A non-default operation identity retained for causal revalidation.</value>
    public OperationId OperationId { get; }
}
