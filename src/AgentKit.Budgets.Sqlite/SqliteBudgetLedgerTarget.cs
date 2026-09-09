// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Sqlite;

/// <summary>Captures one fixed local SQLite target supplied through the security control-plane bootstrap boundary.</summary>
/// <remarks>
/// The value authorizes no ordinary file operation. Construction validates configuration without probing or creating the
/// target; initialization performs the explicitly selected effects. Link and persistent store-identity checks detect
/// accidental or ordinary path replacement but are not operating-system isolation; trusted bootstrap retains responsibility
/// for directory ownership, access control, and target capability confinement.
/// </remarks>
public sealed record SqliteBudgetLedgerTarget
{
    /// <summary>Initializes immutable bootstrap configuration for one exact database.</summary>
    /// <param name="databasePath">The fully qualified local main-database path interpreted only as a path.</param>
    /// <param name="expectedStoreInstanceId">The nondefault identity that initialized schema metadata must contain.</param>
    /// <param name="openMode">Whether the exact main file may be created.</param>
    /// <param name="schemaMode">Whether known schema and WAL changes may be applied.</param>
    /// <exception cref="ArgumentNullException"><paramref name="databasePath"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="databasePath"/> is blank, relative, an SQLite memory target, a file URI, or a DataDirectory substitution.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="expectedStoreInstanceId"/> is default, an enum is undefined, or creation is combined with validation-only schema handling.</exception>
    public SqliteBudgetLedgerTarget(
        string databasePath,
        SqliteBudgetLedgerInstanceId expectedStoreInstanceId,
        SqliteDatabaseOpenMode openMode,
        SqliteSchemaMode schemaMode)
    {
        ArgumentNullException.ThrowIfNull(databasePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        ArgumentOutOfRangeException.ThrowIfEqual(expectedStoreInstanceId, default);
        ArgumentOutOfRangeException.ThrowIfUndefined(openMode);
        ArgumentOutOfRangeException.ThrowIfUndefined(schemaMode);
        ArgumentOutOfRangeException.ThrowIfEqual(
            (openMode, schemaMode),
            (SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ValidateExact),
            nameof(schemaMode));
        ArgumentException.ThrowIfInvalidSqliteDatabasePath(databasePath);

        DatabasePath = Path.GetFullPath(databasePath);
        ExpectedStoreInstanceId = expectedStoreInstanceId;
        OpenMode = openMode;
        SchemaMode = schemaMode;
    }

    /// <summary>Gets the normalized fully qualified main-database path.</summary>
    /// <value>The fixed target path; callers must treat it as sensitive bootstrap configuration and never emit it to ordinary diagnostics.</value>
    public string DatabasePath { get; }

    /// <summary>Gets the expected persistent store identity.</summary>
    /// <value>The exact nondefault identity verified before every budget-ledger access.</value>
    public SqliteBudgetLedgerInstanceId ExpectedStoreInstanceId { get; }

    /// <summary>Gets the main-file creation permission.</summary>
    /// <value>The explicit bootstrap open mode.</value>
    public SqliteDatabaseOpenMode OpenMode { get; }

    /// <summary>Gets the schema mutation permission.</summary>
    /// <value>The explicit validation or known-migration mode.</value>
    public SqliteSchemaMode SchemaMode { get; }
}
