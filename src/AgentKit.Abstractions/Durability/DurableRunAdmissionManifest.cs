// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The journaled manifest of one run's admission into the engine.</summary>
/// <remarks>
/// <para>
/// The manifest identifies the accepted run, its session, and the admission whose input opened it. The input itself
/// is already durable session truth and is deliberately absent: an admission record that carried the user's message
/// would make the recovery journal a second copy of the conversation.
/// </para>
/// <para>
/// This type is an immutable value object with structural equality over its fields. It carries no mutable state and
/// is safe to share across threads without synchronization.
/// </para>
/// </remarks>
public sealed record DurableRunAdmissionManifest
{
    /// <summary>Initializes one complete run-admission manifest.</summary>
    /// <param name="runId">The nonempty identity of the accepted run.</param>
    /// <param name="sessionId">The nonempty identity of the session the run was accepted on.</param>
    /// <param name="admissionId">The nonempty identity of the admission whose input opened the run.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="runId"/>, <paramref name="sessionId"/>, or <paramref name="admissionId"/> is the empty
    /// identity.
    /// </exception>
    public DurableRunAdmissionManifest(Guid runId, Guid sessionId, Guid admissionId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(runId, Guid.Empty, nameof(runId));
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, Guid.Empty, nameof(sessionId));
        ArgumentOutOfRangeException.ThrowIfEqual(admissionId, Guid.Empty, nameof(admissionId));
        RunId = runId;
        SessionId = sessionId;
        AdmissionId = admissionId;
    }

    /// <summary>Gets the accepted run.</summary>
    /// <value>The nonempty run identity, matching the durable address this operation is journaled under.</value>
    public Guid RunId { get; }

    /// <summary>Gets the session the run was accepted on.</summary>
    /// <value>The nonempty session identity whose lane the run holds until settlement.</value>
    public Guid SessionId { get; }

    /// <summary>Gets the admission whose input opened the run.</summary>
    /// <value>
    /// The nonempty admission identity. It is recorded because the session's accepted-run state is keyed by it, so
    /// recovery can correlate the durable operation with the accepted run rather than inferring the pairing.
    /// </value>
    public Guid AdmissionId { get; }
}
