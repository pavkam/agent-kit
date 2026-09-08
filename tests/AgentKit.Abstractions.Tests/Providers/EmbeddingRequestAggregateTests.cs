// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

public sealed class EmbeddingRequestAggregateTests
{
    private static EmbeddingModelDescriptor CreateDescriptor() => new(
        new EmbeddingModelAlias("default"),
        new ProviderId("test-provider"),
        new ApiFamilyId("test-api"),
        new ModelId("test-embedding-model"),
        deploymentId: null,
        new EmbeddingCapabilities(true, true, true, true, true, ExtensionData.Empty),
        new EmbeddingLimits(96, 8192, 1536, 3072),
        pricing: null,
        ExtensionData.Empty);

    private static EmbeddingRequest CreateEmbeddingRequest() => new(
        [new TextEmbeddingInput("hello world", null)],
        EmbeddingPurpose.Document,
        dimensions: null,
        encoding: null,
        EmbeddingTruncation.ProviderDefault, ExtensionData.Empty);

    private static EmbeddingModelRequest CreateRequest()
    {
        var context = new EmbeddingRequestContext(
            new EmbeddingRequestId(new Guid("dc591d0d-5ae3-47ee-8255-e1357764fc0e")),
            CreateDescriptor(),
            CreateEmbeddingRequest());

        return new EmbeddingModelRequest(
            context,
            attempt: 1,
            DateTimeOffset.UnixEpoch.AddMinutes(1),
            ProviderRequestOptions.Empty);
    }

    [Fact]
    public void EmbeddingRequest_Constructor_WhenInputsEmpty_ThrowsArgumentException() =>
        _ = Should.Throw<ArgumentException>(() => new EmbeddingRequest(
            [], EmbeddingPurpose.Document, null, null, EmbeddingTruncation.ProviderDefault, ExtensionData.Empty));

    [Fact]
    public void EmbeddingRequest_Constructor_WhenInputsContainsNull_ThrowsArgumentException() =>
        _ = Should.Throw<ArgumentException>(() => new EmbeddingRequest(
            [null!], EmbeddingPurpose.Document, null, null, EmbeddingTruncation.ProviderDefault, ExtensionData.Empty));

    [Fact]
    public void EmbeddingRequest_Constructor_WhenDimensionsLessThanOne_ThrowsArgumentOutOfRangeException() =>
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new EmbeddingRequest(
            [new TextEmbeddingInput("hi", null)],
            EmbeddingPurpose.Document,
            0,
            null,
            EmbeddingTruncation.ProviderDefault,
            ExtensionData.Empty));

    [Fact]
    public void EmbeddingRequest_Constructor_WhenExtensionsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new EmbeddingRequest(
            [new TextEmbeddingInput("hi", null)],
            EmbeddingPurpose.Document,
            null,
            null,
            EmbeddingTruncation.ProviderDefault,
            null!));

        exception.ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void EmbeddingRequest_Equality_WhenSameValues_InstancesAreEqual()
    {
        var first = CreateEmbeddingRequest();
        var second = CreateEmbeddingRequest();

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void EmbeddingRequest_Equality_WhenDifferentInputs_InstancesAreNotEqual()
    {
        var first = CreateEmbeddingRequest();
        var second = first with { Inputs = [new TextEmbeddingInput("different", null)] };

        first.ShouldNotBe(second);
    }

    [Fact]
    public void EmbeddingRequestContext_Constructor_WhenModelNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new EmbeddingRequestContext(
            new EmbeddingRequestId(Guid.NewGuid()), null!, CreateEmbeddingRequest()));

        exception.ParamName.ShouldBe("model");
    }

    [Fact]
    public void EmbeddingRequestContext_Constructor_WhenRequestNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new EmbeddingRequestContext(
            new EmbeddingRequestId(Guid.NewGuid()), CreateDescriptor(), null!));

        exception.ParamName.ShouldBe("request");
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
    public void EmbeddingModelRequest_Constructor_WhenContextNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new EmbeddingModelRequest(null!, 1, DateTimeOffset.UnixEpoch, ProviderRequestOptions.Empty));

        exception.ParamName.ShouldBe("context");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void EmbeddingModelRequest_Constructor_WhenAttemptNotPositive_ThrowsArgumentOutOfRangeException(int attempt)
    {
        var context = CreateRequest().Context;

        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => new EmbeddingModelRequest(context, attempt, DateTimeOffset.UnixEpoch, ProviderRequestOptions.Empty));
    }

    [Fact]
    public void EmbeddingModelRequest_Constructor_WhenOptionsNull_ThrowsArgumentNullException()
    {
        var context = CreateRequest().Context;

        var exception = Should.Throw<ArgumentNullException>(
            () => new EmbeddingModelRequest(context, 1, DateTimeOffset.UnixEpoch, null!));

        exception.ParamName.ShouldBe("options");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void WithExpression_WhenAttemptNotPositive_ThrowsArgumentOutOfRangeException(int attempt)
    {
        var request = CreateRequest();

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => _ = request with { Attempt = attempt });

        exception.ParamName.ShouldBe("value");
        exception.ActualValue.ShouldBe(attempt);
    }

    [Fact]
    public void WithExpression_WhenOptionsIsNull_ThrowsArgumentNullException()
    {
        var request = CreateRequest();

        var exception = Should.Throw<ArgumentNullException>(() => _ = request with { Options = null! });

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void WithExpression_WhenRequestContextIsNull_ThrowsArgumentNullException()
    {
        var request = CreateRequest();

        var exception = Should.Throw<ArgumentNullException>(() => _ = request with { Context = null! });

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void WithExpression_WhenChangesAreValid_PreservesConfigurabilityAndOriginalValues()
    {
        var original = CreateRequest();
        var updatedRequest = original.Context.Request with { Dimensions = 512 };
        var updatedContext = original.Context with { Request = updatedRequest };

        var updated = original with
        {
            Context = updatedContext,
            Attempt = 2,
            Deadline = original.Deadline.AddMinutes(1),
        };

        updated.Attempt.ShouldBe(2);
        updated.Context.Request.Dimensions.ShouldBe(512);
        original.Attempt.ShouldBe(1);
        original.Context.Request.Dimensions.ShouldBeNull();
    }
}
