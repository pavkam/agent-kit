// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Sqlite.Tests;

/// <summary>Creates isolated temporary SQLite durable stores for one scenario.</summary>
/// <remarks>
/// Each store gets its own uniquely named directory under the temporary root so scenarios never observe one another,
/// and the store identity is generated per store so an accidentally shared path fails loudly instead of silently
/// mixing operations. The directory is left for the operating system to reclaim: the conformance suites construct
/// fixtures directly and never dispose them, so a disposal contract here would be one that nothing honors.
/// </remarks>
internal sealed class SqliteDurableTestStore
{
    /// <summary>Creates one empty store directory and the database target inside it.</summary>
    internal SqliteDurableTestStore()
    {
        var root = Path.GetTempPath();
        if (OperatingSystem.IsMacOS() && root.StartsWith("/var/", StringComparison.Ordinal))
        {
            root = "/private" + root;
        }

        var directory = Path.Combine(root, $"agentkit-durability-sqlite-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);
        Target = new SqliteDurableStoreTarget(
            Path.Combine(directory, "durability.db"),
            new SqliteDurableStoreInstanceId(Guid.NewGuid()),
            SqliteDatabaseOpenMode.CreateIfMissing,
            SqliteSchemaMode.ApplyKnownMigrations);
    }

    /// <summary>Gets the host-authorized target every database over this store opens.</summary>
    internal SqliteDurableStoreTarget Target { get; }

    /// <summary>Creates and initializes one database boundary over this store's target.</summary>
    /// <returns>An initialized database ready for journal and lease operations.</returns>
    internal SqliteDurableDatabase CreateDatabase()
    {
        var database = new SqliteDurableDatabase(Target, SqliteDurableStoreSettings.CreateDefault());
        database.Initialize(CancellationToken.None);
        return database;
    }
}
