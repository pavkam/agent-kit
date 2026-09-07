// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

public sealed class ProviderValueTypesTests
{
    [Fact]
    public void ModelPricing_Constructor_WhenInputCostNegative_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ModelPricing(-1m, null, "USD"));

    [Fact]
    public void ModelPricing_Constructor_WhenOutputCostNegative_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ModelPricing(null, -1m, "USD"));

    [Fact]
    public void ModelPricing_Constructor_WhenValid_RoundTripsProperties()
    {
        var pricing = new ModelPricing(1.5m, 2.5m, "USD");

        pricing.InputCostPerMillionTokens.ShouldBe(1.5m);
        pricing.OutputCostPerMillionTokens.ShouldBe(2.5m);
        pricing.CostCurrency.ShouldBe("USD");
    }

    [Fact]
    public void ModelPricing_Equality_WhenSameValues_InstancesAreEqual() =>
        new ModelPricing(1m, 2m, "USD").ShouldBe(new ModelPricing(1m, 2m, "USD"));

    [Fact]
    public void ModelLimits_Constructor_WhenContextTokensNegative_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ModelLimits(-1, null));

    [Fact]
    public void ModelLimits_Constructor_WhenOutputTokensNegative_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ModelLimits(null, -1));

    [Fact]
    public void ModelLimits_Constructor_WhenValid_RoundTripsProperties()
    {
        var limits = new ModelLimits(1000, 500);

        limits.MaxContextTokens.ShouldBe(1000);
        limits.MaxOutputTokens.ShouldBe(500);
    }

    [Fact]
    public void ModelLimits_Equality_WhenSameValues_InstancesAreEqual() =>
        new ModelLimits(1000, 500).ShouldBe(new ModelLimits(1000, 500));

    [Fact]
    public void LlmToolChoice_Constructor_WhenNamedWithoutToolName_ThrowsArgumentException() => _ = Should.Throw<ArgumentException>(() => new LlmToolChoice(LlmToolChoiceMode.Named, null));

    [Fact]
    public void LlmToolChoice_Constructor_WhenNotNamedWithToolName_ThrowsArgumentException() => _ = Should.Throw<ArgumentException>(() => new LlmToolChoice(LlmToolChoiceMode.Auto, "tool"));

    [Fact]
    public void LlmToolChoice_Named_WhenToolNameInvalid_ThrowsArgumentException() => _ = Should.Throw<ArgumentException>(() => LlmToolChoice.Named(" "));

    [Fact]
    public void LlmToolChoice_Named_WhenValid_ProducesNamedChoice()
    {
        var choice = LlmToolChoice.Named("my-tool");

        choice.Mode.ShouldBe(LlmToolChoiceMode.Named);
        choice.ForcedToolName.ShouldBe("my-tool");
    }

    [Fact]
    public void LlmToolChoice_Equality_WhenSameValues_InstancesAreEqual() =>
        LlmToolChoice.Named("tool").ShouldBe(LlmToolChoice.Named("tool"));

    [Fact]
    public void LlmToolChoice_SharedInstances_HaveExpectedModes()
    {
        LlmToolChoice.Auto.Mode.ShouldBe(LlmToolChoiceMode.Auto);
        LlmToolChoice.None.Mode.ShouldBe(LlmToolChoiceMode.None);
        LlmToolChoice.Required.Mode.ShouldBe(LlmToolChoiceMode.Required);
    }

    [Fact]
    public void ApiKeyProviderCredential_Constructor_WhenApiKeyInvalid_ThrowsArgumentException() => _ = Should.Throw<ArgumentException>(() => new ApiKeyProviderCredential(" "));

    [Fact]
    public void ApiKeyProviderCredential_Equality_WhenSameValues_InstancesAreEqual() =>
        new ApiKeyProviderCredential("secret").ShouldBe(new ApiKeyProviderCredential("secret"));

    [Fact]
    public void OAuthTokenProviderCredential_Constructor_WhenTokenInvalid_ThrowsArgumentException() => _ = Should.Throw<ArgumentException>(() => new OAuthTokenProviderCredential(" ", null));

    [Fact]
    public void OAuthTokenProviderCredential_Equality_WhenSameValues_InstancesAreEqual() =>
        new OAuthTokenProviderCredential("token", null).ShouldBe(new OAuthTokenProviderCredential("token", null));

    [Fact]
    public void ProviderCredential_Hierarchy_EveryLeafDerivesFromProviderCredential()
    {
        ProviderCredential apiKey = new ApiKeyProviderCredential("k");
        ProviderCredential oauth = new OAuthTokenProviderCredential("t", null);

        _ = apiKey.ShouldBeOfType<ApiKeyProviderCredential>();
        _ = oauth.ShouldBeOfType<OAuthTokenProviderCredential>();
    }

    [Fact]
    public void ProviderFailure_Constructor_WhenSafeMessageInvalid_ThrowsArgumentException() => _ = Should.Throw<ArgumentException>(() => Failure(safeMessage: " "));

    [Fact]
    public void ProviderFailure_Constructor_WhenExtensionsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ProviderFailure(
            ProviderFailureKind.Unknown, new ProviderId("openai"), null, null, null, null, "failed", null, null!));

        exception.ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void ProviderFailure_Equality_WhenSameValues_InstancesAreEqual() =>
        Failure().ShouldBe(Failure());

    [Fact]
    public void ProviderRequestOptions_Constructor_WhenExtensionsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ProviderRequestOptions(null!));

        exception.ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void ProviderRequestOptions_Equality_WhenSameValues_InstancesAreEqual() =>
        new ProviderRequestOptions(ExtensionData.Empty).ShouldBe(new ProviderRequestOptions(ExtensionData.Empty));

    [Fact]
    public void LlmToolDefinition_Constructor_WhenNameInvalid_ThrowsArgumentException() => _ = Should.Throw<ArgumentException>(() => new LlmToolDefinition(new ToolId("t"), " ", null, default));

    [Fact]
    public void LlmToolDefinition_Equality_WhenSameValues_InstancesAreEqual() =>
        new LlmToolDefinition(new ToolId("t"), "tool", null, default).ShouldBe(
            new LlmToolDefinition(new ToolId("t"), "tool", null, default));

    [Fact]
    public void ModelAttemptCompleted_Constructor_WhenResponseNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ModelAttemptCompleted(null!));

        exception.ParamName.ShouldBe("response");
    }

    [Fact]
    public void ModelAttemptCompleted_Equality_WhenSameValues_InstancesAreEqual() =>
        new ModelAttemptCompleted(Response()).ShouldBe(new ModelAttemptCompleted(Response()));

    [Fact]
    public void ModelResponseCompleted_Constructor_WhenResponseNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new ModelResponseCompleted(new ModelRequestId(Guid.NewGuid()), 1, null!));

        exception.ParamName.ShouldBe("response");
    }

    [Fact]
    public void ModelResponseCompleted_Equality_WhenSameValues_InstancesAreEqual()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());

        new ModelResponseCompleted(requestId, 1, Response()).ShouldBe(new ModelResponseCompleted(requestId, 1, Response()));
    }

    [Fact]
    public void ModelPartCompleted_Constructor_WhenPartNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new ModelPartCompleted(new ModelRequestId(Guid.NewGuid()), 1, 0, null!));

        exception.ParamName.ShouldBe("part");
    }

    [Fact]
    public void ModelPartCompleted_Equality_WhenSameValues_InstancesAreEqual()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var part = new TextPart("hi", TextSemantics.Plain, ExtensionData.Empty);

        new ModelPartCompleted(requestId, 1, 0, part).ShouldBe(new ModelPartCompleted(requestId, 1, 0, part));
    }

    [Fact]
    public void ModelPartDelta_Constructor_WhenDeltaNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new ModelPartDelta(new ModelRequestId(Guid.NewGuid()), 1, 0, null!));

        exception.ParamName.ShouldBe("delta");
    }

    [Fact]
    public void ModelPartDelta_Equality_WhenSameValues_InstancesAreEqual()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var delta = new TextContentDelta("chunk");

        new ModelPartDelta(requestId, 1, 0, delta).ShouldBe(new ModelPartDelta(requestId, 1, 0, delta));
    }

    [Fact]
    public void ModelPartStarted_Equality_WhenSameValues_InstancesAreEqual()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());

        new ModelPartStarted(requestId, 1, 0).ShouldBe(new ModelPartStarted(requestId, 1, 0));
    }

    [Fact]
    public void ModelResponseStarted_Equality_WhenSameValues_InstancesAreEqual()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());

        new ModelResponseStarted(requestId, 1).ShouldBe(new ModelResponseStarted(requestId, 1));
    }

    [Fact]
    public void ModelUsageUpdated_Constructor_WhenUsageNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new ModelUsageUpdated(new ModelRequestId(Guid.NewGuid()), 1, null!));

        exception.ParamName.ShouldBe("usage");
    }

    [Fact]
    public void ModelUsageUpdated_Equality_WhenSameValues_InstancesAreEqual()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());

        new ModelUsageUpdated(requestId, 1, ModelUsage.Empty).ShouldBe(new ModelUsageUpdated(requestId, 1, ModelUsage.Empty));
    }

    [Fact]
    public void ModelResponseEvent_Hierarchy_EveryLeafDerivesFromModelResponseEvent()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());

        ModelResponseEvent started = new ModelResponseStarted(requestId, 1);
        ModelResponseEvent completed = new ModelResponseCompleted(requestId, 2, Response());
        ModelResponseEvent failed = new ModelResponseFailed(requestId, 2, Failure(), [], null);
        ModelResponseEvent cancelled = new ModelResponseCancelled(
            requestId, 2, Failure(kind: ProviderFailureKind.Cancellation), [], null);

        _ = started.ShouldBeOfType<ModelResponseStarted>();
        _ = completed.ShouldBeOfType<ModelResponseCompleted>();
        _ = failed.ShouldBeOfType<ModelResponseFailed>();
        _ = cancelled.ShouldBeOfType<ModelResponseCancelled>();
    }

    [Fact]
    public void ModelAttemptResult_Hierarchy_EveryLeafDerivesFromModelAttemptResult()
    {
        ModelAttemptResult completed = new ModelAttemptCompleted(Response());
        ModelAttemptResult failed = new ModelAttemptFailed(Failure(), [], null);
        ModelAttemptResult cancelled = new ModelAttemptCancelled(Failure(kind: ProviderFailureKind.Cancellation), [], null);

        _ = completed.ShouldBeOfType<ModelAttemptCompleted>();
        _ = failed.ShouldBeOfType<ModelAttemptFailed>();
        _ = cancelled.ShouldBeOfType<ModelAttemptCancelled>();
    }

    [Fact]
    public void ModelResponse_Equality_WhenSameValues_InstancesAreEqual()
    {
        var first = Response();
        var second = Response();

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void ModelResponseFailed_Equality_WhenSameValues_InstancesAreEqual()
    {
        var requestId = new ModelRequestId(_fixedRequestGuid);
        var first = new ModelResponseFailed(requestId, 1, Failure(), [], null);
        var second = new ModelResponseFailed(requestId, 1, Failure(), [], null);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void ModelResponseCancelled_Equality_WhenSameValues_InstancesAreEqual()
    {
        var requestId = new ModelRequestId(_fixedRequestGuid);
        var cancellation = Failure(kind: ProviderFailureKind.Cancellation);
        var first = new ModelResponseCancelled(requestId, 1, cancellation, [], null);
        var second = new ModelResponseCancelled(requestId, 1, cancellation, [], null);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void ModelResponseCancelled_Constructor_WhenNotCancellationKind_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new ModelResponseCancelled(new ModelRequestId(_fixedRequestGuid), 1, Failure(), [], null));

        exception.ParamName.ShouldBe("cancellation");
    }

    [Fact]
    public void ModelAttemptFailed_Equality_WhenSameValues_InstancesAreEqual()
    {
        var first = new ModelAttemptFailed(Failure(), [], null);
        var second = new ModelAttemptFailed(Failure(), [], null);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

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

    private static ProviderFailure Failure(
        ProviderFailureKind kind = ProviderFailureKind.Unknown, string safeMessage = "failed") =>
        new(kind, new ProviderId("openai"), null, null, null, null, safeMessage, null, ExtensionData.Empty);

    private static readonly Guid _fixedRequestGuid = Guid.Parse("77777777-7777-7777-7777-777777777777");

    private static ModelResponse Response() => new(
        new ModelRequestId(_fixedRequestGuid),
        new ProviderResponseIdentity(new ProviderId("openai"), null, new ApiFamilyId("chat"), new ModelId("gpt"), new ModelId("gpt"), null, null, null),
        [new TextPart("hi", TextSemantics.Plain, ExtensionData.Empty)],
        NormalizedStopReason.Completed,
        ModelUsage.Empty,
        ExtensionData.Empty);
}
