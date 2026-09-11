// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies EmbeddingItemOutcome behavior and contracts.</summary>
public sealed class EmbeddingItemOutcomeTests
{
    [Fact]
    public void EmbeddingItemOutcome_Hierarchy_EveryLeafDerivesFromEmbeddingItemOutcome()
    {
        EmbeddingItemOutcome succeeded = new EmbeddingItemSucceeded(0, null, new DenseFloatVector([1.0f]), Space(), ExtensionData.Empty);
        EmbeddingItemOutcome failed = new EmbeddingItemFailed(1, null, Failure());
        _ = succeeded.ShouldBeOfType<EmbeddingItemSucceeded>();
        _ = failed.ShouldBeOfType<EmbeddingItemFailed>();
        succeeded.InputIndex.ShouldBe(0);
        failed.InputIndex.ShouldBe(1);
    }

    private static ProviderResponseIdentity ProviderIdentity() => new(new ProviderId("openai"), null, new ApiFamilyId("openai"), new ModelId("text-embedding-3-small"), new ModelId("text-embedding-3-small"), null, null, null);
    private static EmbeddingSpaceIdentity Space() => new(ProviderIdentity(), 3, EmbeddingElementType.Float32, EmbeddingPurpose.Document, ExtensionData.Empty);
    private static ProviderFailure Failure(ProviderFailureKind kind = ProviderFailureKind.Unknown, string safeMessage = "failed") => new(kind, new ProviderId("openai"), null, null, null, null, safeMessage, null, ExtensionData.Empty);
}
