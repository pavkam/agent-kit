// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite;

internal sealed partial class SqliteSessionUnitOfWork
{
    /// <summary>Loads one execution lane's row.</summary>
    /// <param name="address">The addressed session.</param>
    /// <param name="laneId">The lane identity.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The lane row, or <see langword="null"/> when the lane is not provisioned.</returns>
    internal async ValueTask<LaneRecord?> GetLaneAsync(SessionAddress address, ExecutionLaneId laneId, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand($"""
            SELECT branch_id, branch_cursor_entry_id, revision, accepted_state FROM {SqliteSessionSchema.LanesTable}
            WHERE agent_id = $agent AND session_id = $session AND lane_id = $lane;
            """);
        AddAddress(command, address);
        _ = command.Parameters.AddWithValue("$lane", ToText(laneId.Value));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var cursor = new SessionBranchCursor(
            new BranchId(Guid.Parse(reader.GetString(0))),
            reader.IsDBNull(1) ? null : new SessionEntryId(Guid.Parse(reader.GetString(1))));
        var record = new LaneRecord(cursor, new SessionLaneRevision(reader.GetInt64(2)));
        if (!reader.IsDBNull(3))
        {
            record.AcceptedState = Deserialize<SessionAcceptedRunState>((byte[]) reader[3]);
        }

        return record;
    }

    /// <summary>Determines whether any lane in this session already owns (has its cursor bound to) a branch.</summary>
    /// <param name="address">The addressed session.</param>
    /// <param name="branchId">The candidate branch.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    internal async ValueTask<bool> AnyLaneOwnsBranchAsync(SessionAddress address, BranchId branchId, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand($"""
            SELECT 1 FROM {SqliteSessionSchema.LanesTable}
            WHERE agent_id = $agent AND session_id = $session AND branch_id = $branch LIMIT 1;
            """);
        AddAddress(command, address);
        _ = command.Parameters.AddWithValue("$branch", ToText(branchId.Value));
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null;
    }

    /// <summary>Inserts a new idle lane row.</summary>
    /// <param name="address">The addressed session.</param>
    /// <param name="laneId">The new lane identity.</param>
    /// <param name="cursor">The lane's initial branch cursor.</param>
    /// <param name="revision">The lane's initial revision.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    internal async ValueTask InsertLaneAsync(
        SessionAddress address, ExecutionLaneId laneId, SessionBranchCursor cursor, SessionLaneRevision revision,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand($"""
            INSERT INTO {SqliteSessionSchema.LanesTable}
                (agent_id, session_id, lane_id, branch_id, branch_cursor_entry_id, revision, accepted_state)
            VALUES ($agent, $session, $lane, $branch, $entry, $revision, NULL);
            """);
        AddAddress(command, address);
        _ = command.Parameters.AddWithValue("$lane", ToText(laneId.Value));
        _ = command.Parameters.AddWithValue("$branch", ToText(cursor.BranchId.Value));
        _ = command.Parameters.AddWithValue("$entry", cursor.LastEntryId is { } entry ? ToText(entry.Value) : DBNull.Value);
        _ = command.Parameters.AddWithValue("$revision", revision.Value);
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Updates a lane's cursor and revision without changing its accepted-run state.</summary>
    /// <param name="address">The addressed session.</param>
    /// <param name="laneId">The lane identity.</param>
    /// <param name="cursor">The new branch cursor.</param>
    /// <param name="revision">The new revision.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    internal async ValueTask UpdateLaneCursorAsync(
        SessionAddress address, ExecutionLaneId laneId, SessionBranchCursor cursor, SessionLaneRevision revision,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand($"""
            UPDATE {SqliteSessionSchema.LanesTable}
            SET branch_id = $branch, branch_cursor_entry_id = $entry, revision = $revision
            WHERE agent_id = $agent AND session_id = $session AND lane_id = $lane;
            """);
        AddAddress(command, address);
        _ = command.Parameters.AddWithValue("$lane", ToText(laneId.Value));
        _ = command.Parameters.AddWithValue("$branch", ToText(cursor.BranchId.Value));
        _ = command.Parameters.AddWithValue("$entry", cursor.LastEntryId is { } entry ? ToText(entry.Value) : DBNull.Value);
        _ = command.Parameters.AddWithValue("$revision", revision.Value);
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Installs or clears a lane's accepted-run state, and updates its cursor and revision atomically.</summary>
    /// <param name="address">The addressed session.</param>
    /// <param name="laneId">The lane identity.</param>
    /// <param name="cursor">The new branch cursor.</param>
    /// <param name="revision">The new revision.</param>
    /// <param name="acceptedState">The new accepted state, or <see langword="null"/> to clear it (a release).</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    internal async ValueTask UpdateLaneAcceptedStateAsync(
        SessionAddress address, ExecutionLaneId laneId, SessionBranchCursor cursor, SessionLaneRevision revision,
        SessionAcceptedRunState? acceptedState, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand($"""
            UPDATE {SqliteSessionSchema.LanesTable}
            SET branch_id = $branch, branch_cursor_entry_id = $entry, revision = $revision, accepted_state = $state
            WHERE agent_id = $agent AND session_id = $session AND lane_id = $lane;
            """);
        AddAddress(command, address);
        _ = command.Parameters.AddWithValue("$lane", ToText(laneId.Value));
        _ = command.Parameters.AddWithValue("$branch", ToText(cursor.BranchId.Value));
        _ = command.Parameters.AddWithValue("$entry", cursor.LastEntryId is { } entry ? ToText(entry.Value) : DBNull.Value);
        _ = command.Parameters.AddWithValue("$revision", revision.Value);
        _ = command.Parameters.AddWithValue("$state", acceptedState is null ? DBNull.Value : Serialize(acceptedState));
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
