// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>An audit dispatcher that records delivered records and can simulate unavailable required audit.</summary>
public sealed class RecordingAuditDispatcher: ISecurityAuditDispatcher
{
    private readonly Lock _gate = new();
    private readonly List<SecurityAuditRecord> _records = [];

    /// <summary>Gets or sets the result returned for every dispatch.</summary>
    /// <value>The result. The default is <see cref="SecurityAuditAccepted"/>.</value>
    public SecurityAuditDispatchResult Result { get; set; } = new SecurityAuditAccepted();

    /// <summary>Gets a snapshot of every record dispatched, in order.</summary>
    public IReadOnlyList<SecurityAuditRecord> Records
    {
        get
        {
            lock (_gate)
            {
                return [.. _records];
            }
        }
    }

    /// <inheritdoc/>
    public ValueTask<SecurityAuditDispatchResult> DispatchAsync(
        SecurityAuditRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        lock (_gate)
        {
            _records.Add(record);
        }

        return ValueTask.FromResult(Result);
    }
}
