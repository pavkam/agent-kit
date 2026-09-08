// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Applies the effective audit-delivery policy before a security-relevant transition proceeds.</summary>
/// <remarks>
/// Required delivery returns a typed unsuccessful result when no durable acceptance path exists, delivery fails, or
/// its finite deadline expires. Consumers must fail closed before protected access unless the result is
/// <see cref="SecurityAuditAccepted"/>. A <see cref="SecurityAuditTimedOut"/> result means durable acceptance is
/// unknown, not absent. Dispatchers check caller cancellation before each sink invocation and after each sink
/// completion; cancellation propagates even if a prior sink already durably accepted the record, because that
/// acceptance cannot be undone or reclassified.
/// </remarks>
public interface ISecurityAuditDispatcher
{
    /// <summary>Delivers one immutable redacted audit intent using the configured required or best-effort policy.</summary>
    /// <param name="record">The immutable redacted audit record to deliver.</param>
    /// <param name="cancellationToken">Cancels before required delivery is known to be accepted.</param>
    /// <returns>A typed accepted, unavailable, failed, or deadline-expired delivery result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="record"/> is null.</exception>
    public ValueTask<SecurityAuditDispatchResult> DispatchAsync(
        SecurityAuditRecord record,
        CancellationToken cancellationToken = default);
}
