// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Supplies a configured policy's explicit optional reason to continue an otherwise idle run.</summary>
/// <remarks>The bounded code is diagnostic and policy evidence only. It must remain content-free and cannot override a required stop, input promotion, or another higher-precedence cause.</remarks>
public sealed record ExplicitPolicyContinuationCause: RunContinuationCause
{
    /// <summary>Initializes explicit optional continuation evidence.</summary>
    /// <param name="reasonCode">A non-null, non-whitespace stable code containing no user, model, tool, or other protected content.</param>
    /// <exception cref="ArgumentException"><paramref name="reasonCode"/> is null, empty, or whitespace.</exception>
    public ExplicitPolicyContinuationCause(string reasonCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        ReasonCode = reasonCode;
    }

    /// <summary>Gets the stable content-free reason code supplied by policy configuration.</summary>
    /// <value>A non-empty code suitable for bounded diagnostics and policy correlation, not for reconstructing user-visible content.</value>
    public string ReasonCode { get; }
}
