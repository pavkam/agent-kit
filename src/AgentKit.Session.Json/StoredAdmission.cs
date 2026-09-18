// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json;

/// <summary>Retains one canonical admission and its causal session entry under the store gate.</summary>
internal sealed class StoredAdmission
{
    /// <summary>Initializes retained admission state.</summary><param name="input">The complete admitted input.</param><param name="correlation">The original before-run correlation.</param><param name="entryId">The durable admission entry.</param><param name="receipt">The original receipt.</param>
    public StoredAdmission(AdmittedInput input, BeforeRunOperationCorrelation correlation, SessionEntryId entryId, AdmissionReceipt receipt)
    {
        Input = input; Correlation = correlation; EntryId = entryId; Receipt = receipt;
    }
    /// <summary>Gets or sets the immutable admission snapshot as promotion advances it.</summary>
    public AdmittedInput Input { get; set; }
    /// <summary>Gets the retained admission correlation.</summary>
    public BeforeRunOperationCorrelation Correlation { get; }
    /// <summary>Gets the durable admission entry identity.</summary>
    public SessionEntryId EntryId { get; }
    /// <summary>Gets the original admission receipt.</summary>
    public AdmissionReceipt Receipt { get; }
}
