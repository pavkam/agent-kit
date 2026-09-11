// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Reports a typed local subscription admission failure without claiming run rejection or cancellation.</summary>
internal sealed class RunEventSubscriptionRejectedException: InvalidOperationException
{
    /// <summary>Creates a failure retaining the bounded admission reason.</summary>
    /// <param name="reason">The defined admission rejection.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="reason"/> is undefined.</exception>
    internal RunEventSubscriptionRejectedException(RunEventSubscriptionRejection reason)
       : base("The run-event subscription could not be registered.")
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(reason);
        Reason = reason;
    }

    /// <summary>Gets the exact local admission failure.</summary>
    /// <value>A defined reason that carries no event content or run-state claim.</value>
    internal RunEventSubscriptionRejection Reason { get; }
}
