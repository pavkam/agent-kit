// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;

using AgentKit;

public sealed class ResponseMetadataTests
{
    [Fact]
    public void ModelUsage_Empty_HasNoReportedCounters()
    {
        ModelUsage.Empty.InputTokens.ShouldBeNull();
        ModelUsage.Empty.OutputTokens.ShouldBeNull();
        ModelUsage.Empty.EstimatedCost.ShouldBeNull();
        ModelUsage.Empty.Extensions.ShouldBe(ExtensionData.Empty);
    }

    [Fact]
    public void ModelUsage_WhenExtensionsIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new ModelUsage(1, 1, null, null, null, null, null!));

        exception.ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void ProviderResponseIdentity_WhenConstructed_ExposesResolvedModel()
    {
        var identity = new ProviderResponseIdentity(
            new ProviderId("openai"),
            null,
            new ApiFamilyId("chat-completions"),
            new ModelId("gpt-latest"),
            new ModelId("gpt-2024-01"),
            null,
            new ProviderRequestId("req_123"),
            new ProviderResponseId("resp_456"));

        identity.RequestedModelId.ShouldBe(new ModelId("gpt-latest"));
        identity.ResolvedModelId.ShouldBe(new ModelId("gpt-2024-01"));
        identity.UpstreamProviderId.ShouldBeNull();
    }

    private static ProviderResponseIdentity CreateIdentity() => new(
        new ProviderId("openai"),
        null,
        new ApiFamilyId("chat-completions"),
        new ModelId("gpt"),
        new ModelId("gpt"),
        null,
        null,
        null);

    [Fact]
    public void AssistantResponseMetadata_WhenResponseIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new AssistantResponseMetadata(
            new ModelRequestId(Guid.NewGuid()),
            null!,
            NormalizedStopReason.Completed,
            null,
            ModelUsage.Empty,
            ExtensionData.Empty));

        exception.ParamName.ShouldBe("response");
    }

    [Fact]
    public void AssistantResponseMetadata_WhenUsageIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new AssistantResponseMetadata(
            new ModelRequestId(Guid.NewGuid()),
            CreateIdentity(),
            NormalizedStopReason.Completed,
            null,
            null!,
            ExtensionData.Empty));

        exception.ParamName.ShouldBe("usage");
    }

    [Fact]
    public void AssistantResponseMetadata_WhenArgumentsAreValid_ExposesStopReason()
    {
        var metadata = new AssistantResponseMetadata(
            new ModelRequestId(Guid.NewGuid()),
            CreateIdentity(),
            NormalizedStopReason.ToolUse,
            "tool_calls",
            ModelUsage.Empty,
            ExtensionData.Empty);

        metadata.StopReason.ShouldBe(NormalizedStopReason.ToolUse);
        metadata.RawStopReason.ShouldBe("tool_calls");
    }
}
