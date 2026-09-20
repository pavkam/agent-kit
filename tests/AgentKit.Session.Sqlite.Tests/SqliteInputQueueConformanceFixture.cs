// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite.Tests;

using AgentKit.Conformance;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Composes the session-backed input queue over the real SQLite session store.</summary>
public sealed class SqliteInputQueueConformanceFixture: SessionBackedInputQueueConformanceFixture
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), $"agentkit-input-queue-{Guid.NewGuid():N}");

    /// <inheritdoc/>
    protected override string StoreKey => "agentkit.sqlite";

    /// <inheritdoc/>
    protected override void RegisterStore(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = Directory.CreateDirectory(_directory);
        _ = services.AddSqliteSessionStore(Target("sessions.db"));
        _ = services.AddSqliteSessionDirectory(new ComponentId("conformance-directory"), Target("directory.db"));
    }

    /// <inheritdoc/>
    protected override ValueTask ReleaseStoreAsync()
    {
        try
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // A still-open handle is released by provider disposal; leaving the temp root is safer than throwing.
        }

        return ValueTask.CompletedTask;
    }

    private SqliteSessionStoreTarget Target(string fileName) => new(
        Path.GetFullPath(Path.Combine(_directory, fileName)),
        new SqliteSessionStoreInstanceId(Guid.NewGuid()),
        SqliteDatabaseOpenMode.CreateIfMissing,
        SqliteSchemaMode.ApplyKnownMigrations);
}
