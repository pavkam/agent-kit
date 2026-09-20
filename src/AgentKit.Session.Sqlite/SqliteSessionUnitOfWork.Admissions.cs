// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite;

internal sealed partial class SqliteSessionUnitOfWork
{
    private const string _admissionColumns =
        "admission_id, input_id, execution_lane_id, admitted_sequence, promoted_sequence, entry_id, correlation, admitted_input";

    /// <summary>Loads the single canonical admission by its durable admission identity.</summary>
    /// <param name="address">The addressed session.</param>
    /// <param name="admissionId">The admission identity.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The stored admission, or <see langword="null"/> when no such admission exists.</returns>
    internal async ValueTask<StoredAdmission?> GetAdmissionByIdAsync(
        SessionAddress address, AdmissionId admissionId, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand($"""
            SELECT {_admissionColumns} FROM {SqliteSessionSchema.AdmissionsTable}
            WHERE agent_id = $agent AND session_id = $session AND admission_id = $admission;
            """);
        AddAddress(command, address);
        _ = command.Parameters.AddWithValue("$admission", ToText(admissionId.Value));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadAdmission(reader) : null;
    }

    /// <summary>Resolves the single canonical admission for one caller input identity through the indexed reverse lookup.</summary>
    /// <param name="address">The addressed session.</param>
    /// <param name="inputId">The caller-supplied input identity.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The stored admission, or <see langword="null"/> when this input was never admitted.</returns>
    internal async ValueTask<StoredAdmission?> GetAdmissionByInputIdAsync(
        SessionAddress address, InputId inputId, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand($"""
            SELECT {_admissionColumns} FROM {SqliteSessionSchema.AdmissionsTable}
            WHERE agent_id = $agent AND session_id = $session AND input_id = $input;
            """);
        AddAddress(command, address);
        _ = command.Parameters.AddWithValue("$input", ToText(inputId.Value));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadAdmission(reader) : null;
    }

    /// <summary>Counts admissions that have not yet been promoted into a run.</summary>
    /// <param name="address">The addressed session.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    internal async ValueTask<int> CountPendingAdmissionsAsync(SessionAddress address, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand($"""
            SELECT COUNT(*) FROM {SqliteSessionSchema.AdmissionsTable}
            WHERE agent_id = $agent AND session_id = $session AND promoted_sequence IS NULL;
            """);
        AddAddress(command, address);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);
    }

    /// <summary>Inserts one new canonical admission row.</summary>
    /// <param name="address">The addressed session.</param>
    /// <param name="admission">The complete new admission.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    internal async ValueTask InsertAdmissionAsync(SessionAddress address, StoredAdmission admission, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand($"""
            INSERT INTO {SqliteSessionSchema.AdmissionsTable} (agent_id, session_id, {_admissionColumns})
            VALUES ($agent, $session, $admission, $input, $lane, $admitted, $promoted, $entry, $correlation, $payload);
            """);
        AddAddress(command, address);
        _ = command.Parameters.AddWithValue("$admission", ToText(admission.Input.AdmissionId.Value));
        _ = command.Parameters.AddWithValue("$input", ToText(admission.Input.OriginalPayload.Id.Value));
        _ = command.Parameters.AddWithValue("$lane", ToText(admission.Input.ExecutionLaneId.Value));
        _ = command.Parameters.AddWithValue("$admitted", admission.Input.AdmittedSequence.Value);
        _ = command.Parameters.AddWithValue(
            "$promoted", admission.Input.PromotedSequence is { } promoted ? promoted.Value : DBNull.Value);
        _ = command.Parameters.AddWithValue("$entry", ToText(admission.EntryId.Value));
        _ = command.Parameters.AddWithValue("$correlation", Serialize(admission.Correlation));
        _ = command.Parameters.AddWithValue("$payload", Serialize(admission.Input));
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Loads every not-yet-promoted admission for one execution lane, in ascending admitted-sequence order.</summary>
    /// <param name="address">The addressed session.</param>
    /// <param name="laneId">The lane identity.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The lane's pending admissions in admitted-sequence order.</returns>
    internal async ValueTask<ImmutableArray<AdmittedInput>> ListPendingAdmissionsByLaneAsync(
        SessionAddress address, ExecutionLaneId laneId, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand($"""
            SELECT {_admissionColumns} FROM {SqliteSessionSchema.AdmissionsTable}
            WHERE agent_id = $agent AND session_id = $session AND execution_lane_id = $lane AND promoted_sequence IS NULL
            ORDER BY admitted_sequence ASC;
            """);
        AddAddress(command, address);
        _ = command.Parameters.AddWithValue("$lane", ToText(laneId.Value));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var builder = ImmutableArray.CreateBuilder<AdmittedInput>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            builder.Add(ReadAdmission(reader).Input);
        }

        return builder.ToImmutable();
    }

    /// <summary>Records that one admission was consumed by a promotion at a given sequence.</summary>
    /// <param name="address">The addressed session.</param>
    /// <param name="updated">The admission with its updated <see cref="AdmittedInput.PromotedSequence"/> already set.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    internal async ValueTask MarkAdmissionPromotedAsync(SessionAddress address, AdmittedInput updated, CancellationToken cancellationToken)
    {
        Debug.Assert(updated.PromotedSequence is not null, "The caller sets the promoted sequence before persisting it.");
        await using var command = CreateCommand($"""
            UPDATE {SqliteSessionSchema.AdmissionsTable} SET promoted_sequence = $promoted, admitted_input = $payload
            WHERE agent_id = $agent AND session_id = $session AND admission_id = $admission;
            """);
        AddAddress(command, address);
        _ = command.Parameters.AddWithValue("$admission", ToText(updated.AdmissionId.Value));
        _ = command.Parameters.AddWithValue("$promoted", updated.PromotedSequence.Value.Value);
        _ = command.Parameters.AddWithValue("$payload", Serialize(updated));
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Deletes pending admissions bound to one lane, leaving promoted admissions and other lanes in place.</summary>
    /// <param name="address">The addressed session.</param>
    /// <param name="laneId">The lane whose unpromoted admissions are pruned.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    internal async ValueTask DeletePendingAdmissionsByLaneAsync(
        SessionAddress address, ExecutionLaneId laneId, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand($"""
            DELETE FROM {SqliteSessionSchema.AdmissionsTable}
            WHERE agent_id = $agent AND session_id = $session AND execution_lane_id = $lane AND promoted_sequence IS NULL;
            """);
        AddAddress(command, address);
        _ = command.Parameters.AddWithValue("$lane", ToText(laneId.Value));
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private StoredAdmission ReadAdmission(SqliteDataReader reader)
    {
        var admissionId = new AdmissionId(Guid.Parse(reader.GetString(0)));
        var inputId = new InputId(Guid.Parse(reader.GetString(1)));
        var laneId = new ExecutionLaneId(Guid.Parse(reader.GetString(2)));
        var admittedSequence = new SessionSequence(reader.GetInt64(3));
        var entryId = new SessionEntryId(Guid.Parse(reader.GetString(5)));
        var correlation = Deserialize<BeforeRunOperationCorrelation>((byte[]) reader[6]);
        var input = Deserialize<AdmittedInput>((byte[]) reader[7]);
        var receipt = new AdmissionReceipt(admissionId, inputId, input.AgentId, input.SessionId, laneId, admittedSequence, existing: false);
        return new StoredAdmission(input, correlation, entryId, receipt);
    }
}
