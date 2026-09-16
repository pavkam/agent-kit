// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies ModelAttemptResult behavior and contracts.</summary>
public sealed class ModelAttemptResultTests
{
    [Fact]
    public void ModelAttemptResult_Hierarchy_EveryLeafDerivesFromModelAttemptResult()
    {
        ModelAttemptResult completed = new ModelAttemptCompleted(Response());
        ModelAttemptResult failed = new ModelAttemptFailed(Failure(), [], null);
        ModelAttemptResult cancelled = new ModelAttemptCancelled(Failure(kind: ProviderFailureKind.Cancellation), [], null);
        _ = completed.ShouldBeOfType<ModelAttemptCompleted>();
        _ = failed.ShouldBeOfType<ModelAttemptFailed>();
        _ = cancelled.ShouldBeOfType<ModelAttemptCancelled>();
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ModelAttemptCompleted(Response());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static ProviderFailure Failure(ProviderFailureKind kind = ProviderFailureKind.Unknown, string safeMessage = "failed") => new(kind, new ProviderId("openai"), null, null, null, null, safeMessage, null, ExtensionData.Empty);
    private static readonly Guid _fixedRequestGuid = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static ModelResponse Response() => new(new ModelRequestId(_fixedRequestGuid), new ProviderResponseIdentity(new ProviderId("openai"), null, new ApiFamilyId("chat"), new ModelId("gpt"), new ModelId("gpt"), null, null, null), [new TextPart("hi", TextSemantics.Plain, ExtensionData.Empty)], NormalizedStopReason.Completed, ModelUsage.NotReported, ExtensionData.Empty);
}
