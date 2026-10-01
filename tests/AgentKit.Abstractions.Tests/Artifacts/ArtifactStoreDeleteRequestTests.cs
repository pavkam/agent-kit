// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;

using static AgentKit.Abstractions.Tests.Artifacts.ArtifactContractTestData;

/// <summary>Verifies <see cref="ArtifactStoreDeleteRequest"/> validation and grant-derived evidence.</summary>
public sealed class ArtifactStoreDeleteRequestTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var reference = Reference();
        var grant = ArtifactGrant(SecurityEffect.Delete);
        var key = new IdempotencyKey("delete");

        var request = new ArtifactStoreDeleteRequest(reference, grant, key);

        request.Reference.ShouldBe(reference);
        request.Grant.ShouldBe(grant);
        request.Scope.ShouldBe(Scope);
        request.Identity.ShouldBe(Identity);
        request.IdempotencyKey.ShouldBe(key);
    }

    [Fact]
    public void Constructor_WhenReferenceIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ArtifactStoreDeleteRequest(null!, ArtifactGrant(SecurityEffect.Delete), new IdempotencyKey("delete")));
        exception.ParamName.ShouldBe("reference");
    }

    [Fact]
    public void Constructor_WhenGrantIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ArtifactStoreDeleteRequest(Reference(), null!, new IdempotencyKey("delete")));
        exception.ParamName.ShouldBe("grant");
    }

    [Fact]
    public void Constructor_WhenIdempotencyKeyIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new ArtifactStoreDeleteRequest(Reference(), ArtifactGrant(SecurityEffect.Delete), default));
        exception.ParamName.ShouldBe("idempotencyKey");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ArtifactStoreDeleteRequest(Reference(), ArtifactGrant(SecurityEffect.Delete), new IdempotencyKey("delete"));
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
