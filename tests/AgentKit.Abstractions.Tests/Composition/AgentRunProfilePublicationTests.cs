// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Composition;



/// <summary>Verifies AgentRunProfilePublication behavior and contracts.</summary>
public sealed class AgentRunProfilePublicationTests
{
    [Fact]
    public void AgentRunProfilePublication_WhenArgumentsAreNull_ThrowsWithExactParameterNames()
    {
        var sessionProfile = SessionProfile();
        var securityProfile = SecurityProfile();
        var security = Should.Throw<ArgumentNullException>(() => new AgentRunProfilePublication(null!, sessionProfile, Hook, Budget, Configuration()));
        var session = Should.Throw<ArgumentNullException>(() => new AgentRunProfilePublication(securityProfile, null!, Hook, Budget, Configuration()));
        var configuration = Should.Throw<ArgumentNullException>(() => new AgentRunProfilePublication(securityProfile, sessionProfile, Hook, Budget, null!));
        security.ParamName.ShouldBe("securityProfile");
        session.ParamName.ShouldBe("sessionProfile");
        configuration.ParamName.ShouldBe("configuration");
    }

    [Fact]
    public void ThrowIfDuplicateAgentRunProfileCoordinates_WhenInvokedByType_ValidatesExactCoordinates()
    {
        var publication = new AgentRunProfilePublication(SecurityProfile(), SessionProfile(), Hook, Budget, Configuration());
        var valid = ImmutableArray.Create(publication);
        var duplicate = ImmutableArray.Create(publication, publication);
        Should.NotThrow(() => ArgumentException.ThrowIfDuplicateAgentRunProfileCoordinates(valid));
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfDuplicateAgentRunProfileCoordinates(duplicate));
        exception.ParamName.ShouldBe("duplicate");
    }

    [Fact]
    public void Constructor_WhenConfigurationMatchesProfiles_PreservesExactSnapshot()
    {
        var configuration = Configuration();
        var publication = new AgentRunProfilePublication(SecurityProfile(), SessionProfile(), Hook, Budget, configuration);
        publication.Configuration.ShouldBeSameAs(configuration);
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var security = SecurityProfile();
        var session = SessionProfile();
        var publication = new AgentRunProfilePublication(security, session, Hook, Budget, Configuration());
        publication.SecurityProfile.ShouldBeSameAs(security);
        publication.SessionProfile.ShouldBeSameAs(session);
        publication.HookProfile.ShouldBe(Hook);
        publication.BudgetProfile.ShouldBe(Budget);
    }

    [Fact]
    public void Constructor_WhenHookProfileIsDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new AgentRunProfilePublication(SecurityProfile(), SessionProfile(), default, Budget, Configuration()))
            .ParamName.ShouldBe("hookProfile");

    [Fact]
    public void Constructor_WhenBudgetProfileIsDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new AgentRunProfilePublication(SecurityProfile(), SessionProfile(), Hook, default, Configuration()))
            .ParamName.ShouldBe("budgetProfile");

    [Fact]
    public void Equality_WhenHookOrBudgetProfileDiffers_IsNotEqual()
    {
        var baseline = new AgentRunProfilePublication(SecurityProfile(), SessionProfile(), Hook, Budget, Configuration());

        baseline.ShouldNotBe(new AgentRunProfilePublication(SecurityProfile(), SessionProfile(), new HookProfileKey("other"), Budget, Configuration()));
        baseline.ShouldNotBe(new AgentRunProfilePublication(SecurityProfile(), SessionProfile(), Hook, new BudgetProfileKey("other"), Configuration()));
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new AgentRunProfilePublication(SecurityProfile(), SessionProfile(), Hook, Budget, Configuration());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static EffectiveConfigurationSnapshot Configuration() => new(new ConfigurationVersion(1), new ContentHash("sha256:session"), [], []);
    private static HookProfileKey Hook { get; } = new("default");
    private static BudgetProfileKey Budget { get; } = new("budget");
    private static SecurityProfilePublication SecurityProfile() => new(new AgentId(Guid.Parse("a0000000-0000-0000-0000-000000000001")), new AgentDefinitionRevision(1), new ConfigurationVersion(1), new SecurityProfileKey("security"), new SecurityProfileVersion(1), new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Guid.Parse("d0000000-0000-0000-0000-000000000004")), new SecurityPolicyVersion(1), new ContentHash("sha256:policy")), new ComponentKey<ISecurityAuthority>("authority"));
    private static SessionProfileSnapshot SessionProfile() => new(new SessionProfileReference(new SessionProfileKey("session"), new SessionProfileVersion(1)), new ComponentKey<ISessionCoordinator>("coordinator"), new ComponentKey<ISessionRunCoordinator>("run-coordinator"), new SessionStoreKey("store"), SessionStoreCapabilities.None, false, false, new SessionRetentionProfileKey("retention"), SessionBusyBehavior.Reject, 128, 256, true, false, new ContentHash("sha256:session"));
}
