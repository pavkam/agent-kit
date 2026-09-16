// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;

using AgentKit;

/// <summary>Verifies AssistantResponseMetadata behavior and contracts.</summary>
public sealed class AssistantResponseMetadataTests
{
    private static ProviderResponseIdentity CreateIdentity() => new(new ProviderId("openai"), null, new ApiFamilyId("chat-completions"), new ModelId("gpt"), new ModelId("gpt"), null, null, null);
    [Fact]
    public void AssistantResponseMetadata_WhenResponseIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new AssistantResponseMetadata(new ModelRequestId(Guid.NewGuid()), null!, NormalizedStopReason.Completed, null, ModelUsage.NotReported, ExtensionData.Empty));
        exception.ParamName.ShouldBe("response");
    }

    [Fact]
    public void AssistantResponseMetadata_WhenUsageIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new AssistantResponseMetadata(new ModelRequestId(Guid.NewGuid()), CreateIdentity(), NormalizedStopReason.Completed, null, null!, ExtensionData.Empty));
        exception.ParamName.ShouldBe("usage");
    }

    [Fact]
    public void AssistantResponseMetadata_WhenArgumentsAreValid_ExposesStopReason()
    {
        var metadata = new AssistantResponseMetadata(new ModelRequestId(Guid.NewGuid()), CreateIdentity(), NormalizedStopReason.ToolUse, "tool_calls", ModelUsage.NotReported, ExtensionData.Empty);
        metadata.StopReason.ShouldBe(NormalizedStopReason.ToolUse);
        metadata.RawStopReason.ShouldBe("tool_calls");
    }

    private static readonly Guid _messageGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");
    [Fact]
    public void AssistantResponseMetadata_Equality_WhenSameValues_InstancesAreEqual()
    {
        var metadata = ResponseMetadata();
        metadata.ShouldBe(ResponseMetadata());
        metadata.Response.ShouldBe(ResponseIdentity());
        metadata.Usage.ShouldBe(ModelUsage.NotReported);
        metadata.Extensions.ShouldBe(ExtensionData.Empty);
    }
    private static ProviderResponseIdentity ResponseIdentity() => new(new ProviderId("openai"), null, new ApiFamilyId("chat"), new ModelId("gpt"), new ModelId("gpt"), null, null, null);
    private static AssistantResponseMetadata ResponseMetadata() => new(new ModelRequestId(_messageGuid), ResponseIdentity(), NormalizedStopReason.Completed, null, ModelUsage.NotReported, ExtensionData.Empty);

    [Fact]
    public void With_WhenResponseIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => ResponseMetadata() with { Response = null! });

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void With_WhenUsageIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => ResponseMetadata() with { Usage = null! });

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void With_WhenExtensionsIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => ResponseMetadata() with { Extensions = null! });

        exception.ParamName.ShouldBe("value");
    }
}
