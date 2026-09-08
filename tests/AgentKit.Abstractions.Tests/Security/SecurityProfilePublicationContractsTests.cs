// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;

/// <summary>Verifies immutable security-profile publication and capture result contracts.</summary>
public sealed class SecurityProfilePublicationContractsTests
{
    [Fact]
    public void Constructor_WhenPublicationPartIsInvalid_ThrowsWithExactParameterName()
    {
        AssertExact<ArgumentOutOfRangeException>(() => NewPublication(
            default,
            DefinitionRevision(),
            ConfigurationVersion(),
            ProfileKey(),
            ProfileVersion(),
            PolicySnapshot(),
            AuthorityKey()), "agentId");
        AssertExact<ArgumentOutOfRangeException>(
            () => NewPublication(AgentId(), DefinitionRevision(), default, ProfileKey(), ProfileVersion(), PolicySnapshot(), AuthorityKey()),
            "configurationVersion");
        AssertExact<ArgumentNullException>(
            () => NewPublication(AgentId(), DefinitionRevision(), ConfigurationVersion(), default, ProfileVersion(), PolicySnapshot(), AuthorityKey()),
            "profileKey");
        AssertExact<ArgumentOutOfRangeException>(
            () => NewPublication(AgentId(), DefinitionRevision(), ConfigurationVersion(), ProfileKey(), default, PolicySnapshot(), AuthorityKey()),
            "profileVersion");
        AssertExact<ArgumentNullException>(
            () => NewPublication(AgentId(), DefinitionRevision(), ConfigurationVersion(), ProfileKey(), ProfileVersion(), null!, AuthorityKey()),
            "policySnapshot");
        AssertExact<ArgumentNullException>(
            () => NewPublication(AgentId(), DefinitionRevision(), ConfigurationVersion(), ProfileKey(), ProfileVersion(), PolicySnapshot(), default),
            "authorityKey");
    }

    [Fact]
    public void Constructor_WhenResultPartIsInvalid_ThrowsWithExactParameterName()
    {
        AssertExact<ArgumentNullException>(() => new SecurityProfilePublicationFound(null!), "publication");
        AssertExact<ArgumentNullException>(() => new SecurityProfilePublicationUnavailable(null!), "safeReason");
        AssertExact<ArgumentException>(() => new SecurityProfilePublicationUnavailable(" "), "safeReason");
        AssertExact<ArgumentNullException>(() => new SecurityAuthorizationCaptured(null!), "authorization");
        AssertExact<ArgumentNullException>(() => new SecurityAuthorizationCaptureUnavailable(null!), "safeReason");
        AssertExact<ArgumentException>(() => new SecurityAuthorizationCaptureUnavailable(" "), "safeReason");
    }

    [Fact]
    public void Values_WhenConstructed_PreserveImmutableStructuralEvidence()
    {
        var publication = Publication();
        var equal = Publication();
        var found = new SecurityProfilePublicationFound(publication);

        publication.ShouldBe(equal);
        publication.PolicySnapshot.ShouldBeSameAs(found.Publication.PolicySnapshot);
        typeof(SecurityProfilePublication).GetProperties().ShouldAllBe(static property => property.SetMethod == null);
        typeof(SecurityProfilePublicationFound).GetProperties().ShouldAllBe(static property => property.SetMethod == null);
        typeof(SecurityAuthorizationCaptured).GetProperties().ShouldAllBe(static property => property.SetMethod == null);
    }

    private static SecurityProfilePublication Publication() => NewPublication(
        AgentId(),
        DefinitionRevision(),
        ConfigurationVersion(),
        ProfileKey(),
        ProfileVersion(),
        PolicySnapshot(),
        AuthorityKey());

    private static SecurityProfilePublication NewPublication(
        AgentId agentId,
        AgentDefinitionRevision agentDefinitionRevision,
        ConfigurationVersion configurationVersion,
        SecurityProfileKey profileKey,
        SecurityProfileVersion profileVersion,
        SecurityPolicySnapshotReference policySnapshot,
        ComponentKey<ISecurityAuthority> authorityKey) => new(
        agentId,
        agentDefinitionRevision,
        configurationVersion,
        profileKey,
        profileVersion,
        policySnapshot,
        authorityKey);

    private static AgentId AgentId() => new(Guid.Parse("a1111111-1111-1111-1111-111111111111"));
    private static AgentDefinitionRevision DefinitionRevision() => new(2);
    private static ConfigurationVersion ConfigurationVersion() => new(3);
    private static SecurityProfileKey ProfileKey() => new("security.primary");
    private static SecurityProfileVersion ProfileVersion() => new(4);
    private static SecurityPolicySnapshotReference PolicySnapshot() => new(
            new SecurityPolicySnapshotId(Guid.Parse("a2222222-2222-2222-2222-222222222222")),
            new SecurityPolicyVersion(5),
            new ContentHash("sha256:policy"));
    private static ComponentKey<ISecurityAuthority> AuthorityKey() => new("authority.primary");

    private static void AssertExact<TException>(Func<object?> factory, string parameterName)
        where TException : ArgumentException
    {
        var exception = Should.Throw<TException>(() => _ = factory());
        exception.GetType().ShouldBe(typeof(TException));
        exception.ParamName.ShouldBe(parameterName);
    }
}
