// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.AgentLoop;

/// <summary>Verifies AgentLoopRunRequest behavior and contracts.</summary>
public sealed class AgentRunRequestTests
{
    private static AgentLoopRunRequest Build(
        AgentDefinition? agent = null,
        SessionProfileSnapshot? sessionProfile = null,
        EffectiveConfigurationSnapshot? configuration = null,
        int maxTurns = 8,
        TimeSpan? attemptTimeout = null,
        ExtensionData? extensions = null) =>
        new(
            agent ?? LoopTestData.Definition(),
            LoopTestData.SessionId,
            LoopTestData.BranchId,
            LoopTestData.RunId,
            LoopTestData.Identity(),
            LoopTestData.RunAuthorization(),
            sessionProfile ?? LoopTestData.SessionProfile(),
            configuration ?? LoopTestData.Configuration(),
            maxTurns,
            attemptTimeout ?? TimeSpan.FromMinutes(1),
            extensions ?? ExtensionData.Empty);

    [Fact]
    public void Constructor_WhenAgentIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new AgentLoopRunRequest(
            null!, LoopTestData.SessionId, LoopTestData.BranchId, LoopTestData.RunId, LoopTestData.Identity(),
            LoopTestData.RunAuthorization(), LoopTestData.SessionProfile(), LoopTestData.Configuration(),
            8, TimeSpan.FromMinutes(1), ExtensionData.Empty)).ParamName.ShouldBe("agent");

    [Fact]
    public void Constructor_WhenIdentityIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new AgentLoopRunRequest(
            LoopTestData.Definition(), LoopTestData.SessionId, LoopTestData.BranchId, LoopTestData.RunId, null!,
            LoopTestData.RunAuthorization(), LoopTestData.SessionProfile(), LoopTestData.Configuration(),
            8, TimeSpan.FromMinutes(1), ExtensionData.Empty)).ParamName.ShouldBe("identity");

    [Fact]
    public void Constructor_WhenAuthorizationIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new AgentLoopRunRequest(
            LoopTestData.Definition(), LoopTestData.SessionId, LoopTestData.BranchId, LoopTestData.RunId, LoopTestData.Identity(),
            null!, LoopTestData.SessionProfile(), LoopTestData.Configuration(),
            8, TimeSpan.FromMinutes(1), ExtensionData.Empty)).ParamName.ShouldBe("authorization");

    [Fact]
    public void Constructor_WhenAuthorizationScopeAgentDiffers_ThrowsExactArgumentException()
    {
        var other = LoopTestData.Definition() with { Id = new AgentId(Guid.NewGuid()) };

        var exception = Should.Throw<ArgumentException>(() => Build(agent: other));

        exception.ParamName.ShouldBe("authorization");
    }

    [Fact]
    public void Constructor_WhenSessionProfileIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new AgentLoopRunRequest(
            LoopTestData.Definition(), LoopTestData.SessionId, LoopTestData.BranchId, LoopTestData.RunId, LoopTestData.Identity(),
            LoopTestData.RunAuthorization(), null!, LoopTestData.Configuration(),
            8, TimeSpan.FromMinutes(1), ExtensionData.Empty)).ParamName.ShouldBe("sessionProfile");

    [Fact]
    public void Constructor_WhenConfigurationIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new AgentLoopRunRequest(
            LoopTestData.Definition(), LoopTestData.SessionId, LoopTestData.BranchId, LoopTestData.RunId, LoopTestData.Identity(),
            LoopTestData.RunAuthorization(), LoopTestData.SessionProfile(), null!,
            8, TimeSpan.FromMinutes(1), ExtensionData.Empty)).ParamName.ShouldBe("configuration");

    [Fact]
    public void Constructor_WhenAgentRevisionDiffersFromAuthorization_ThrowsExactArgumentException()
    {
        var agent = LoopTestData.Definition() with { Revision = new AgentDefinitionRevision(2) };

        Should.Throw<ArgumentException>(() => Build(agent: agent)).ParamName.ShouldBe("agent");
    }

    [Fact]
    public void Constructor_WhenConfigurationVersionDiffersFromAuthorization_ThrowsExactArgumentException()
    {
        var configuration = new EffectiveConfigurationSnapshot(new ConfigurationVersion(2), new ContentHash("sha256:test-session-profile"), [], []);

        _ = Should.Throw<ArgumentException>(() => Build(configuration: configuration));
    }

    [Fact]
    public void Constructor_WhenConfigurationFingerprintDiffersFromSessionProfile_ThrowsExactArgumentException()
    {
        var configuration = new EffectiveConfigurationSnapshot(new ConfigurationVersion(1), new ContentHash("sha256:other"), [], []);

        _ = Should.Throw<ArgumentException>(() => Build(configuration: configuration));
    }

    [Fact]
    public void Constructor_WhenMaxTurnsIsNotPositive_ThrowsExactArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Build(maxTurns: 0)).ParamName.ShouldBe("maxTurns");

    [Fact]
    public void Constructor_WhenAttemptTimeoutIsNotPositive_ThrowsExactArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Build(attemptTimeout: TimeSpan.Zero)).ParamName.ShouldBe("attemptTimeout");

    [Fact]
    public void Constructor_WhenExtensionsIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new AgentLoopRunRequest(
            LoopTestData.Definition(), LoopTestData.SessionId, LoopTestData.BranchId, LoopTestData.RunId, LoopTestData.Identity(),
            LoopTestData.RunAuthorization(), LoopTestData.SessionProfile(), LoopTestData.Configuration(),
            8, TimeSpan.FromMinutes(1), null!)).ParamName.ShouldBe("extensions");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var request = LoopTestData.RunRequest();

        request.Agent.ShouldBe(LoopTestData.Definition());
        request.AgentId.ShouldBe(LoopTestData.AgentId);
        request.SessionId.ShouldBe(LoopTestData.SessionId);
        request.BranchId.ShouldBe(LoopTestData.BranchId);
        request.RunId.ShouldBe(LoopTestData.RunId);
        request.Configuration.ShouldBe(LoopTestData.Configuration());
        request.SessionProfile.ShouldBe(LoopTestData.SessionProfile());
        request.MaxTurns.ShouldBe(8);
        request.AttemptTimeout.ShouldBe(TimeSpan.FromMinutes(1));
        request.Extensions.ShouldBe(ExtensionData.Empty);
        request.Observer.ShouldBeNull();
        request.LaneAdmission.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WhenDefinitionIsSupplied_DerivesRequestMembersFromTheDefinition()
    {
        var request = LoopTestData.RunRequest();
        var agent = request.Agent;

        request.ModelPolicy.ShouldBe(agent.Models);
        request.BudgetProfile.ShouldBe(agent.Components.BudgetProfile);
        request.ModelRequirements.ShouldBe(agent.Models.Requirements);
        request.Settings.ShouldBe(agent.Models.RequestSettings);
        request.Output.ShouldBe(agent.Output);
        request.HookProfile.ShouldBe(agent.HookProfile);
    }

    [Fact]
    public void Equality_WhenValuesMatch_HashesEveryComponent()
    {
        var first = LoopTestData.RunRequest();
        var second = LoopTestData.RunRequest();

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equality_WhenOtherIsNull_IsNotEqual() => LoopTestData.RunRequest().Equals(null).ShouldBeFalse();

    [Fact]
    public void Equality_WhenMaxTurnsDiffers_IsNotEqual() =>
        LoopTestData.RunRequest().ShouldNotBe(LoopTestData.RunRequest() with { MaxTurns = 3 });

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = LoopTestData.RunRequest();
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void With_WhenMaxTurnsIsSetToZero_ThrowsBeforeConstruction()
    {
        var request = LoopTestData.RunRequest();
        var action = () => request with { MaxTurns = 0 };
        action.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe("MaxTurns");
    }

    [Fact]
    public void With_WhenAttemptTimeoutIsSetToZero_ThrowsBeforeConstruction()
    {
        var request = LoopTestData.RunRequest();
        var action = () => request with { AttemptTimeout = TimeSpan.Zero };
        action.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe("AttemptTimeout");
    }

    [Fact]
    public void With_WhenExtensionsIsSetToNull_ThrowsBeforeConstruction()
    {
        var request = LoopTestData.RunRequest();
        var action = () => request with { Extensions = null! };
        action.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe("Extensions");
    }

    [Fact]
    public void With_WhenLaneAdmissionNamesADifferentRun_ThrowsBeforeConstruction()
    {
        var request = LoopTestData.RunRequest();
        var otherRun = new RunId(Guid.NewGuid());
        var admission = new LoopLaneAdmission(
            LoopTestData.ExecutionLaneId,
            new InRunOperationCorrelation(LoopTestData.OperationId, otherRun, LoopTestData.TurnId),
            new OperationStateRevision(1));
        var action = () => request with { LaneAdmission = admission };
        action.ShouldThrow<ArgumentException>().ParamName.ShouldBe("LaneAdmission");
    }

    [Fact]
    public void With_WhenLaneAdmissionNamesTheSameRun_RoundTripsTheValue()
    {
        var request = LoopTestData.RunRequest();
        var admission = new LoopLaneAdmission(
            LoopTestData.ExecutionLaneId, LoopTestData.InRun(), new OperationStateRevision(1));
        var updated = request with { LaneAdmission = admission };
        updated.LaneAdmission.ShouldBe(admission);
    }

    [Fact]
    public void Equals_WhenLaneAdmissionDiffers_ReturnsFalse()
    {
        var first = LoopTestData.RunRequest() with
        {
            LaneAdmission = new LoopLaneAdmission(
                LoopTestData.ExecutionLaneId, LoopTestData.InRun(), new OperationStateRevision(1)),
        };
        var second = LoopTestData.RunRequest();

        first.Equals(second).ShouldBeFalse();
    }
}
