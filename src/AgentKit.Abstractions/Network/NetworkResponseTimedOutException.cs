// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Signals that response-body consumption exceeded the request's response deadline.</summary>
public sealed class NetworkResponseTimedOutException: IOException
{
    /// <summary>Initializes a response-deadline exception without an underlying transport exception.</summary>
    public NetworkResponseTimedOutException()
        : base("The response body exceeded its configured deadline.")
    {
    }

    /// <summary>Initializes a response-deadline exception caused by cancellation of an active read.</summary>
    /// <param name="innerException">The cancellation raised by the underlying stream.</param>
    /// <exception cref="ArgumentNullException"><paramref name="innerException"/> is null.</exception>
    public NetworkResponseTimedOutException(OperationCanceledException innerException)
        : base("The response body exceeded its configured deadline.", innerException)
        => ArgumentNullException.ThrowIfNull(innerException);
}
