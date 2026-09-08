// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Records one canonical admitted input and its retained before-run correlation.</summary>
public sealed record InputAdmittedSessionEntry: SessionEntry
{
    /// <summary>Initializes an admitted-input session fact.</summary><param name="id">The stable entry ID.</param><param name="address">The owning session.</param><param name="correlation">The original before-run correlation.</param><param name="branchId">The selected branch.</param><param name="sequence">The durable admission sequence.</param><param name="causalParentId">The prior branch tip.</param><param name="recordedAt">The commit timestamp.</param><param name="schemaVersion">The entry schema.</param><param name="input">The complete admitted input.</param><exception cref="ArgumentNullException"><paramref name="correlation"/> or <paramref name="input"/> is null.</exception><exception cref="ArgumentException">The correlation is not before-run or input address/sequence differs.</exception>
    public InputAdmittedSessionEntry(SessionEntryId id, SessionAddress address, BeforeRunOperationCorrelation correlation,
        BranchId branchId, SessionSequence sequence, SessionEntryId? causalParentId, DateTimeOffset recordedAt,
        SchemaVersion schemaVersion, AdmittedInput input)
        : base(id, address, correlation, branchId, sequence, causalParentId, recordedAt, schemaVersion)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNotEqual(input.AgentId, address.AgentId, nameof(input));
        ArgumentException.ThrowIfNotEqual(input.SessionId, address.SessionId, nameof(input));
        ArgumentException.ThrowIfNotEqual(input.AdmittedSequence, sequence, nameof(input));
        Input = input;
    }
    /// <summary>Gets the complete admitted input.</summary><value>The immutable original/effective payload, identity, lane, order, and preprocessing evidence.</value>
    public AdmittedInput Input { get; }
}
