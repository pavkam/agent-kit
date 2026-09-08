// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Indicates an explicit optional follow-up proposed by configured run policy.</summary>
public sealed record ExplicitPolicyContinuationCause: RunContinuationCause
{
    /// <summary>Initializes explicit policy evidence.</summary>
    /// <param name="reasonCode">A bounded stable code containing no user or model content.</param>
    /// <exception cref="ArgumentException"><paramref name="reasonCode"/> is null, empty, or whitespace.</exception>
    public ExplicitPolicyContinuationCause(string reasonCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        ReasonCode = reasonCode;
    }

    /// <summary>Gets the stable content-free reason code.</summary>
    public string ReasonCode { get; }
}
