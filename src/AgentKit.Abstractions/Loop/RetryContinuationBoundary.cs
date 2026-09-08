// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures an open retry boundary without fabricating a completed assistant response or turn.</summary>
/// <remarks>The retained identities tie a later retry or repair to the original attempt. This boundary is not evidence that the provider operation completed safely or may be repeated without revalidation.</remarks>
public sealed record RetryContinuationBoundary: RunContinuationBoundary
{
    /// <summary>Initializes a boundary for an existing retryable or repairable request.</summary>
    /// <param name="turnId">The non-default identity of the existing turn retained by the retry.</param>
    /// <param name="modelRequestId">The non-default identity of the existing model request being retried or repaired.</param>
    /// <exception cref="ArgumentOutOfRangeException">Either identity is default.</exception>
    public RetryContinuationBoundary(TurnId turnId, ModelRequestId modelRequestId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(turnId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(modelRequestId, default);
        TurnId = turnId;
        ModelRequestId = modelRequestId;
    }

    /// <summary>Gets the identity of the existing turn retained by the retry.</summary>
    /// <value>A non-default turn identity; it does not imply the turn completed.</value>
    public TurnId TurnId { get; }

    /// <summary>Gets the identity of the existing model request being retried or repaired.</summary>
    /// <value>A non-default request identity used to prevent a retry from being applied to another request.</value>
    public ModelRequestId ModelRequestId { get; }
}
