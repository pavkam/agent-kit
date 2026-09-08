// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures an open deferred operation without claiming that its provider response or turn completed.</summary>
/// <remarks>The boundary preserves the existing turn, request, and deferred-operation identities so a later drive can continue causally without fabricating completed-turn evidence.</remarks>
public sealed record DeferredContinuationBoundary: RunContinuationBoundary
{
    /// <summary>Initializes a boundary for an open deferred operation.</summary>
    /// <param name="turnId">The non-default identity of the existing turn.</param>
    /// <param name="modelRequestId">The non-default identity of the existing model request.</param>
    /// <param name="deferredOperationId">The non-default identity of the operation whose resolution is awaited.</param>
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

    /// <summary>Gets the identity of the existing turn preserved while work is deferred.</summary>
    /// <value>A non-default turn identity; it is not evidence of a completed turn.</value>
    public TurnId TurnId { get; }
    /// <summary>Gets the identity of the existing model request preserved while work is deferred.</summary>
    /// <value>A non-default request identity; it is not evidence of a completed provider response.</value>
    public ModelRequestId ModelRequestId { get; }
    /// <summary>Gets the identity of the deferred operation whose resolution is awaited.</summary>
    /// <value>A non-default operation identity used to correlate a future resolution event.</value>
    public OperationId DeferredOperationId { get; }
}
