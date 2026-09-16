// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite.Tests;

using AgentKit.Session;

using Microsoft.Data.Sqlite;

/// <summary>
/// Verifies internal unit-of-work behavior that is otherwise unreachable through the public
/// <see cref="SqliteSessionStore"/> surface, because every session the store ever creates always writes a
/// matching create receipt in the same transaction.
/// </summary>
public sealed class SqliteSessionUnitOfWorkTests
{
    [Fact]
    public async Task MigrateCreateReceiptToDeletedAsync_WhenNoCreateReceiptExistsForAddress_DoesNothing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"agentkit-uow-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(path);
        var databasePath = Path.Combine(path, "sessions.db");
        using (var bootstrap = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = databasePath, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ConnectionString))
        {
            bootstrap.Open();
            using var command = bootstrap.CreateCommand();
            command.CommandText = SqliteSessionSchema.CreateSchema;
            _ = command.ExecuteNonQuery();
        }

        await using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = databasePath, Mode = SqliteOpenMode.ReadWrite, Pooling = false }.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var transaction = connection.BeginTransaction();
        var codecs = new SessionEntryCodecCatalog([], TimeProvider.System);
        var json = new System.Text.Json.JsonSerializerOptions { TypeInfoResolver = SqliteSessionJsonTypeResolver.Create() };
        json.Converters.Add(new SqliteValueObjectJsonConverterFactory());
        json.Converters.Add(new SqliteSessionEntryJsonConverterFactory(codecs));
        var unitOfWork = new SqliteSessionUnitOfWork(connection, transaction, codecs, json);
        var address = new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()));

        // No session was ever created at this address, so no create receipt is indexed under it; the migration
        // must return without writing a deleted-create row instead of failing or fabricating one.
        await unitOfWork.MigrateCreateReceiptToDeletedAsync(address, TestContext.Current.CancellationToken);

        using var countCommand = connection.CreateCommand();
        countCommand.Transaction = transaction;
        countCommand.CommandText = $"SELECT COUNT(*) FROM {SqliteSessionSchema.StoreScopeIdempotencyTable};";
        Convert.ToInt64(await countCommand.ExecuteScalarAsync(TestContext.Current.CancellationToken), System.Globalization.CultureInfo.InvariantCulture)
            .ShouldBe(0L);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }
}
