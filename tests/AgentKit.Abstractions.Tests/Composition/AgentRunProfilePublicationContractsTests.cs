// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Composition;

public sealed class AgentRunProfilePublicationContractsTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AgentDefinition_WhenSelectedProfileIsBlank_ThrowsWithExactParameterName(
        bool securityProfile)
    {
        var security = securityProfile ? default : new SecurityProfileKey("security");
        var session = securityProfile ? new SessionProfileKey("session") : default;

        var exception = Should.Throw<ArgumentException>(() => Definition(security, session));

        exception.ParamName.ShouldBe(securityProfile ? "securityProfile" : "sessionProfile");
    }

    [Fact]
    public void AgentDefinition_WhenProfilesAreExplicit_PreservesSelections()
    {
        var security = new SecurityProfileKey("security");
        var session = new SessionProfileKey("session");

        var definition = Definition(security, session);

        definition.SecurityProfile.ShouldBe(security);
        definition.SessionProfile.ShouldBe(session);
    }

    [Fact]
    public void AgentRunProfilePublication_WhenArgumentsAreNull_ThrowsWithExactParameterNames()
    {
        var sessionProfile = SessionProfile();
        var securityProfile = SecurityProfile();

        var security = Should.Throw<ArgumentNullException>(
            () => new AgentRunProfilePublication(null!, sessionProfile));
        var session = Should.Throw<ArgumentNullException>(
            () => new AgentRunProfilePublication(securityProfile, null!));

        security.ParamName.ShouldBe("securityProfile");
        session.ParamName.ShouldBe("sessionProfile");
    }

    [Fact]
    public void AgentRunProfilePublicationSnapshot_WhenArrayIsInvalid_ThrowsWithParameterName()
    {
        var uninitialized = Should.Throw<ArgumentException>(
            () => new AgentRunProfilePublicationSnapshot(default));
        var containingNull = Should.Throw<ArgumentException>(
            () => new AgentRunProfilePublicationSnapshot([null!]));

        uninitialized.ParamName.ShouldBe("publications");
        containingNull.ParamName.ShouldBe("publications");
    }

    [Fact]
    public void ThrowIfDuplicateAgentRunProfileCoordinates_WhenInvokedByType_ValidatesExactCoordinates()
    {
        var publication = new AgentRunProfilePublication(SecurityProfile(), SessionProfile());
        var valid = ImmutableArray.Create(publication);
        var duplicate = ImmutableArray.Create(publication, publication);

        Should.NotThrow(() => ArgumentException.ThrowIfDuplicateAgentRunProfileCoordinates(valid));
        var exception = Should.Throw<ArgumentException>(
            () => ArgumentException.ThrowIfDuplicateAgentRunProfileCoordinates(duplicate));

        exception.ParamName.ShouldBe("duplicate");
    }

    private static AgentDefinition Definition(
        SecurityProfileKey securityProfile,
        SessionProfileKey sessionProfile) => new(
        new AgentId(Guid.Parse("a0000000-0000-0000-0000-000000000001")),
        new AgentDefinitionRevision(1),
        "agent",
        new ModelSelectionPolicy([new ModelAlias("chat")]),
        ModelRequirements.None,
        [],
        [],
        LlmToolChoice.Auto,
        LlmRequestSettings.Default,
        new RunPolicyDefaults(8, TimeSpan.FromMinutes(1)),
        ExtensionData.Empty,
        securityProfile,
        sessionProfile);

    private static SecurityProfilePublication SecurityProfile() => new(
        new AgentId(Guid.Parse("a0000000-0000-0000-0000-000000000001")),
        new AgentDefinitionRevision(1),
        new ConfigurationVersion(1),
        new SecurityProfileKey("security"),
        new SecurityProfileVersion(1),
        new SecurityPolicySnapshotReference(
            new SecurityPolicySnapshotId(Guid.Parse("d0000000-0000-0000-0000-000000000004")),
            new SecurityPolicyVersion(1),
            new ContentHash("sha256:policy")),
        new ComponentKey<ISecurityAuthority>("authority"));

    private static SessionProfileSnapshot SessionProfile() => new(
        new SessionProfileReference(new SessionProfileKey("session"), new SessionProfileVersion(1)),
        new ComponentKey<ISessionCoordinator>("coordinator"),
        new ComponentKey<ISessionRunCoordinator>("run-coordinator"),
        new SessionStoreKey("store"),
        SessionStoreCapabilities.None,
        false,
        false,
        new SessionRetentionProfileKey("retention"),
        SessionBusyBehavior.Reject,
        128,
        256,
        true,
        false,
        new ContentHash("sha256:session"));
}
