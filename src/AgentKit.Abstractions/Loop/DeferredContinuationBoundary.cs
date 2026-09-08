// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures an open deferred operation without claiming that its provider response completed.</summary>
public sealed record DeferredContinuationBoundary: RunContinuationBoundary
{
    /// <summary>Initializes a deferred boundary.</summary>
    /// <param name="turnId">The retained turn identity.</param>
    /// <param name="modelRequestId">The retained request identity.</param>
    /// <param name="deferredOperationId">The deferred operation that must resolve.</param>
    /// <exception cref="ArgumentOutOfRangeException">Any identity is default.</exception>
    public DeferredContinuationBoundary(TurnId turnId, ModelRequestId modelRequestId, OperationId deferredOperationId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(turnId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(modelRequestId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(deferredOperationId, default);
        TurnId = turnId;
        ModelRequestId = modelRequestId;
        DeferredOperationId = deferredOperationId;
    }

    /// <summary>Gets the retained turn identity.</summary>
    public TurnId TurnId { get; }
    /// <summary>Gets the retained request identity.</summary>
    public ModelRequestId ModelRequestId { get; }
    /// <summary>Gets the deferred operation identity.</summary>
    public OperationId DeferredOperationId { get; }
}
