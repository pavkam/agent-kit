// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The immutable base for the terminal outcome of one <see cref="NetworkRequest"/>.</summary>
/// <remarks>
/// This is a closed discriminated hierarchy. The concrete kinds are
/// <see cref="NetworkResponseReceived"/>, <see cref="NetworkDenied"/>,
/// <see cref="NetworkRequestFailed"/>,
/// <see cref="NetworkResponseLimitExceeded"/>,
/// <see cref="NetworkRedirectLimitExceeded"/>,
/// <see cref="NetworkRedirectReceived"/>, and
/// <see cref="NetworkCancelled"/>. Its constructor is
/// <see langword="private protected"/>, so no assembly outside
/// AgentKit.Abstractions can add a seventh kind.
/// </remarks>
public abstract record NetworkSendResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NetworkSendResult"/>
    /// record. This constructor is <see langword="private protected"/> so
    /// only the closed set of kinds declared in this assembly can extend
    /// the hierarchy.
    /// </summary>
    private protected NetworkSendResult()
    {
    }
}
