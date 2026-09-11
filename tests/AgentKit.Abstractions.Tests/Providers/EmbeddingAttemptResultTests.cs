// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies EmbeddingAttemptResult behavior and contracts.</summary>
public sealed class EmbeddingAttemptResultTests
{
    [Fact]
    public void EmbeddingAttemptResult_Hierarchy_EveryLeafDerivesFromEmbeddingAttemptResult()
    {
        var items = ImmutableArray.Create<EmbeddingItemOutcome>(SucceededItem());
        var response = new EmbeddingResponse(items, ModelUsage.NotReported, null, ExtensionData.Empty);
        EmbeddingAttemptResult completed = new EmbeddingAttemptCompleted(response);
        EmbeddingAttemptResult failed = new EmbeddingAttemptFailed(Failure());
        EmbeddingAttemptResult cancelled = new EmbeddingAttemptCancelled(Failure(kind: ProviderFailureKind.Cancellation));
        _ = completed.ShouldBeOfType<EmbeddingAttemptCompleted>();
        _ = failed.ShouldBeOfType<EmbeddingAttemptFailed>();
        _ = cancelled.ShouldBeOfType<EmbeddingAttemptCancelled>();
    }

    private static ProviderResponseIdentity ProviderIdentity() => new(new ProviderId("openai"), null, new ApiFamilyId("openai"), new ModelId("text-embedding-3-small"), new ModelId("text-embedding-3-small"), null, null, null);
    private static EmbeddingSpaceIdentity Space() => new(ProviderIdentity(), 3, EmbeddingElementType.Float32, EmbeddingPurpose.Document, ExtensionData.Empty);
    private static EmbeddingItemSucceeded SucceededItem() => new(0, null, new DenseFloatVector([1.0f, 2.0f, 3.0f]), Space(), ExtensionData.Empty);
    private static ProviderFailure Failure(ProviderFailureKind kind = ProviderFailureKind.Unknown, string safeMessage = "failed") => new(kind, new ProviderId("openai"), null, null, null, null, safeMessage, null, ExtensionData.Empty);
}
