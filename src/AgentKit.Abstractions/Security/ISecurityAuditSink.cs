// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Receives immutable redacted security-audit records through additive host registrations.</summary>
/// <remarks>A sink observes records only. It does not issue authority, consume grants, or decide whether an effect may proceed; the singular dispatcher applies the configured delivery policy.</remarks>
public interface ISecurityAuditSink
{
    /// <summary>Persists or forwards one already-redacted audit record.</summary>
    /// <param name="record">The immutable record with no raw protected content.</param>
    /// <param name="cancellationToken">Cancels delivery before the sink's durable acceptance point.</param>
    /// <returns>A task that completes when this sink accepted the record or faults when it could not.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="record"/> is null.</exception>
    public ValueTask WriteAsync(SecurityAuditRecord record, CancellationToken cancellationToken = default);
}
