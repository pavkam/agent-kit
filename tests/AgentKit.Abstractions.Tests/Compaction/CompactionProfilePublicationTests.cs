// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Compaction;

using AgentKit.TestSupport;

/// <summary>Verifies CompactionProfilePublication behavior and contracts.</summary>
public sealed class CompactionProfilePublicationTests
{
    [Fact]
    public void Constructor_WhenPolicyIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new CompactionProfilePublication(null!, enabled: true))
            .ParamName.ShouldBe("policy");

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Constructor_WhenArgumentsAreValid_RoundTripsPolicyAndEnabled(bool enabled)
    {
        var policy = CompactionPolicyFixtures.Create();

        var publication = new CompactionProfilePublication(policy, enabled);

        publication.Policy.ShouldBeSameAs(policy);
        publication.Enabled.ShouldBe(enabled);
    }

    [Fact]
    public void ProfileKey_WhenQueried_ReturnsThePolicyProfileKey()
    {
        var key = new CompactionProfileKey("other-profile");

        var publication = CompactionPolicyFixtures.Publication(profileKey: key);

        publication.ProfileKey.ShouldBe(key);
    }

    [Fact]
    public void CompactorKey_WhenQueried_ReturnsThePolicyCompactorKey()
    {
        var key = new ComponentKey<ICompactor>("other-compactor");

        var publication = CompactionPolicyFixtures.Publication(compactorKey: key);

        publication.CompactorKey.ShouldBe(key);
    }

    [Fact]
    public void Equality_WhenSamePolicyAndEnabled_InstancesAreEqual()
    {
        var policy = CompactionPolicyFixtures.Create();

        new CompactionProfilePublication(policy, true).ShouldBe(new CompactionProfilePublication(policy, true));
    }
}
