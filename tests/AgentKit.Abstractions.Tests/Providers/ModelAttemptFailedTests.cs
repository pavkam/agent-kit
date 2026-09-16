// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies ModelAttemptFailed behavior and contracts.</summary>
public sealed class ModelAttemptFailedTests
{
    [Fact]
    public void ModelAttemptFailed_Equality_WhenSameValues_InstancesAreEqual()
    {
        var first = new ModelAttemptFailed(Failure(), [], null);
        var second = new ModelAttemptFailed(Failure(), [], null);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ModelAttemptFailed(Failure(), [], null);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static ProviderFailure Failure(ProviderFailureKind kind = ProviderFailureKind.Unknown, string safeMessage = "failed") => new(kind, new ProviderId("openai"), null, null, null, null, safeMessage, null, ExtensionData.Empty);
}
