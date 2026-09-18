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

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = Response();
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void Constructor_WhenPartsContainsNull_ThrowsExactParameter()
    {
        // parts was guarded with ThrowIfDefault only, whereas every other message type (AgentMessage, AgentInput,
        // ToolResultPart) requires ThrowIfContainsNull. A provider adapter that yielded a null part produced a
        // ModelResponse that passed construction, and the failure surfaced later as a NullReferenceException
        // inside Parts.SequenceEqual, hashing, or when the loop built the AssistantMessage - far from the
        // offending adapter.
        var exception = Should.Throw<ArgumentException>(() => new ModelResponse(
            new ModelRequestId(_fixedRequestGuid),
            new ProviderResponseIdentity(new ProviderId("openai"), null, new ApiFamilyId("chat"), new ModelId("gpt"), new ModelId("gpt"), null, null, null),
            [null!],
            NormalizedStopReason.Completed,
            ModelUsage.NotReported,
            ExtensionData.Empty));

        exception.ParamName.ShouldBe("parts");
    }

    [Fact]
    public void WithExpression_WhenPartsContainsNull_ThrowsArgumentException()
    {
        var original = Response();
        var exception = Should.Throw<ArgumentException>(() => _ = original with { Parts = [null!] });
        exception.ParamName.ShouldBe("value");
    }

    private static readonly Guid _fixedRequestGuid = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static ModelResponse Response() => new(new ModelRequestId(_fixedRequestGuid), new ProviderResponseIdentity(new ProviderId("openai"), null, new ApiFamilyId("chat"), new ModelId("gpt"), new ModelId("gpt"), null, null, null), [new TextPart("hi", TextSemantics.Plain, ExtensionData.Empty)], NormalizedStopReason.Completed, ModelUsage.NotReported, ExtensionData.Empty);
}
