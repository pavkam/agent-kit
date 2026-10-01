// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;

using static AgentKit.Abstractions.Tests.Artifacts.ArtifactContractTestData;

/// <summary>Verifies <see cref="ArtifactStoreAbortRequest"/> validation and grant-derived evidence.</summary>
public sealed class ArtifactStoreAbortRequestTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var grant = PreparationGrant(SecurityEffect.Delete);
        var key = new IdempotencyKey("abort");

        var request = new ArtifactStoreAbortRequest(PreparationId, ArtifactAbortReason.Cancelled, grant, key);

        request.PreparationId.ShouldBe(PreparationId);
        request.Reason.ShouldBe(ArtifactAbortReason.Cancelled);
        request.Grant.ShouldBe(grant);
        request.Scope.ShouldBe(Scope);
        request.Identity.ShouldBe(Identity);
        request.IdempotencyKey.ShouldBe(key);
    }

    [Fact]
    public void Constructor_WhenPreparationIdIsEmpty_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactStoreAbortRequest(default, ArtifactAbortReason.Cancelled, PreparationGrant(SecurityEffect.Delete), new IdempotencyKey("abort")));
        exception.ParamName.ShouldBe("preparationId");
    }

    [Fact]
    public void Constructor_WhenReasonIsUndefined_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactStoreAbortRequest(PreparationId, (ArtifactAbortReason) 999, PreparationGrant(SecurityEffect.Delete), new IdempotencyKey("abort")));
        exception.ParamName.ShouldBe("reason");
    }

    [Fact]
    public void Constructor_WhenGrantIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ArtifactStoreAbortRequest(PreparationId, ArtifactAbortReason.Cancelled, null!, new IdempotencyKey("abort")));
        exception.ParamName.ShouldBe("grant");
    }

    [Fact]
    public void Constructor_WhenIdempotencyKeyIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new ArtifactStoreAbortRequest(PreparationId, ArtifactAbortReason.Cancelled, PreparationGrant(SecurityEffect.Delete), default));
        exception.ParamName.ShouldBe("idempotencyKey");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ArtifactStoreAbortRequest(PreparationId, ArtifactAbortReason.Cancelled, PreparationGrant(SecurityEffect.Delete), new IdempotencyKey("abort"));
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
