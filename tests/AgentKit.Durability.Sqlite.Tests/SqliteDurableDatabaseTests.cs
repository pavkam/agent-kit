// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Sqlite.Tests;

/// <summary>Verifies the shared database boundary creates, validates, and refuses targets exactly as documented.</summary>
public sealed class SqliteDurableDatabaseTests
{
    /// <summary>Verifies a required dependency is refused before anything touches the filesystem.</summary>
    [Fact]
    public void Constructor_WhenTargetIsNull_ThrowsForTheTargetArgument()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new SqliteDurableDatabase(null!, SqliteDurableStoreSettings.CreateDefault()));

        exception.ParamName.ShouldBe("target");
    }

    /// <summary>Verifies bounds are required, because every operation enforces them.</summary>
    [Fact]
    public void Constructor_WhenSettingsAreNull_ThrowsForTheSettingsArgument()
    {
        var store = new SqliteDurableTestStore();

        var exception = Should.Throw<ArgumentNullException>(() => new SqliteDurableDatabase(store.Target, null!));

        exception.ParamName.ShouldBe("settings");
    }

    /// <summary>Verifies construction performs no bootstrap effect, so the target file appears only on initialization.</summary>
    [Fact]
    public void Constructor_WhenCalled_CreatesNoDatabaseFile()
    {
        var store = new SqliteDurableTestStore();

        var database = new SqliteDurableDatabase(store.Target, SqliteDurableStoreSettings.CreateDefault());

        database.Target.ShouldBe(store.Target);
        File.Exists(store.Target.DatabasePath).ShouldBeFalse();
    }

    /// <summary>Verifies initialization creates the database under explicit creation permission.</summary>
    [Fact]
    public void Initialize_WhenCreationIsPermitted_CreatesTheExactTarget()
    {
        var store = new SqliteDurableTestStore();
        var database = new SqliteDurableDatabase(store.Target, SqliteDurableStoreSettings.CreateDefault());

        database.Initialize(TestContext.Current.CancellationToken);

        File.Exists(store.Target.DatabasePath).ShouldBeTrue();
    }

    /// <summary>Verifies repeated initialization is a no-op, so journal and lease manager may each initialize.</summary>
    [Fact]
    public void Initialize_WhenRepeated_SucceedsWithoutRecreatingTheStore()
    {
        var store = new SqliteDurableTestStore();
        var database = store.CreateDatabase();

        Should.NotThrow(() => database.Initialize(TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies a missing target is refused rather than silently created when creation was not permitted.</summary>
    [Fact]
    public void Initialize_WhenTargetIsMissingAndCreationIsNotPermitted_ThrowsAnOpenFailure()
    {
        var store = new SqliteDurableTestStore();
        var database = new SqliteDurableDatabase(
            new SqliteDurableStoreTarget(
                store.Target.DatabasePath,
                store.Target.ExpectedStoreInstanceId,
                SqliteDatabaseOpenMode.OpenExisting,
                SqliteSchemaMode.ValidateExact),
            SqliteDurableStoreSettings.CreateDefault());

        var exception = Should.Throw<InvalidOperationException>(
            () => database.Initialize(TestContext.Current.CancellationToken));

        exception.Data["agentkit.failure_kind"].ShouldBe("open_failed");
    }

    /// <summary>Verifies an existing store opened under a different expected identity is refused.</summary>
    [Fact]
    public void Initialize_WhenStoreIdentityDiffers_ThrowsACorruptEvidenceFailure()
    {
        var store = new SqliteDurableTestStore();
        _ = store.CreateDatabase();
        var repointed = new SqliteDurableDatabase(
            new SqliteDurableStoreTarget(
                store.Target.DatabasePath,
                new SqliteDurableStoreInstanceId(Guid.NewGuid()),
                SqliteDatabaseOpenMode.OpenExisting,
                SqliteSchemaMode.ValidateExact),
            SqliteDurableStoreSettings.CreateDefault());

        var exception = Should.Throw<InvalidOperationException>(
            () => repointed.Initialize(TestContext.Current.CancellationToken));

        exception.Data["agentkit.failure_kind"].ShouldBe("corrupt_evidence");
    }

    /// <summary>Verifies cancellation before the bootstrap linearization point leaves no initialized store.</summary>
    [Fact]
    public void Initialize_WhenCancelledBeforeCommit_ThrowsAndLeavesTheStoreUninitialized()
    {
        var store = new SqliteDurableTestStore();
        var database = new SqliteDurableDatabase(store.Target, SqliteDurableStoreSettings.CreateDefault());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = Should.Throw<OperationCanceledException>(() => database.Initialize(cancellation.Token));
        _ = Should.Throw<InvalidOperationException>(() => database.RequireInitialized("test"));
    }

    /// <summary>Verifies use before trusted bootstrap fails closed instead of opening an unvalidated target.</summary>
    [Fact]
    public void RequireInitialized_WhenBootstrapDidNotRun_ThrowsAnOpenFailure()
    {
        var store = new SqliteDurableTestStore();
        var database = new SqliteDurableDatabase(store.Target, SqliteDurableStoreSettings.CreateDefault());

        var exception = Should.Throw<InvalidOperationException>(() => database.RequireInitialized("operation journal"));

        exception.Data["agentkit.failure_kind"].ShouldBe("open_failed");
        exception.Message.ShouldContain("operation journal");
    }

    /// <summary>Verifies an initialized store validates its own schema, identity, integrity, and journal mode.</summary>
    [Fact]
    public void Validate_WhenStoreIsInitialized_AcceptsTheExactSchemaAndJournalMode()
    {
        var store = new SqliteDurableTestStore();
        var database = store.CreateDatabase();

        using var connection = database.OpenValidated();

        Should.NotThrow(() => database.Validate(connection, requireWal: true, performIntegrityCheck: true));
    }

    /// <summary>Verifies an unrelated table in the target makes the schema shape unsupported.</summary>
    [Fact]
    public void Validate_WhenAnUnexpectedTableExists_ThrowsASchemaFailure()
    {
        var store = new SqliteDurableTestStore();
        var database = store.CreateDatabase();
        using (var connection = database.OpenValidated())
        {
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE intruder (id INTEGER PRIMARY KEY);";
            _ = command.ExecuteNonQuery();
        }

        using var reopened = database.OpenValidated();
        var exception = Should.Throw<InvalidOperationException>(
            () => database.Validate(reopened, requireWal: true, performIntegrityCheck: false));

        exception.Data["agentkit.failure_kind"].ShouldBe("schema_unsupported");
    }

    /// <summary>Verifies an absent turn encodes to a sentinel a real turn can never produce.</summary>
    [Fact]
    public void EncodeKey_WhenTurnIsAbsent_UsesTheZeroSentinelDistinctFromEveryRealTurn()
    {
        var address = DurabilityConformanceData.Address();
        var inRun = address with { TurnId = new TurnId(Guid.NewGuid()) };

        var afterRun = SqliteDurableDatabase.EncodeKey(address with { TurnId = null });

        afterRun.TurnId.ShouldBe(new byte[16]);
        SqliteDurableDatabase.EncodeKey(inRun).TurnId.ShouldNotBe(afterRun.TurnId);
    }

    /// <summary>Verifies a payload whose digest does not match is refused rather than decoded.</summary>
    [Fact]
    public void VerifyDigest_WhenDigestDoesNotMatch_ThrowsACorruptEvidenceFailure()
    {
        var exception = Should.Throw<InvalidOperationException>(
            () => SqliteDurableDatabase.VerifyDigest([1, 2, 3], new byte[32]));

        exception.Data["agentkit.failure_kind"].ShouldBe("corrupt_evidence");
    }
}
