// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies ModelResponse behavior and contracts.</summary>
public sealed class ModelResponseTests
{
    [Fact]
    public void ModelResponse_Equality_WhenSameValues_InstancesAreEqual()
    {
        var first = Response();
        var second = Response();
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    private static readonly Guid _fixedRequestGuid = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static ModelResponse Response() => new(new ModelRequestId(_fixedRequestGuid), new ProviderResponseIdentity(new ProviderId("openai"), null, new ApiFamilyId("chat"), new ModelId("gpt"), new ModelId("gpt"), null, null, null), [new TextPart("hi", TextSemantics.Plain, ExtensionData.Empty)], NormalizedStopReason.Completed, ModelUsage.NotReported, ExtensionData.Empty);
}
