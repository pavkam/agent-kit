// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures an open retry boundary without fabricating a completed assistant response.</summary>
public sealed record RetryContinuationBoundary: RunContinuationBoundary
{
    /// <summary>Initializes a retry boundary.</summary>
    /// <param name="turnId">The existing turn identity retained by the retry.</param>
    /// <param name="modelRequestId">The existing model request being retried or repaired.</param>
    /// <exception cref="ArgumentOutOfRangeException">Either identity is default.</exception>
    public RetryContinuationBoundary(TurnId turnId, ModelRequestId modelRequestId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(turnId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(modelRequestId, default);
        TurnId = turnId;
        ModelRequestId = modelRequestId;
    }

    /// <summary>Gets the retained turn identity.</summary>
    public TurnId TurnId { get; }

    /// <summary>Gets the retained request identity.</summary>
    public ModelRequestId ModelRequestId { get; }
}
