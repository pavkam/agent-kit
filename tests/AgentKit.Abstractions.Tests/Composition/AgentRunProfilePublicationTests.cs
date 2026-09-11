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
        var security = Should.Throw<ArgumentNullException>(() => new AgentRunProfilePublication(null!, sessionProfile));
        var session = Should.Throw<ArgumentNullException>(() => new AgentRunProfilePublication(securityProfile, null!));
        security.ParamName.ShouldBe("securityProfile");
        session.ParamName.ShouldBe("sessionProfile");
    }

    [Fact]
    public void ThrowIfDuplicateAgentRunProfileCoordinates_WhenInvokedByType_ValidatesExactCoordinates()
    {
        var publication = new AgentRunProfilePublication(SecurityProfile(), SessionProfile());
        var valid = ImmutableArray.Create(publication);
        var duplicate = ImmutableArray.Create(publication, publication);
        Should.NotThrow(() => ArgumentException.ThrowIfDuplicateAgentRunProfileCoordinates(valid));
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfDuplicateAgentRunProfileCoordinates(duplicate));
        exception.ParamName.ShouldBe("duplicate");
    }

    private static SecurityProfilePublication SecurityProfile() => new(new AgentId(Guid.Parse("a0000000-0000-0000-0000-000000000001")), new AgentDefinitionRevision(1), new ConfigurationVersion(1), new SecurityProfileKey("security"), new SecurityProfileVersion(1), new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Guid.Parse("d0000000-0000-0000-0000-000000000004")), new SecurityPolicyVersion(1), new ContentHash("sha256:policy")), new ComponentKey<ISecurityAuthority>("authority"));
    private static SessionProfileSnapshot SessionProfile() => new(new SessionProfileReference(new SessionProfileKey("session"), new SessionProfileVersion(1)), new ComponentKey<ISessionCoordinator>("coordinator"), new ComponentKey<ISessionRunCoordinator>("run-coordinator"), new SessionStoreKey("store"), SessionStoreCapabilities.None, false, false, new SessionRetentionProfileKey("retention"), SessionBusyBehavior.Reject, 128, 256, true, false, new ContentHash("sha256:session"));
}
