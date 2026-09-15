// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite;

/// <summary>Captures one explicit fixed SQLite target and its trusted bootstrap permissions.</summary>
/// <remarks>Construction validates configuration only and performs no filesystem access.</remarks>
public sealed record SqliteSessionStoreTarget
{
    /// <summary>Initializes immutable target evidence.</summary>
    /// <param name="databasePath">The fully qualified ordinary database path.</param>
    /// <param name="expectedStoreInstanceId">The identity expected in persistent schema metadata.</param>
    /// <param name="openMode">Whether bootstrap may create the main file.</param>
    /// <param name="schemaMode">Whether bootstrap may install known schema.</param>
    /// <exception cref="ArgumentNullException"><paramref name="databasePath"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="databasePath"/> is not a fully qualified ordinary path.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity or mode is invalid, or creation is combined with validation-only schema handling.</exception>
    public SqliteSessionStoreTarget(
        string databasePath,
        SqliteSessionStoreInstanceId expectedStoreInstanceId,
        SqliteDatabaseOpenMode openMode,
        SqliteSchemaMode schemaMode)
    {
        ArgumentNullException.ThrowIfNull(databasePath);
        ArgumentException.ThrowIfInvalidSqliteDatabasePath(databasePath);
        ArgumentOutOfRangeException.ThrowIfEqual(expectedStoreInstanceId, default);
        ArgumentOutOfRangeException.ThrowIfUndefined(openMode);
        ArgumentOutOfRangeException.ThrowIfUndefined(schemaMode);
        ArgumentOutOfRangeException.ThrowIfEqual(
            (openMode, schemaMode),
            (SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ValidateExact),
            nameof(schemaMode));
        DatabasePath = Path.GetFullPath(databasePath);
        ExpectedStoreInstanceId = expectedStoreInstanceId;
        OpenMode = openMode;
        SchemaMode = schemaMode;
    }

    /// <summary>Gets the normalized database path.</summary><value>An absolute ordinary path.</value>
    public string DatabasePath { get; }
    /// <summary>Gets the expected persistent identity.</summary><value>A nondefault identity.</value>
    public SqliteSessionStoreInstanceId ExpectedStoreInstanceId { get; }
    /// <summary>Gets file creation permission.</summary><value>A defined mode.</value>
    public SqliteDatabaseOpenMode OpenMode { get; }
    /// <summary>Gets schema mutation permission.</summary><value>A defined mode.</value>
    public SqliteSchemaMode SchemaMode { get; }
}
