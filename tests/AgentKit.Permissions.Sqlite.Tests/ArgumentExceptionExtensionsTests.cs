// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Sqlite.Tests;



/// <summary>Verifies ArgumentExceptionExtensions behavior and contracts.</summary>
public sealed class ArgumentExceptionExtensionsTests
{
    /// <summary>Verifies the custom path guard accepts ordinary absolute paths and reports inferred or explicit names.</summary>
    [Fact]
    public void ThrowIfInvalidSqliteDatabasePath_WhenPathIsInvalid_ReportsExactParameterName()
    {
        var valid = Path.Combine(Path.GetTempPath(), "grants.db");
        ArgumentException.ThrowIfInvalidSqliteDatabasePath(valid);
        const string databasePath = "relative.db";
        var inferred = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidSqliteDatabasePath(databasePath));
        var explicitName = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidSqliteDatabasePath("file:grants.db", "target"));
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
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidSqliteDatabasePath(path));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe(nameof(path));
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
        // SecurityGrant's own init accessors now reject Id/RequestId/Audience/InputFingerprint
        // default values, undefined Kind/Effect, a default/empty Resources array,
        // AllowedUses <= 0, and ExpiresAt <= NotBefore directly (see SecurityGrantTests), so those
        // states can no longer be produced by `with` at all; only the remaining entries below still
        // reach ThrowIfNotPersistable's own copied-evidence checks.
        SecurityGrant[] invalidGrants = [grant with
        {
            PolicyVersion = default
        }, grant with
        {
            RevocationVersion = default
        }, grant with
        {
            Resources = [null!]
        }, ];
        foreach (var invalid in invalidGrants)
        {
            var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfNotPersistable(invalid, settings));
            exception.GetType().ShouldBe(typeof(ArgumentException));
            exception.ParamName.ShouldBe("invalid");
        }

        SecurityEnforcementRequest[] invalidEnforcements = [enforcement with
        {
            Audience = default
        }, enforcement with
        {
            InputFingerprint = default
        }, enforcement with
        {
            RevocationVersion = default
        }, enforcement with
        {
            Kind = (SecurityOperationKind)999
        }, enforcement with
        {
            Effect = (SecurityEffect)999
        }, enforcement with
        {
            Resources = default
        }, enforcement with
        {
            Resources = [null!]
        }, ];
        foreach (var invalid in invalidEnforcements)
        {
            var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfNotPersistable(invalid, settings, "effect"));
            exception.GetType().ShouldBe(typeof(ArgumentException));
            exception.ParamName.ShouldBe("effect");
        }

        var nullGrant = Should.Throw<ArgumentNullException>(() => ArgumentException.ThrowIfNotPersistable((SecurityGrant) null!, settings));
        var nullEnforcement = Should.Throw<ArgumentNullException>(() => ArgumentException.ThrowIfNotPersistable((SecurityEnforcementRequest) null!, settings));
        var nullSettings = Should.Throw<ArgumentNullException>(() => ArgumentException.ThrowIfNotPersistable(grant, null!));
        nullGrant.GetType().ShouldBe(typeof(ArgumentNullException));
        nullGrant.ParamName.ShouldBe("grant");
        nullEnforcement.GetType().ShouldBe(typeof(ArgumentNullException));
        nullEnforcement.ParamName.ShouldBe("enforcement");
        nullSettings.GetType().ShouldBe(typeof(ArgumentNullException));
        nullSettings.ParamName.ShouldBe("settings");
    }
}
