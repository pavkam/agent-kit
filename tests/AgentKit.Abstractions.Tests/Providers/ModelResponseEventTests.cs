// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies ModelResponseEvent behavior and contracts.</summary>
public sealed class ModelResponseEventTests
{
    [Fact]
    public void ModelResponseEvent_Hierarchy_EveryLeafDerivesFromModelResponseEvent()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        ModelResponseEvent started = new ModelResponseStarted(requestId, 1);
        ModelResponseEvent completed = new ModelResponseCompleted(requestId, 2, Response());
        ModelResponseEvent failed = new ModelResponseFailed(requestId, 2, Failure(), [], null);
        ModelResponseEvent cancelled = new ModelResponseCancelled(requestId, 2, Failure(kind: ProviderFailureKind.Cancellation), [], null);
        _ = started.ShouldBeOfType<ModelResponseStarted>();
        _ = completed.ShouldBeOfType<ModelResponseCompleted>();
        _ = failed.ShouldBeOfType<ModelResponseFailed>();
        _ = cancelled.ShouldBeOfType<ModelResponseCancelled>();
    }

    private static ProviderFailure Failure(ProviderFailureKind kind = ProviderFailureKind.Unknown, string safeMessage = "failed") => new(kind, new ProviderId("openai"), null, null, null, null, safeMessage, null, ExtensionData.Empty);
    private static readonly Guid _fixedRequestGuid = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static ModelResponse Response() => new(new ModelRequestId(_fixedRequestGuid), new ProviderResponseIdentity(new ProviderId("openai"), null, new ApiFamilyId("chat"), new ModelId("gpt"), new ModelId("gpt"), null, null, null), [new TextPart("hi", TextSemantics.Plain, ExtensionData.Empty)], NormalizedStopReason.Completed, ModelUsage.NotReported, ExtensionData.Empty);
}
