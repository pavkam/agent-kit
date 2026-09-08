// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports durable acceptance of input or an equivalent idempotent replay of that acceptance.</summary>
/// <remarks>The receipt is evidence of the one accepted admission; it does not imply that the queued input has been promoted into a run.</remarks>
public sealed record AcceptedInput: InputAdmissionResult
{
    /// <summary>Initializes an accepted admission result.</summary>
    /// <param name="receipt">The non-null durable receipt for the newly accepted input or the matching existing admission.</param>
    public AcceptedInput(AdmissionReceipt receipt) { ArgumentNullException.ThrowIfNull(receipt); Receipt = receipt; }
    /// <summary>Gets the receipt identifying the durable admission.</summary>
    /// <value>A non-null receipt that records the admitted sequence, resolved lane, and whether the result came from an equivalent replay.</value>
    public AdmissionReceipt Receipt { get; }
}
