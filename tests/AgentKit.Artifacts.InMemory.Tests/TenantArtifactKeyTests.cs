// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.InMemory.Tests;



/// <summary>Verifies TenantArtifactKey behavior and contracts.</summary>
public sealed class TenantArtifactKeyTests
{
    [Fact]
    public void Keys_WhenValuesMatch_AreEqualOnlyWithinSameTenantPartition()
    {
        var artifact = new TenantArtifactKey(TenantId(), ArtifactId(), Version());
        var equivalentArtifact = new TenantArtifactKey(TenantId(), ArtifactId(), Version());
        var otherTenantArtifact = new TenantArtifactKey(new TenantId("other"), ArtifactId(), Version());
        equivalentArtifact.ShouldBe(artifact);
        otherTenantArtifact.ShouldNotBe(artifact);
    }

    [Fact]
    public void Properties_WhenConstructed_ExposeTheExactCapturedValues()
    {
        var key = new TenantArtifactKey(TenantId(), ArtifactId(), Version());

        key.TenantId.ShouldBe(TenantId());
        key.ArtifactId.ShouldBe(ArtifactId());
        key.Version.ShouldBe(Version());
    }

    private static TenantId TenantId() => new("tenant");
    private static ArtifactId ArtifactId() => new(Guid.Parse("20000000-0000-0000-0000-000000000002"));
    private static ArtifactVersion Version() => new("1");
}
