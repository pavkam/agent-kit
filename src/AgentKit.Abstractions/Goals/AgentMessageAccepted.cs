// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that the recipient's input path durably admitted the message.</summary>
/// <remarks>Acceptance never starts a run or appends history, and it does not mean the recipient has read the message. A replay of the same message reports the original admission.</remarks>
public sealed record AgentMessageAccepted: AgentMessageResult
{
    /// <summary>Initializes an accepted outcome.</summary>
    /// <param name="receipt">The durable admission receipt.</param>
    /// <exception cref="ArgumentNullException"><paramref name="receipt"/> is null.</exception>
    public AgentMessageAccepted(AdmissionReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        Receipt = receipt;
    }

    /// <summary>Gets the durable admission receipt.</summary>
    public AdmissionReceipt Receipt { get; }
}
