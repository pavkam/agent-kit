// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json;

/// <summary>One newline-delimited authoritative session transition appended to the store record log.</summary>
/// <remarks>
/// <para>
/// The log is a command log rather than a state dump. A record retains the exact immutable request the store accepted plus
/// the values the store itself produced for that commit: a generated <see cref="NewBranchId"/> and a clock-derived
/// <see cref="CommittedAt"/>. Replay re-executes the same deterministic commit path against the rebuilt projection, so
/// session versions, per-lane cursors and revisions, installed accepted run state, and every idempotency receipt are
/// reproduced byte-for-byte as they were before process loss. Persisting the request rather than a diff also means the
/// idempotency caches keep the exact evidence a retry must be compared against.
/// </para>
/// <para>
/// A single record is the store's atomicity unit: the whole batch an operation commits is described by one flushed line, so
/// recovery never observes half of an append, half of an admission, or a promoted input without its accepted run state.
/// </para>
/// <para>
/// Members not applicable to a record's <paramref name="Kind"/> are null and are omitted from the encoded line under the
/// canonical contract. Replay validates applicability rather than trusting the writer, so a hand-edited or truncated log is
/// rejected instead of silently producing a partial projection.
/// </para>
/// </remarks>
/// <param name="Kind">The discriminator selecting which remaining members are meaningful.</param>
/// <param name="Create">The accepted creation request, present only for <see cref="JsonSessionStoreLogRecordKind.SessionCreated"/>.</param>
/// <param name="Provision">The accepted lane-provisioning request, present only for <see cref="JsonSessionStoreLogRecordKind.LaneProvisioned"/>.</param>
/// <param name="Append">The accepted conditional append, present only for <see cref="JsonSessionStoreLogRecordKind.EntriesAppended"/>.</param>
/// <param name="Branch">The accepted fork request, present only for <see cref="JsonSessionStoreLogRecordKind.BranchCreated"/>.</param>
/// <param name="Delete">The accepted deletion request, present only for <see cref="JsonSessionStoreLogRecordKind.SessionDeleted"/>.</param>
/// <param name="Admit">The accepted admission request, present only for <see cref="JsonSessionStoreLogRecordKind.InputAdmitted"/>.</param>
/// <param name="Start">The accepted run-start request, present only for <see cref="JsonSessionStoreLogRecordKind.RunAccepted"/>.</param>
/// <param name="Release">The accepted release request, present only for <see cref="JsonSessionStoreLogRecordKind.RunReleased"/>.</param>
/// <param name="Promote">The accepted mid-run promotion request, present only for <see cref="JsonSessionStoreLogRecordKind.InputPromoted"/>.</param>
/// <param name="NewBranchId">The branch identity the store generated for this commit, present only for session creation and branch creation.</param>
/// <param name="CommittedAt">The clock-derived instant the store stamped on this commit, present only for transitions that read the clock rather than a caller-supplied timestamp.</param>
public sealed record JsonSessionStoreLogRecord(
    JsonSessionStoreLogRecordKind Kind,
    SessionStoreCreateRequest? Create,
    SessionExecutionLaneProvisionRequest? Provision,
    SessionAppendRequest? Append,
    SessionBranchRequest? Branch,
    SessionDeleteRequest? Delete,
    SessionInputAdmissionRequest? Admit,
    SessionRunStartRequest? Start,
    SessionRunReleaseRequest? Release,
    SessionInputPromotionRequest? Promote,
    Guid? NewBranchId,
    DateTimeOffset? CommittedAt)
{
    /// <summary>Creates the record describing one session entering the store.</summary>
    /// <param name="request">The accepted creation request carrying the already allocated address.</param>
    /// <param name="branchId">The generated identity of the session's initial active branch.</param>
    /// <param name="createdAt">The clock-derived creation instant.</param>
    /// <returns>A creation record carrying every value replay needs to reproduce the session descriptor.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static JsonSessionStoreLogRecord ForCreate(
        SessionStoreCreateRequest request, BranchId branchId, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new JsonSessionStoreLogRecord(
            JsonSessionStoreLogRecordKind.SessionCreated, request, null, null, null, null, null, null, null, null,
            branchId.Value, createdAt);
    }

    /// <summary>Creates the record describing one execution lane claiming a branch tip.</summary>
    /// <param name="request">The accepted provisioning request, which already carries its deterministic commit timestamp.</param>
    /// <returns>A lane-provisioning record.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static JsonSessionStoreLogRecord ForProvision(SessionExecutionLaneProvisionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new JsonSessionStoreLogRecord(
            JsonSessionStoreLogRecordKind.LaneProvisioned, null, request, null, null, null, null, null, null, null,
            null, null);
    }

    /// <summary>Creates the record describing one accepted conditional append.</summary>
    /// <param name="request">The accepted append request carrying the complete ordered entry batch.</param>
    /// <param name="committedAt">The clock-derived instant stamped on the session's last-updated time.</param>
    /// <returns>An append record describing the whole batch as one atomic commit.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static JsonSessionStoreLogRecord ForAppend(SessionAppendRequest request, DateTimeOffset committedAt)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new JsonSessionStoreLogRecord(
            JsonSessionStoreLogRecordKind.EntriesAppended, null, null, request, null, null, null, null, null, null,
            null, committedAt);
    }

    /// <summary>Creates the record describing one branch forked from a committed parent point.</summary>
    /// <param name="request">The accepted fork request.</param>
    /// <param name="branchId">The generated identity of the new branch.</param>
    /// <param name="committedAt">The clock-derived instant stamped on the session's last-updated time.</param>
    /// <returns>A branch record.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static JsonSessionStoreLogRecord ForBranch(
        SessionBranchRequest request, BranchId branchId, DateTimeOffset committedAt)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new JsonSessionStoreLogRecord(
            JsonSessionStoreLogRecordKind.BranchCreated, null, null, null, request, null, null, null, null, null,
            branchId.Value, committedAt);
    }

    /// <summary>Creates the record describing one session deletion.</summary>
    /// <param name="request">The accepted deletion request.</param>
    /// <returns>A deletion record that also retires the creation retry evidence for the removed address.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static JsonSessionStoreLogRecord ForDelete(SessionDeleteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new JsonSessionStoreLogRecord(
            JsonSessionStoreLogRecordKind.SessionDeleted, null, null, null, null, request, null, null, null, null,
            null, null);
    }

    /// <summary>Creates the record describing one accepted admission or reconciled equivalent admission.</summary>
    /// <param name="request">The accepted admission request, which already carries its deterministic admitted timestamp.</param>
    /// <returns>An admission record covering both the fresh-admission and reconciliation commit paths.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <remarks>
    /// Reconciling an equivalent prior admission still mutates durable state because it binds a new idempotency key to the
    /// existing receipt, so it is logged through this same kind. Replay re-executes the request and deterministically takes
    /// whichever of the two paths the original commit took.
    /// </remarks>
    public static JsonSessionStoreLogRecord ForAdmit(SessionInputAdmissionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new JsonSessionStoreLogRecord(
            JsonSessionStoreLogRecordKind.InputAdmitted, null, null, null, null, null, request, null, null, null,
            null, null);
    }

    /// <summary>Creates the record describing one atomic run acceptance.</summary>
    /// <param name="request">The accepted run-start request, which already carries its deterministic acceptance timestamp.</param>
    /// <returns>A run-acceptance record covering promotion, message materialization, and installed accepted state.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static JsonSessionStoreLogRecord ForRunAccepted(SessionRunStartRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new JsonSessionStoreLogRecord(
            JsonSessionStoreLogRecordKind.RunAccepted, null, null, null, null, null, null, request, null, null,
            null, null);
    }

    /// <summary>Creates the record describing one release of a lane's installed accepted run.</summary>
    /// <param name="request">The accepted release request.</param>
    /// <param name="committedAt">The clock-derived instant stamped on the session's last-updated time.</param>
    /// <returns>A release record.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static JsonSessionStoreLogRecord ForRunReleased(
        SessionRunReleaseRequest request, DateTimeOffset committedAt)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new JsonSessionStoreLogRecord(
            JsonSessionStoreLogRecordKind.RunReleased, null, null, null, null, null, null, null, request, null,
            null, committedAt);
    }

    /// <summary>Creates the record describing one atomic mid-run input promotion.</summary>
    /// <param name="request">The accepted promotion request, which already carries its deterministic commit timestamp.</param>
    /// <returns>A promotion record covering message materialization and the advanced accepted-run revision.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static JsonSessionStoreLogRecord ForInputPromoted(SessionInputPromotionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new JsonSessionStoreLogRecord(
            JsonSessionStoreLogRecordKind.InputPromoted, null, null, null, null, null, null, null, null, request,
            null, null);
    }
}
