// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports typed pre-admission rejection without fabricating durable identities.</summary>
public sealed record RejectedInput: InputAdmissionResult
{
    /// <summary>Initializes rejection.</summary><param name="rejection">The nonnull typed failure.</param>
    public RejectedInput(InputRejection rejection) { ArgumentNullException.ThrowIfNull(rejection); Rejection = rejection; }
    /// <summary>Gets rejection.</summary><value>The typed content-free failure.</value>
    public InputRejection Rejection { get; }
}
