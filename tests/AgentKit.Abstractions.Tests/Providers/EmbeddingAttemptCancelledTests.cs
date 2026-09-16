// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies EmbeddingAttemptCancelled behavior and contracts.</summary>
public sealed class EmbeddingAttemptCancelledTests
{
    [Fact]
    public void EmbeddingAttemptCancelled_Constructor_WhenCancellationNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new EmbeddingAttemptCancelled(null!));
        exception.ParamName.ShouldBe("cancellation");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var cancellation = new ProviderFailure(ProviderFailureKind.Cancellation, new ProviderId("openai"), null, null, null, null, "cancelled", null, ExtensionData.Empty);
        var original = new EmbeddingAttemptCancelled(cancellation);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
