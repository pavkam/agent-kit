// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Returns the original durable admission for an equivalent authorized replay.</summary>
public sealed record SessionInputReplayFound: SessionInputLookupResult
{
    /// <summary>Initializes a replay result.</summary><param name="admittedInput">The retained admitted input.</param><param name="correlation">The retained before-run correlation.</param><param name="receipt">The original durable receipt marked as existing.</param>
    public SessionInputReplayFound(AdmittedInput admittedInput, BeforeRunOperationCorrelation correlation, AdmissionReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(admittedInput);
        ArgumentNullException.ThrowIfNull(correlation);
        ArgumentNullException.ThrowIfNull(receipt);
        AdmittedInput = admittedInput;
        Correlation = correlation;
        Receipt = receipt;
    }
    /// <summary>Gets the retained input including preprocessing evidence.</summary><value>The original durable admission.</value>
    public AdmittedInput AdmittedInput { get; }
    /// <summary>Gets the causal admission correlation.</summary><value>The original before-run operation correlation.</value>
    public BeforeRunOperationCorrelation Correlation { get; }
    /// <summary>Gets the replay receipt.</summary><value>The original admission identity and sequence with <c>Existing</c> true.</value>
    public AdmissionReceipt Receipt { get; }
}
