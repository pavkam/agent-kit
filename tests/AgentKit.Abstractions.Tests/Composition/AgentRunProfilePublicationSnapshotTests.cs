// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Composition;



/// <summary>Verifies AgentRunProfilePublicationSnapshot behavior and contracts.</summary>
public sealed class AgentRunProfilePublicationSnapshotTests
{
    [Fact]
    public void AgentRunProfilePublicationSnapshot_WhenArrayIsInvalid_ThrowsWithParameterName()
    {
        var uninitialized = Should.Throw<ArgumentException>(() => new AgentRunProfilePublicationSnapshot(default));
        var containingNull = Should.Throw<ArgumentException>(() => new AgentRunProfilePublicationSnapshot([null!]));
        uninitialized.ParamName.ShouldBe("publications");
        containingNull.ParamName.ShouldBe("publications");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsPublications()
    {
        var publication = new AgentRunProfilePublication(SecurityProfile(), SessionProfile(), Hook, Budget, Configuration());
        var snapshot = new AgentRunProfilePublicationSnapshot([publication]);
        snapshot.Publications.ShouldBe([publication]);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new AgentRunProfilePublicationSnapshot([new AgentRunProfilePublication(SecurityProfile(), SessionProfile(), Hook, Budget, Configuration())]);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static EffectiveConfigurationSnapshot Configuration() => new(new ConfigurationVersion(1), new ContentHash("sha256:session"), [], []);
    private static HookProfileKey Hook { get; } = new("default");
    private static BudgetProfileKey Budget { get; } = new("budget");
    private static SecurityProfilePublication SecurityProfile() => new(new AgentId(Guid.Parse("a0000000-0000-0000-0000-000000000001")), new AgentDefinitionRevision(1), new ConfigurationVersion(1), new SecurityProfileKey("security"), new SecurityProfileVersion(1), new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Guid.Parse("d0000000-0000-0000-0000-000000000004")), new SecurityPolicyVersion(1), new ContentHash("sha256:policy")), new ComponentKey<ISecurityAuthority>("authority"));
    private static SessionProfileSnapshot SessionProfile() => new(new SessionProfileReference(new SessionProfileKey("session"), new SessionProfileVersion(1)), new ComponentKey<ISessionCoordinator>("coordinator"), new ComponentKey<ISessionRunCoordinator>("run-coordinator"), new SessionStoreKey("store"), SessionStoreCapabilities.None, false, false, new SessionRetentionProfileKey("retention"), SessionBusyBehavior.Reject, 128, 256, true, false, new ContentHash("sha256:session"));
}
