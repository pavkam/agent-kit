// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies EmbeddingRequestContext behavior and contracts.</summary>
public sealed class EmbeddingRequestContextTests
{
    private static EmbeddingModelDescriptor CreateDescriptor() => new(new EmbeddingModelAlias("default"), new ProviderId("test-provider"), new ApiFamilyId("test-api"), new ModelId("test-embedding-model"), deploymentId: null, new EmbeddingCapabilities(true, true, true, true, true, ExtensionData.Empty), new EmbeddingLimits(96, 8192, 1536, 3072), pricing: null, ExtensionData.Empty);
    private static EmbeddingRequest CreateEmbeddingRequest() => new([new TextEmbeddingInput("hello world", null)], EmbeddingPurpose.Document, dimensions: null, encoding: null, EmbeddingTruncation.ProviderDefault, ExtensionData.Empty);
    [Fact]
    public void EmbeddingRequestContext_Constructor_WhenModelNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new EmbeddingRequestContext(new EmbeddingRequestId(Guid.NewGuid()), null!, CreateEmbeddingRequest()));
        exception.ParamName.ShouldBe("model");
    }

    [Fact]
    public void EmbeddingRequestContext_Constructor_WhenRequestNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new EmbeddingRequestContext(new EmbeddingRequestId(Guid.NewGuid()), CreateDescriptor(), null!));
        exception.ParamName.ShouldBe("request");
    }

    private static EmbeddingModelRequest CreateRequest()
    {
        var context = new EmbeddingRequestContext(new EmbeddingRequestId(new Guid("dc591d0d-5ae3-47ee-8255-e1357764fc0e")), CreateDescriptor(), CreateEmbeddingRequest());
        return new EmbeddingModelRequest(context, attempt: 1, DateTimeOffset.UnixEpoch.AddMinutes(1), ProviderRequestOptions.Empty);
    }

    [Fact]
    public void WithExpression_WhenContextModelIsNull_ThrowsArgumentNullException()
    {
        var context = CreateRequest().Context;
        var exception = Should.Throw<ArgumentNullException>(() => _ = context with { Model = null! });
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void WithExpression_WhenContextRequestIsNull_ThrowsArgumentNullException()
    {
        var context = CreateRequest().Context;
        var exception = Should.Throw<ArgumentNullException>(() => _ = context with { Request = null! });
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var context = CreateRequest().Context;
        context.Model.ShouldBe(CreateDescriptor());
        context.Request.ShouldBe(CreateEmbeddingRequest());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = CreateRequest().Context;
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
