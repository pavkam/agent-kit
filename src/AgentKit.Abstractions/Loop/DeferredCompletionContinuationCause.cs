// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Indicates that a previously suspended deferred operation has resolved.</summary>
public sealed record DeferredCompletionContinuationCause: RunContinuationCause
{
    /// <summary>Initializes deferred-completion evidence.</summary>
    /// <param name="operationId">The resolved deferred operation.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="operationId"/> is default.</exception>
    public DeferredCompletionContinuationCause(OperationId operationId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(operationId, default);
        OperationId = operationId;
    }

    /// <summary>Gets the resolved deferred operation identity.</summary>
    public OperationId OperationId { get; }
}
