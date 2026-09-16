// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies ModelResponseCancelled behavior and contracts.</summary>
public sealed class ModelResponseCancelledTests
{
    [Fact]
    public void ModelResponseCancelled_Equality_WhenSameValues_InstancesAreEqual()
    {
        var requestId = new ModelRequestId(_fixedRequestGuid);
        var cancellation = Failure(kind: ProviderFailureKind.Cancellation);
        var first = new ModelResponseCancelled(requestId, 1, cancellation, [], null);
        var second = new ModelResponseCancelled(requestId, 1, cancellation, [], null);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void ModelResponseCancelled_Constructor_WhenNotCancellationKind_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new ModelResponseCancelled(new ModelRequestId(_fixedRequestGuid), 1, Failure(), [], null));
        exception.ParamName.ShouldBe("cancellation");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ModelResponseCancelled(new ModelRequestId(_fixedRequestGuid), 1, Failure(kind: ProviderFailureKind.Cancellation), [], null);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void Equality_WhenPartialPartsArePresent_HashesEveryPart()
    {
        var part = new TextPart("partial", TextSemantics.Plain, ExtensionData.Empty);
        var requestId = new ModelRequestId(_fixedRequestGuid);
        var cancellation = Failure(kind: ProviderFailureKind.Cancellation);
        var first = new ModelResponseCancelled(requestId, 1, cancellation, [part], null);
        var second = new ModelResponseCancelled(requestId, 1, cancellation, [part], null);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    private static ProviderFailure Failure(ProviderFailureKind kind = ProviderFailureKind.Unknown, string safeMessage = "failed") => new(kind, new ProviderId("openai"), null, null, null, null, safeMessage, null, ExtensionData.Empty);
    private static readonly Guid _fixedRequestGuid = Guid.Parse("77777777-7777-7777-7777-777777777777");
}
