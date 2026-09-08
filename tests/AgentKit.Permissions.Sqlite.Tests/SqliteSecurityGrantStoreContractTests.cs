// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Sqlite.Tests;

/// <summary>Verifies exact public configuration and adapter guard semantics.</summary>
public sealed class SqliteSecurityGrantStoreContractTests
{
    /// <summary>Verifies fixed-target construction rejects every invalid bootstrap coordinate with its exact parameter.</summary>
    [Fact]
    public void Constructor_WhenTargetEvidenceIsInvalid_ThrowsExactArgument()
    {
        var instanceId = new SqliteSecurityGrantStoreInstanceId(Guid.NewGuid());
        var path = Path.Combine(Path.GetTempPath(), "grants.db");
        var nullPath = Should.Throw<ArgumentNullException>(() => new SqliteSecurityGrantStoreTarget(
            null!, instanceId, SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact));
        var blankPath = Should.Throw<ArgumentException>(() => new SqliteSecurityGrantStoreTarget(
            " ", instanceId, SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact));
        var emptyId = Should.Throw<ArgumentOutOfRangeException>(() => new SqliteSecurityGrantStoreTarget(
            path, default, SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact));
        var openMode = Should.Throw<ArgumentOutOfRangeException>(() => new SqliteSecurityGrantStoreTarget(
            path, instanceId, (SqliteDatabaseOpenMode) 99, SqliteSchemaMode.ValidateExact));
        var schemaMode = Should.Throw<ArgumentOutOfRangeException>(() => new SqliteSecurityGrantStoreTarget(
            path, instanceId, SqliteDatabaseOpenMode.OpenExisting, (SqliteSchemaMode) 99));
        var impossible = Should.Throw<ArgumentOutOfRangeException>(() => new SqliteSecurityGrantStoreTarget(
            path, instanceId, SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ValidateExact));
        var directId = Should.Throw<ArgumentOutOfRangeException>(() => new SqliteSecurityGrantStoreInstanceId(Guid.Empty));

        nullPath.ParamName.ShouldBe("databasePath");
        blankPath.GetType().ShouldBe(typeof(ArgumentException));
        blankPath.ParamName.ShouldBe("databasePath");
        emptyId.ParamName.ShouldBe("expectedStoreInstanceId");
        openMode.ParamName.ShouldBe("openMode");
        schemaMode.ParamName.ShouldBe("schemaMode");
        impossible.ParamName.ShouldBe("schemaMode");
        directId.ParamName.ShouldBe("value");
    }

    /// <summary>Verifies the custom path guard accepts ordinary absolute paths and reports inferred or explicit names.</summary>
    [Fact]
    public void ThrowIfInvalidSqliteDatabasePath_WhenPathIsInvalid_ReportsExactParameterName()
    {
        var valid = Path.Combine(Path.GetTempPath(), "grants.db");
        ArgumentException.ThrowIfInvalidSqliteDatabasePath(valid);

        const string databasePath = "relative.db";
        var inferred = Should.Throw<ArgumentException>(() =>
            ArgumentException.ThrowIfInvalidSqliteDatabasePath(databasePath));
        var explicitName = Should.Throw<ArgumentException>(() =>
            ArgumentException.ThrowIfInvalidSqliteDatabasePath("file:grants.db", "target"));

        inferred.GetType().ShouldBe(typeof(ArgumentException));
        inferred.ParamName.ShouldBe(nameof(databasePath));
        explicitName.GetType().ShouldBe(typeof(ArgumentException));
        explicitName.ParamName.ShouldBe("target");
    }

    /// <summary>Verifies every special SQLite target syntax is rejected by the type-qualified guard.</summary>
    [Theory]
    [InlineData(":memory:")]
    [InlineData("file:/tmp/grants.db")]
    [InlineData("|DataDirectory|/grants.db")]
    public void ThrowIfInvalidSqliteDatabasePath_WhenSpecialTargetIsUsed_ThrowsExactArgument(string path)
    {
        var exception = Should.Throw<ArgumentException>(() =>
            ArgumentException.ThrowIfInvalidSqliteDatabasePath(path));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe(nameof(path));
    }

