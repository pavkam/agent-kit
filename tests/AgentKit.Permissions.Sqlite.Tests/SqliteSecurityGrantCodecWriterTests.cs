// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Sqlite.Tests;

/// <summary>Verifies the strict bounded evidence writer's argument checks and every encoding branch through its paired reader.</summary>
public sealed class SqliteSecurityGrantCodecWriterTests
{
    private static readonly SqliteSecurityGrantStoreSettings _settings = SqliteSecurityGrantStoreSettings.CreateDefault();

    [Fact]
    public void Constructor_WhenArgumentsAreInvalid_ThrowsWithExactParameterNames()
    {
        var settingsException = Should.Throw<ArgumentNullException>(() => new SqliteSecurityGrantCodecWriter(null!, 16, "grant"));
        var boundException = Should.Throw<ArgumentOutOfRangeException>(() => new SqliteSecurityGrantCodecWriter(_settings, 0, "grant"));
        var negativeBoundException = Should.Throw<ArgumentOutOfRangeException>(() => new SqliteSecurityGrantCodecWriter(_settings, -1, "grant"));
        var nameException = Should.Throw<ArgumentException>(() => new SqliteSecurityGrantCodecWriter(_settings, 16, "  "));

        settingsException.ParamName.ShouldBe("settings");
        boundException.ParamName.ShouldBe("maximumBytes");
        negativeBoundException.ParamName.ShouldBe("maximumBytes");
        nameException.ParamName.ShouldBe("paramName");
    }

    [Fact]
    public void WriteScope_WhenCorrelationIsBeforeRunWithoutAdmission_RoundTripsThroughReader()
    {
        var scope = new SecurityAuthorizationScope(
            new AgentId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
            null,
            new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("30000000-0000-0000-0000-000000000003")), null));

        var decoded = RoundTripScope(scope);

        decoded.ShouldBe(scope);
        decoded.Correlation.ShouldBeOfType<BeforeRunOperationCorrelation>().AdmissionId.ShouldBeNull();
    }

    [Fact]
    public void WriteScope_WhenCorrelationIsBeforeRunWithAdmission_RoundTripsThroughReader()
    {
        var scope = new SecurityAuthorizationScope(
            new AgentId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
            new SessionId(Guid.Parse("20000000-0000-0000-0000-000000000002")),
            new BeforeRunOperationCorrelation(
                new OperationId(Guid.Parse("30000000-0000-0000-0000-000000000003")),
                new AdmissionId(Guid.Parse("50000000-0000-0000-0000-000000000005"))));

        var decoded = RoundTripScope(scope);

        decoded.ShouldBe(scope);
        decoded.Correlation.ShouldBeOfType<BeforeRunOperationCorrelation>().AdmissionId.ShouldBe(
            new AdmissionId(Guid.Parse("50000000-0000-0000-0000-000000000005")));
    }

    [Fact]
    public void WriteScope_WhenCorrelationIsInRunWithTurn_RoundTripsThroughReader()
    {
        var scope = new SecurityAuthorizationScope(
            new AgentId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
            new SessionId(Guid.Parse("20000000-0000-0000-0000-000000000002")),
            new InRunOperationCorrelation(
                new OperationId(Guid.Parse("30000000-0000-0000-0000-000000000003")),
                new RunId(Guid.Parse("40000000-0000-0000-0000-000000000004")),
                new TurnId(Guid.Parse("60000000-0000-0000-0000-000000000006"))));

        var decoded = RoundTripScope(scope);

        decoded.ShouldBe(scope);
        decoded.Correlation.ShouldBeOfType<InRunOperationCorrelation>().TurnId.ShouldBe(
            new TurnId(Guid.Parse("60000000-0000-0000-0000-000000000006")));
    }

    [Fact]
    public void WriteScope_WhenCorrelationIsAfterRun_RoundTripsThroughReader()
    {
        var scope = new SecurityAuthorizationScope(
            new AgentId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
            null,
            new AfterRunOperationCorrelation(
                new OperationId(Guid.Parse("30000000-0000-0000-0000-000000000003")),
                new RunId(Guid.Parse("40000000-0000-0000-0000-000000000004"))));

        var decoded = RoundTripScope(scope);

        decoded.ShouldBe(scope);
        _ = decoded.Correlation.ShouldBeOfType<AfterRunOperationCorrelation>();
    }

    [Fact]
    public void WriteIdentity_WhenClaimsDelegationAndExpiringEvidenceArePresent_RoundTripsThroughReader()
    {
        var identity = TestGrantFactory.CreateRichIdentity(DateTimeOffset.UnixEpoch);

        var decoded = RoundTripIdentity(identity);

        decoded.ShouldBe(identity);
        _ = decoded.Claims.ShouldHaveSingleItem();
        _ = decoded.DelegationChain.ShouldHaveSingleItem();
        decoded.Evidence.ExpiresAt.ShouldBe(identity.Evidence.ExpiresAt);
    }

    [Fact]
    public void EnsureRemaining_WhenTheConfiguredBoundIsExceeded_ThrowsWithTheGivenParameterName()
    {
        var writer = new SqliteSecurityGrantCodecWriter(_settings, 4, "grant");
        writer.WriteInt32(1);

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => writer.WriteInt32(2));

        exception.ParamName.ShouldBe("grant");
    }

    private static SecurityAuthorizationScope RoundTripScope(SecurityAuthorizationScope scope)
    {
        var writer = new SqliteSecurityGrantCodecWriter(_settings, _settings.MaximumGrantBytes, "grant");
        writer.WriteScope(scope);
        var reader = new SqliteSecurityGrantCodecReader(writer.ToArray(), _settings);
        return reader.ReadScope();
    }

    private static ExecutionIdentity RoundTripIdentity(ExecutionIdentity identity)
    {
        var writer = new SqliteSecurityGrantCodecWriter(_settings, _settings.MaximumGrantBytes, "grant");
        writer.WriteIdentity(identity);
        var reader = new SqliteSecurityGrantCodecReader(writer.ToArray(), _settings);
        return reader.ReadIdentity();
    }
}
