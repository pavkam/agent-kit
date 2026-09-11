// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies ModelResponseFailed behavior and contracts.</summary>
public sealed class ModelResponseFailedTests
{
    [Fact]
    public void ModelResponseFailed_Equality_WhenSameValues_InstancesAreEqual()
    {
        var requestId = new ModelRequestId(_fixedRequestGuid);
        var first = new ModelResponseFailed(requestId, 1, Failure(), [], null);
        var second = new ModelResponseFailed(requestId, 1, Failure(), [], null);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    private static ProviderFailure Failure(ProviderFailureKind kind = ProviderFailureKind.Unknown, string safeMessage = "failed") => new(kind, new ProviderId("openai"), null, null, null, null, safeMessage, null, ExtensionData.Empty);
    private static readonly Guid _fixedRequestGuid = Guid.Parse("77777777-7777-7777-7777-777777777777");
}
