// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies ModelResponseCompleted behavior and contracts.</summary>
public sealed class ModelResponseCompletedTests
{
    [Fact]
    public void ModelResponseCompleted_Constructor_WhenResponseNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ModelResponseCompleted(new ModelRequestId(Guid.NewGuid()), 1, null!));
        exception.ParamName.ShouldBe("response");
    }

    [Fact]
    public void ModelResponseCompleted_Equality_WhenSameValues_InstancesAreEqual()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        new ModelResponseCompleted(requestId, 1, Response()).ShouldBe(new ModelResponseCompleted(requestId, 1, Response()));
    }

    private static readonly Guid _fixedRequestGuid = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static ModelResponse Response() => new(new ModelRequestId(_fixedRequestGuid), new ProviderResponseIdentity(new ProviderId("openai"), null, new ApiFamilyId("chat"), new ModelId("gpt"), new ModelId("gpt"), null, null, null), [new TextPart("hi", TextSemantics.Plain, ExtensionData.Empty)], NormalizedStopReason.Completed, ModelUsage.NotReported, ExtensionData.Empty);
}
