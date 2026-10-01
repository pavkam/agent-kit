// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Sqlite.Tests;

/// <summary>Verifies the SQLite artifact-store registration is explicit, keyed, idempotent, and never chooses a persistence target.</summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddSqliteArtifactStore_WhenArgumentsAreInvalid_ThrowsNamingThem()
    {
        using var database = new SqliteArtifactTestDatabase();
        IServiceCollection services = null!;
        var key = new ArtifactBackendKey("sqlite");

        Should.Throw<ArgumentNullException>(() => services.AddSqliteArtifactStore(key, database.Target())).ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddSqliteArtifactStore(key, null!)).ParamName.ShouldBe("target");
        Should.Throw<ArgumentException>(() => new ServiceCollection().AddSqliteArtifactStore(default, database.Target())).ParamName.ShouldBe("key");
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new ServiceCollection().AddSqliteArtifactStore(key, database.Target(), static options => options.MaximumRecordBytes = 0));
    }

    [Fact]
    public async Task AddSqliteArtifactStore_WhenRegistered_ResolvesOneKeyedStoreThatOpensLazily()
    {
        using var database = new SqliteArtifactTestDatabase();
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISecurityGrantStore>(new Permissions.InMemory.InMemorySecurityGrantStore(TimeProvider.System));
        _ = services.AddSqliteArtifactStore(new ArtifactBackendKey("sqlite"), database.Target());
        _ = services.AddSqliteArtifactStore(new ArtifactBackendKey("sqlite"), database.Target("ignored"));
        await using var provider = services.BuildServiceProvider();

        var store = provider.GetRequiredKeyedService<IArtifactStore>("sqlite");

        store.ShouldBeOfType<SqliteArtifactStore>().ShouldBeSameAs(provider.GetRequiredKeyedService<IArtifactStore>("sqlite"));
        File.Exists(database.PathOf()).ShouldBeFalse();
        provider.GetService<IArtifactStore>().ShouldBeNull();
        await ((SqliteArtifactStore) store).InitializeAsync(TestContext.Current.CancellationToken);
        File.Exists(database.PathOf()).ShouldBeTrue();
        File.Exists(database.PathOf("ignored")).ShouldBeFalse();
    }
}
