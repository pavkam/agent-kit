// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json;

/// <summary>One newline-delimited authoritative routing transition appended to the directory record log.</summary>
/// <remarks>
/// <para>
/// Like the session store's log this is a command log: a record retains the exact immutable request the directory
/// committed, and replay re-executes the same deterministic commit path. Nothing in a routing commit depends on a clock or
/// a generated identity — the caller supplies the complete candidate <see cref="SessionLocation"/> — so no additional
/// pinned values are needed and a replayed directory is byte-for-byte the directory that existed before process loss.
/// </para>
/// <para>
/// The member that does not apply to a record's <paramref name="Kind"/> is null and is omitted from the encoded line under
/// the canonical contract. Replay validates applicability rather than trusting the writer.
/// </para>
/// </remarks>
/// <param name="Kind">The discriminator selecting which remaining member is meaningful.</param>
/// <param name="Write">The committed conditional route write, present only for <see cref="JsonSessionDirectoryLogRecordKind.RouteRecorded"/>.</param>
/// <param name="Create">The committed creation route, present only for <see cref="JsonSessionDirectoryLogRecordKind.CreationRouteRecorded"/>.</param>
public sealed record JsonSessionDirectoryLogRecord(
    JsonSessionDirectoryLogRecordKind Kind,
    SessionDirectoryWriteRequest? Write,
    SessionDirectoryCreateRecordRequest? Create)
{
    /// <summary>Creates the record describing one committed conditional route write.</summary>
    /// <param name="request">The accepted write request carrying the candidate location and its retry identity.</param>
    /// <returns>A route-write record.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static JsonSessionDirectoryLogRecord ForWrite(SessionDirectoryWriteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new JsonSessionDirectoryLogRecord(JsonSessionDirectoryLogRecordKind.RouteRecorded, request, null);
    }

    /// <summary>Creates the record describing one committed creation route.</summary>
    /// <param name="request">The accepted creation-record request carrying the canonical outer request and its location.</param>
    /// <returns>A creation-route record.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static JsonSessionDirectoryLogRecord ForCreate(SessionDirectoryCreateRecordRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new JsonSessionDirectoryLogRecord(
            JsonSessionDirectoryLogRecordKind.CreationRouteRecorded, null, request);
    }
}
