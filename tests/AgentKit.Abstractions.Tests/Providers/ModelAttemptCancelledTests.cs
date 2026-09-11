// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies ModelAttemptCancelled behavior and contracts.</summary>
public sealed class ModelAttemptCancelledTests
{
    [Fact]
    public void ModelAttemptCancelled_Equality_WhenSameValues_InstancesAreEqual()
    {
        var cancellation = Failure(kind: ProviderFailureKind.Cancellation);
        var first = new ModelAttemptCancelled(cancellation, [], null);
        var second = new ModelAttemptCancelled(cancellation, [], null);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void ModelAttemptCancelled_Constructor_WhenNotCancellationKind_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new ModelAttemptCancelled(Failure(), [], null));
        exception.ParamName.ShouldBe("cancellation");
    }

    private static ProviderFailure Failure(ProviderFailureKind kind = ProviderFailureKind.Unknown, string safeMessage = "failed") => new(kind, new ProviderId("openai"), null, null, null, null, safeMessage, null, ExtensionData.Empty);
}
