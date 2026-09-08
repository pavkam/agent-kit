// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports durable acceptance or an equivalent idempotent replay.</summary>
public sealed record AcceptedInput: InputAdmissionResult
{
    /// <summary>Initializes an accepted result.</summary><param name="receipt">The durable nonnull receipt.</param>
    public AcceptedInput(AdmissionReceipt receipt) { ArgumentNullException.ThrowIfNull(receipt); Receipt = receipt; }
    /// <summary>Gets admission receipt.</summary><value>The durable acceptance evidence.</value>
    public AdmissionReceipt Receipt { get; }
}
