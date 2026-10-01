// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;

using static AgentKit.Abstractions.Tests.Artifacts.ArtifactContractTestData;

/// <summary>Verifies <see cref="ArtifactStoreFinalizeRequest"/> validation and grant-derived evidence.</summary>
public sealed class ArtifactStoreFinalizeRequestTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var grant = PreparationGrant(SecurityEffect.CreateOrReplace);
        var key = new IdempotencyKey("finalize");

        var request = new ArtifactStoreFinalizeRequest(PreparationId, grant, key);

        request.PreparationId.ShouldBe(PreparationId);
        request.Grant.ShouldBe(grant);
        request.Scope.ShouldBe(Scope);
        request.Identity.ShouldBe(Identity);
        request.IdempotencyKey.ShouldBe(key);
    }

    [Fact]
    public void Constructor_WhenPreparationIdIsEmpty_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactStoreFinalizeRequest(default, PreparationGrant(SecurityEffect.CreateOrReplace), new IdempotencyKey("finalize")));
        exception.ParamName.ShouldBe("preparationId");
    }

    [Fact]
    public void Constructor_WhenGrantIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ArtifactStoreFinalizeRequest(PreparationId, null!, new IdempotencyKey("finalize")));
        exception.ParamName.ShouldBe("grant");
    }

    [Fact]
    public void Constructor_WhenIdempotencyKeyIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new ArtifactStoreFinalizeRequest(PreparationId, PreparationGrant(SecurityEffect.CreateOrReplace), default));
        exception.ParamName.ShouldBe("idempotencyKey");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ArtifactStoreFinalizeRequest(PreparationId, PreparationGrant(SecurityEffect.CreateOrReplace), new IdempotencyKey("finalize"));
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