    /// <summary>Verifies timeout and evidence bounds reject every unsupported boundary with the caller property name.</summary>
    [Theory]
    [InlineData(0, 1, 1, 1, 1, 1, "lockTimeout")]
    [InlineData(1, 0, 1, 1, 1, 1, "maximumGrantBytes")]
    [InlineData(1, 1, 0, 1, 1, 1, "maximumEnforcementBytes")]
    [InlineData(1, 1, 1, 0, 1, 1, "maximumResources")]
    [InlineData(1, 1, 1, 1, 0, 1, "maximumClaims")]
    [InlineData(1, 1, 1, 1, 1, 0, "maximumDelegationLinks")]
    public void Constructor_WhenSettingsAreInvalid_ThrowsExactArgument(
        int seconds,
        int grantBytes,
        int enforcementBytes,
        int resources,
        int claims,
        int links,
        string paramName)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new SqliteSecurityGrantStoreSettings(
            TimeSpan.FromSeconds(seconds), grantBytes, enforcementBytes, resources, claims, links));

        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe(paramName);
    }

    /// <summary>Verifies provider timeout representation rejects fractional and excessive durations.</summary>
    [Fact]
    public void Constructor_WhenTimeoutCannotBeRepresented_ThrowsExactArgument()
    {
        var fractional = Should.Throw<ArgumentOutOfRangeException>(() => new SqliteSecurityGrantStoreSettings(
            TimeSpan.FromMilliseconds(1500), 1, 1, 1, 1, 1));
        var excessive = Should.Throw<ArgumentOutOfRangeException>(() => new SqliteSecurityGrantStoreSettings(
            TimeSpan.MaxValue, 1, 1, 1, 1, 1));

        fractional.ParamName.ShouldBe("lockTimeout");
        excessive.ParamName.ShouldBe("lockTimeout");
    }

    /// <summary>Verifies store construction rejects each missing immutable collaborator before effects.</summary>
    [Fact]
    public void Constructor_WhenStoreDependencyIsNull_ThrowsExactArgument()
    {
        var target = new SqliteSecurityGrantStoreTarget(
            Path.Combine(Path.GetTempPath(), "grants.db"),
            new SqliteSecurityGrantStoreInstanceId(Guid.NewGuid()),
            SqliteDatabaseOpenMode.CreateIfMissing,
            SqliteSchemaMode.ApplyKnownMigrations);
        var settings = SqliteSecurityGrantStoreSettings.CreateDefault();
        var targetException = Should.Throw<ArgumentNullException>(() =>
            new SqliteSecurityGrantStore(null!, settings, TimeProvider.System));
        var settingsException = Should.Throw<ArgumentNullException>(() =>
            new SqliteSecurityGrantStore(target, null!, TimeProvider.System));
        var timeException = Should.Throw<ArgumentNullException>(() =>
            new SqliteSecurityGrantStore(target, settings, null!));

        targetException.ParamName.ShouldBe("target");
        settingsException.ParamName.ShouldBe("settings");
        timeException.ParamName.ShouldBe("timeProvider");
    }

    /// <summary>Verifies typed unavailable failures reject undefined kinds and unsafe blank explanations.</summary>
    [Fact]
    public void Constructor_WhenUnavailableEvidenceIsInvalid_ThrowsExactArgument()
    {
        var kind = Should.Throw<ArgumentOutOfRangeException>(() =>
            new SecurityGrantStoreUnavailableException((SecurityGrantStoreFailureKind) 99, "safe"));
        var message = Should.Throw<ArgumentException>(() =>
            new SecurityGrantStoreUnavailableException(SecurityGrantStoreFailureKind.OpenFailed, " "));

        kind.ParamName.ShouldBe("kind");
        message.ParamName.ShouldBe("safeMessage");
    }

    /// <summary>Verifies strict persistability guards accept reconstructable values and reject copied malformed scalar evidence.</summary>
    [Fact]
    public void ThrowIfNotPersistable_WhenCopiedEvidenceIsMalformed_ReportsExactEvidenceParameter()
    {
        var settings = SqliteSecurityGrantStoreSettings.CreateDefault();
        var grant = TestGrantFactory.CreateGrant(DateTimeOffset.UnixEpoch);
        var enforcement = TestGrantFactory.CreateEnforcement(grant);
        var capturedGrant = TestGrantFactory.CreateCapturedGrant(DateTimeOffset.UnixEpoch);
        var capturedEnforcement = TestGrantFactory.CreateCapturedEnforcement(capturedGrant);
        ArgumentException.ThrowIfNotPersistable(grant, settings);
        ArgumentException.ThrowIfNotPersistable(enforcement, settings);
        ArgumentException.ThrowIfNotPersistable(capturedGrant, settings);
        ArgumentException.ThrowIfNotPersistable(capturedEnforcement, settings);

        SecurityGrant[] invalidGrants =
        [
            grant with { Id = default },
            grant with { RequestId = default },
            grant with { Audience = default },
            grant with { InputFingerprint = default },
            grant with { PolicyVersion = default },
            grant with { RevocationVersion = default },
            grant with { Kind = (SecurityOperationKind) 999 },
            grant with { Effect = (SecurityEffect) 999 },
            grant with { Resources = default },
            grant with { Resources = [null!] },
            grant with { AllowedUses = 0 },
            grant with { ExpiresAt = grant.NotBefore },
        ];
        foreach (var invalid in invalidGrants)
        {
            var exception = Should.Throw<ArgumentException>(() =>
                ArgumentException.ThrowIfNotPersistable(invalid, settings));
            exception.GetType().ShouldBe(typeof(ArgumentException));
            exception.ParamName.ShouldBe("invalid");
        }

        SecurityEnforcementRequest[] invalidEnforcements =
        [
            enforcement with { Audience = default },
            enforcement with { InputFingerprint = default },
            enforcement with { RevocationVersion = default },
            enforcement with { Kind = (SecurityOperationKind) 999 },
            enforcement with { Effect = (SecurityEffect) 999 },
            enforcement with { Resources = default },
            enforcement with { Resources = [null!] },
        ];
        foreach (var invalid in invalidEnforcements)
        {
            var exception = Should.Throw<ArgumentException>(() =>
                ArgumentException.ThrowIfNotPersistable(invalid, settings, "effect"));
            exception.GetType().ShouldBe(typeof(ArgumentException));
            exception.ParamName.ShouldBe("effect");
        }

        var nullGrant = Should.Throw<ArgumentNullException>(() =>
            ArgumentException.ThrowIfNotPersistable((SecurityGrant) null!, settings));
        var nullEnforcement = Should.Throw<ArgumentNullException>(() =>
            ArgumentException.ThrowIfNotPersistable((SecurityEnforcementRequest) null!, settings));
        var nullSettings = Should.Throw<ArgumentNullException>(() =>
            ArgumentException.ThrowIfNotPersistable(grant, null!));
        nullGrant.GetType().ShouldBe(typeof(ArgumentNullException));
        nullGrant.ParamName.ShouldBe("grant");
        nullEnforcement.GetType().ShouldBe(typeof(ArgumentNullException));
        nullEnforcement.ParamName.ShouldBe("enforcement");
        nullSettings.GetType().ShouldBe(typeof(ArgumentNullException));
        nullSettings.ParamName.ShouldBe("settings");
    }
}
