// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;

using static AgentKit.Abstractions.Tests.Artifacts.ArtifactContractTestData;

/// <summary>Verifies <see cref="ArtifactStoreReadRequest"/> validation and grant-derived evidence.</summary>
public sealed class ArtifactStoreReadRequestTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var reference = Reference();
        var grant = Grant(SecurityEffect.Observe, ArtifactSecurityBinding.ArtifactResource(reference.Id));

        var request = new ArtifactStoreReadRequest(reference, grant);

        request.Reference.ShouldBe(reference);
        request.Grant.ShouldBe(grant);
        request.Scope.ShouldBe(Scope);
        request.Identity.ShouldBe(Identity);
        request.ToString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void Constructor_WhenReferenceIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ArtifactStoreReadRequest(null!, ArtifactGrant(SecurityEffect.Observe)));
        exception.ParamName.ShouldBe("reference");
    }

    [Fact]
    public void Constructor_WhenGrantIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ArtifactStoreReadRequest(Reference(), null!));
        exception.ParamName.ShouldBe("grant");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ArtifactStoreReadRequest(Reference(), ArtifactGrant(SecurityEffect.Observe));
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
