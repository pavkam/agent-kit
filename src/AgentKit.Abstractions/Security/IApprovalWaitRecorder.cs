// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Records durable evidence that an authorization was answered with a deferred approval, so a process that dies while
/// the approval is pending leaves evidence naming what would let the work resume.
/// </summary>
/// <remarks>
/// <para>
/// The recorder is deliberately a separate contract from <see cref="ISecurityAuthority"/>. A security authority
/// authorizes every durable journal write the durability coordinator makes, so an authority that itself called the
/// coordinator would make the coordinator and the authority construct each other and would let a deferred journal
/// write journal its own wait. The component that <em>observes</em> a <see cref="SecurityApprovalRequired"/> decision
/// calls the recorder instead; the authority never does.
/// </para>
/// <para>
/// Recording a wait is evidence, not authority. It grants, widens, and consumes nothing, and it never changes the
/// decision the caller already holds. Implementations therefore treat a durability gap as a logged condition rather
/// than a failure the caller must handle, and only cancellation propagates.
/// </para>
/// <para>
/// Implementations are engine-wide singletons and are safe for concurrent use.
/// </para>
/// </remarks>
public interface IApprovalWaitRecorder
{
    /// <summary>Records that <paramref name="request"/> is waiting on the pending <paramref name="approval"/>.</summary>
    /// <param name="request">
    /// The non-null security request whose decision was <see cref="SecurityApprovalRequired"/>. Its captured
    /// authorization addresses the durable operation; a request with none, or whose capture cannot be durably
    /// addressed, records nothing.
    /// </param>
    /// <param name="approval">The non-null pending approval the durable wait names as its external reference.</param>
    /// <param name="cancellationToken">Cancels the durable write and the local wait for it.</param>
    /// <returns>A task that completes once the wait is recorded, or immediately when nothing is journaled.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> or <paramref name="approval"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    public ValueTask RecordAsync(
        SecurityRequest request,
        ApprovalRequest approval,
        CancellationToken cancellationToken = default);
}
