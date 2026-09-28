// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Sqlite;

/// <summary>Carries one durable address encoded as the exact five-column primary key the SQLite schema uses.</summary>
/// <remarks>
/// The key is built once per operation and bound to every command that touches the operation's row, so the journal and
/// the lease manager can never disagree about which row an address names. An absent turn is encoded as sixteen zero
/// bytes, a value no real <see cref="TurnId"/> can take.
/// </remarks>
/// <param name="AgentId">The network-order agent key bytes.</param>
/// <param name="SessionId">The network-order session key bytes.</param>
/// <param name="RunId">The network-order run key bytes.</param>
/// <param name="OperationId">The network-order operation key bytes.</param>
/// <param name="TurnId">The network-order turn key bytes, or sixteen zero bytes for an after-run operation.</param>
internal readonly record struct DurableOperationKey(
    byte[] AgentId,
    byte[] SessionId,
    byte[] RunId,
    byte[] OperationId,
    byte[] TurnId)
{
    /// <summary>Binds every key column to one command using the schema's parameter names.</summary>
    /// <param name="command">The non-null command whose text references the five key parameters.</param>
    /// <exception cref="ArgumentNullException"><paramref name="command"/> is null.</exception>
    internal void Bind(SqliteCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _ = command.Parameters.AddWithValue("$agent", AgentId);
        _ = command.Parameters.AddWithValue("$session", SessionId);
        _ = command.Parameters.AddWithValue("$run", RunId);
        _ = command.Parameters.AddWithValue("$operation", OperationId);
        _ = command.Parameters.AddWithValue("$turn", TurnId);
    }
}
