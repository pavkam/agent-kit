// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Sqlite;

/// <summary>Names the one host-authorized database file a SQLite memory, document, or vector store owns and how it may be opened.</summary>
/// <remarks>The path is sensitive bootstrap configuration and is never logged. Each store needs its own database file. Creating a missing database while also requiring exact schema validation is contradictory and is refused.</remarks>
public sealed record SqliteMemoryTarget
{
    /// <summary>Initializes a validated target.</summary>
    /// <param name="databasePath">The fully qualified ordinary database path.</param>
    /// <param name="expectedInstanceId">The identity the database must carry or be created with.</param>
    /// <param name="openMode">Whether a missing database may be created.</param>
    /// <param name="schemaMode">Whether a missing schema may be created.</param>
    /// <exception cref="ArgumentNullException"><paramref name="databasePath"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="databasePath"/> is blank or not one ordinary fully qualified path.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The instance identity is empty, a mode is undefined, or creation is combined with exact validation.</exception>
    public SqliteMemoryTarget(
        string databasePath,
        SqliteMemoryInstanceId expectedInstanceId,
        SqliteDatabaseOpenMode openMode,
        SqliteSchemaMode schemaMode)
    {
        ArgumentNullException.ThrowIfNull(databasePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        ArgumentOutOfRangeException.ThrowIfEqual(expectedInstanceId, default);
        ArgumentOutOfRangeException.ThrowIfUndefined(openMode);
        ArgumentOutOfRangeException.ThrowIfUndefined(schemaMode);
        ArgumentOutOfRangeException.ThrowIfEqual(
            (openMode, schemaMode),
            (SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ValidateExact),
            nameof(schemaMode));
        ArgumentException.ThrowIfInvalidSqliteDatabasePath(databasePath);
        DatabasePath = Path.GetFullPath(databasePath);
        ExpectedInstanceId = expectedInstanceId;
        OpenMode = openMode;
        SchemaMode = schemaMode;
    }

    /// <summary>Gets the normalized database path.</summary>
    public string DatabasePath { get; }

    /// <summary>Gets the instance identity the database must carry.</summary>
    public SqliteMemoryInstanceId ExpectedInstanceId { get; }

    /// <summary>Gets whether a missing database may be created.</summary>
    public SqliteDatabaseOpenMode OpenMode { get; }

    /// <summary>Gets whether a missing schema may be created.</summary>
    public SqliteSchemaMode SchemaMode { get; }
}
