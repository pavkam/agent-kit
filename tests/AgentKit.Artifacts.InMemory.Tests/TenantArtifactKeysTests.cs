// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.InMemory.Tests;

public sealed class TenantArtifactKeysTests
{
    [Theory]
    [InlineData("preparation", 0, "tenantId", typeof(ArgumentNullException))]
    [InlineData("preparation", 1, "preparationId", typeof(ArgumentOutOfRangeException))]
    [InlineData("artifact", 0, "tenantId", typeof(ArgumentNullException))]
    [InlineData("artifact", 1, "artifactId", typeof(ArgumentOutOfRangeException))]
    [InlineData("artifact", 2, "version", typeof(ArgumentNullException))]
    public void Constructor_WhenValueIsDefault_ThrowsExactParameter(
        string keyKind, int invalidIndex, string expectedParameter, Type expectedExceptionType)
    {
        Action action = (keyKind, invalidIndex) switch
        {
            ("preparation", 0) => () => _ = new TenantArtifactPreparationKey(default, PreparationId()),
            ("preparation", 1) => () => _ = new TenantArtifactPreparationKey(TenantId(), default),
            ("artifact", 0) => () => _ = new TenantArtifactKey(default, ArtifactId(), Version()),
            ("artifact", 1) => () => _ = new TenantArtifactKey(TenantId(), default, Version()),
            ("artifact", 2) => () => _ = new TenantArtifactKey(TenantId(), ArtifactId(), default),
            _ => throw new ArgumentOutOfRangeException(nameof(invalidIndex)),
        };

        var exception = Should.Throw<ArgumentException>(action);

        exception.GetType().ShouldBe(expectedExceptionType);
        exception.ParamName.ShouldBe(expectedParameter);
    }

    [Fact]
    public void Keys_WhenValuesMatch_AreEqualOnlyWithinSameTenantPartition()
    {
        var preparation = new TenantArtifactPreparationKey(TenantId(), PreparationId());
        var equivalentPreparation = new TenantArtifactPreparationKey(TenantId(), PreparationId());
        var otherTenantPreparation = new TenantArtifactPreparationKey(new TenantId("other"), PreparationId());
        var artifact = new TenantArtifactKey(TenantId(), ArtifactId(), Version());
        var equivalentArtifact = new TenantArtifactKey(TenantId(), ArtifactId(), Version());
        var otherTenantArtifact = new TenantArtifactKey(new TenantId("other"), ArtifactId(), Version());

        equivalentPreparation.ShouldBe(preparation);
        otherTenantPreparation.ShouldNotBe(preparation);
        equivalentArtifact.ShouldBe(artifact);
        otherTenantArtifact.ShouldNotBe(artifact);
    }

    private static TenantId TenantId() => new("tenant");
    private static ArtifactPreparationId PreparationId() => new(Guid.Parse("10000000-0000-0000-0000-000000000001"));
    private static ArtifactId ArtifactId() => new(Guid.Parse("20000000-0000-0000-0000-000000000002"));
    private static ArtifactVersion Version() => new("1");
}
