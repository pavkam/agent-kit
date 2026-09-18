// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json;

/// <summary>One newline-delimited authoritative transition appended to the approval-store record log.</summary>
/// <remarks>
/// <para>
/// A single record is the store's atomicity unit. A creation carries the complete request and a resolution carries the
/// complete response, including the binding it decided, so replay never has to infer terminal authority evidence from a
/// partially written pair of records.
/// </para>
/// <para>
/// Members not applicable to a record's <paramref name="Kind"/> are null and are omitted from the encoded line under the
/// canonical contract. Replay validates applicability rather than trusting the writer, so a hand-edited or truncated log is
/// rejected instead of silently producing a pending request that was actually resolved, or a resolution with no request.
/// </para>
/// </remarks>
/// <param name="Kind">The discriminator selecting which remaining members are meaningful.</param>
/// <param name="Request">The complete immutable request, present for <see cref="JsonApprovalLogRecordKind.Created"/>.</param>
/// <param name="Response">The complete terminal response, present for <see cref="JsonApprovalLogRecordKind.Resolved"/>.</param>
public sealed record JsonApprovalLogRecord(
    JsonApprovalLogRecordKind Kind,
    JsonApprovalRequest? Request,
    JsonApprovalResponse? Response)
{
    /// <summary>Creates the record describing one approval request entering the store as pending.</summary>
    /// <param name="request">The complete immutable request evidence.</param>
    /// <returns>A creation record carrying the full request.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static JsonApprovalLogRecord ForCreation(ApprovalRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new JsonApprovalLogRecord(
            JsonApprovalLogRecordKind.Created, JsonApprovalRequest.FromDomain(request), null);
    }

    /// <summary>Creates the record describing the one terminal response that resolves a pending request.</summary>
    /// <param name="response">The complete authenticated terminal response.</param>
    /// <returns>A resolution record carrying the full response, including the binding it decided and its approver identity.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="response"/> is null.</exception>
    public static JsonApprovalLogRecord ForResolution(ApprovalResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return new JsonApprovalLogRecord(
            JsonApprovalLogRecordKind.Resolved, null, JsonApprovalResponse.FromDomain(response));
    }
}
