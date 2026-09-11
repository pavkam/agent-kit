// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;

public sealed class SecurityProfilePublicationFoundTests
{
    [Fact]
    public void Constructor_WhenPublicationIsNull_ThrowsWithExactParameterName()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SecurityProfilePublicationFound(null!));
        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("publication");
    }

    [Fact]
    public void Publication_WhenConstructed_RetainsEvidenceWithGetOnlyApi()
    {
        var publication = new SecurityProfilePublication(
            new AgentId(Guid.Parse("a1111111-1111-1111-1111-111111111111")),
            new AgentDefinitionRevision(2), new ConfigurationVersion(3),
            new SecurityProfileKey("security.primary"), new SecurityProfileVersion(4),
            new SecurityPolicySnapshotReference(
                new SecurityPolicySnapshotId(Guid.Parse("a2222222-2222-2222-2222-222222222222")),
                new SecurityPolicyVersion(5), new ContentHash("sha256:policy")),
            new ComponentKey<ISecurityAuthority>("authority.primary"));

        var found = new SecurityProfilePublicationFound(publication);

        found.Publication.ShouldBeSameAs(publication);
        found.Publication.PolicySnapshot.ShouldBeSameAs(publication.PolicySnapshot);
        typeof(SecurityProfilePublicationFound).GetProperties().ShouldAllBe(static property => property.SetMethod == null);
    }
}
