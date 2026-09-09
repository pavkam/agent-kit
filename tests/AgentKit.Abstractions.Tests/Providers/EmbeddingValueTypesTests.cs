// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

public sealed class EmbeddingValueTypesTests
{
    [Fact]
    public void EmbeddingModelAlias_Constructor_WhenValueIsWhitespace_ThrowsArgumentException() =>
        _ = Should.Throw<ArgumentException>(() => new EmbeddingModelAlias(" "));

    [Fact]
    public void EmbeddingModelAlias_Equality_WhenSameValue_InstancesAreEqual() =>
        new EmbeddingModelAlias("default").ShouldBe(new EmbeddingModelAlias("default"));

    [Fact]
    public void EmbeddingRequestId_Constructor_WhenEmptyGuid_ThrowsArgumentOutOfRangeException() =>
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new EmbeddingRequestId(Guid.Empty));

    [Fact]
    public void EmbeddingRequestId_ToString_ReturnsGuidDFormat()
    {
        var guid = Guid.Parse("11111111-1111-1111-1111-111111111111");
        new EmbeddingRequestId(guid).ToString().ShouldBe(guid.ToString("D"));
    }

    [Fact]
    public void EmbeddingInputId_Constructor_WhenEmptyGuid_ThrowsArgumentOutOfRangeException() =>
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new EmbeddingInputId(Guid.Empty));

    [Fact]
    public void TextEmbeddingInput_Constructor_WhenTextNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new TextEmbeddingInput(null!, null));
        exception.ParamName.ShouldBe("text");
    }

    [Fact]
    public void TextEmbeddingInput_Equality_WhenSameValues_InstancesAreEqual() =>
        new TextEmbeddingInput("hello", null).ShouldBe(new TextEmbeddingInput("hello", null));

    [Fact]
    public void EmbeddingInput_Hierarchy_LeafDerivesFromEmbeddingInput()
    {
        EmbeddingInput input = new TextEmbeddingInput("hello", null);
        _ = input.ShouldBeOfType<TextEmbeddingInput>();
    }

    [Fact]
    public void DenseFloatVector_Constructor_WhenValuesEmpty_ThrowsArgumentException() =>
        _ = Should.Throw<ArgumentException>(() => new DenseFloatVector([]));

    [Fact]
    public void DenseFloatVector_Equality_WhenSameValues_InstancesAreEqual()
    {
        var first = new DenseFloatVector([1.0f, 2.0f]);
        var second = new DenseFloatVector([1.0f, 2.0f]);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void DenseFloatVector_Equality_WhenDifferentValues_InstancesAreNotEqual() =>
        new DenseFloatVector([1.0f]).ShouldNotBe(new DenseFloatVector([2.0f]));

    [Fact]
    public void QuantizedByteVector_Constructor_WhenValuesEmpty_ThrowsArgumentException() =>
        _ = Should.Throw<ArgumentException>(() => new QuantizedByteVector([], signed: true));

    [Fact]
    public void QuantizedByteVector_Equality_WhenSameValues_InstancesAreEqual()
    {
        var first = new QuantizedByteVector([1, 2, 3], signed: true);
        var second = new QuantizedByteVector([1, 2, 3], signed: true);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void QuantizedByteVector_Equality_WhenDifferentSignedness_InstancesAreNotEqual() =>
        new QuantizedByteVector([1, 2], signed: true).ShouldNotBe(new QuantizedByteVector([1, 2], signed: false));

    [Fact]
    public void PackedBinaryVector_Constructor_WhenValuesEmpty_ThrowsArgumentException() =>
        _ = Should.Throw<ArgumentException>(() => new PackedBinaryVector([], signed: false));

    [Fact]
    public void PackedBinaryVector_Equality_WhenSameValues_InstancesAreEqual()
    {
        var first = new PackedBinaryVector([0b10101010], signed: false);
        var second = new PackedBinaryVector([0b10101010], signed: false);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void EmbeddingVector_Hierarchy_EveryLeafDerivesFromEmbeddingVector()
    {
        EmbeddingVector dense = new DenseFloatVector([1.0f]);
        EmbeddingVector quantized = new QuantizedByteVector([1], signed: true);
        EmbeddingVector packed = new PackedBinaryVector([1], signed: false);

        _ = dense.ShouldBeOfType<DenseFloatVector>();
        _ = quantized.ShouldBeOfType<QuantizedByteVector>();
        _ = packed.ShouldBeOfType<PackedBinaryVector>();
    }

    [Fact]
    public void EmbeddingSpaceIdentity_Constructor_WhenProviderNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new EmbeddingSpaceIdentity(null!, 3, EmbeddingElementType.Float32, EmbeddingPurpose.Document, ExtensionData.Empty));

        exception.ParamName.ShouldBe("provider");
    }

    [Fact]
    public void EmbeddingSpaceIdentity_Constructor_WhenDimensionsLessThanOne_ThrowsArgumentOutOfRangeException() =>
        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => new EmbeddingSpaceIdentity(ProviderIdentity(), 0, EmbeddingElementType.Float32, EmbeddingPurpose.Document, ExtensionData.Empty));

    [Fact]
    public void EmbeddingSpaceIdentity_Equality_WhenSameValues_InstancesAreEqual()
    {
        var first = new EmbeddingSpaceIdentity(ProviderIdentity(), 3, EmbeddingElementType.Float32, EmbeddingPurpose.Document, ExtensionData.Empty);
        var second = new EmbeddingSpaceIdentity(ProviderIdentity(), 3, EmbeddingElementType.Float32, EmbeddingPurpose.Document, ExtensionData.Empty);

        first.ShouldBe(second);
    }

    [Fact]
    public void EmbeddingItemSucceeded_Constructor_WhenInputIndexNegative_ThrowsArgumentOutOfRangeException() =>
        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => new EmbeddingItemSucceeded(-1, null, new DenseFloatVector([1.0f]), Space(), ExtensionData.Empty));

    [Fact]
    public void EmbeddingItemSucceeded_Constructor_WhenVectorNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new EmbeddingItemSucceeded(0, null, null!, Space(), ExtensionData.Empty));

        exception.ParamName.ShouldBe("vector");
    }

    [Fact]
    public void EmbeddingItemFailed_Constructor_WhenFailureNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new EmbeddingItemFailed(0, null, null!));
        exception.ParamName.ShouldBe("failure");
    }

    [Fact]
    public void EmbeddingItemOutcome_Hierarchy_EveryLeafDerivesFromEmbeddingItemOutcome()
    {
        EmbeddingItemOutcome succeeded = new EmbeddingItemSucceeded(0, null, new DenseFloatVector([1.0f]), Space(), ExtensionData.Empty);
        EmbeddingItemOutcome failed = new EmbeddingItemFailed(1, null, Failure());

        _ = succeeded.ShouldBeOfType<EmbeddingItemSucceeded>();
        _ = failed.ShouldBeOfType<EmbeddingItemFailed>();
        succeeded.InputIndex.ShouldBe(0);
        failed.InputIndex.ShouldBe(1);
    }

    [Fact]
    public void EmbeddingResponse_Constructor_WhenItemsEmpty_ThrowsArgumentException() =>
        _ = Should.Throw<ArgumentException>(() => new EmbeddingResponse([], ModelUsage.NotReported, null, ExtensionData.Empty));

    [Fact]
    public void EmbeddingResponse_Constructor_WhenUsageNull_ThrowsArgumentNullException()
    {
        var items = ImmutableArray.Create<EmbeddingItemOutcome>(SucceededItem());
        var exception = Should.Throw<ArgumentNullException>(() => new EmbeddingResponse(items, null!, null, ExtensionData.Empty));

        exception.ParamName.ShouldBe("usage");
    }

    [Fact]
    public void EmbeddingResponse_Equality_WhenSameValues_InstancesAreEqual()
    {
        var items = ImmutableArray.Create<EmbeddingItemOutcome>(SucceededItem());

        var first = new EmbeddingResponse(items, ModelUsage.NotReported, null, ExtensionData.Empty);
        var second = new EmbeddingResponse(items, ModelUsage.NotReported, null, ExtensionData.Empty);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void EmbeddingCapabilities_Constructor_WhenExtensionsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new EmbeddingCapabilities(true, true, true, true, true, null!));

        exception.ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void EmbeddingLimits_Constructor_WhenMaxInputsLessThanOne_ThrowsArgumentOutOfRangeException() =>
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new EmbeddingLimits(0, null, null, null));

    [Fact]
    public void EmbeddingLimits_Constructor_WhenValid_RoundTripsProperties()
    {
        var limits = new EmbeddingLimits(96, 8192, 1536, 3072);

        limits.MaxInputsPerRequest.ShouldBe(96);
        limits.MaxInputTokensPerInput.ShouldBe(8192);
        limits.DefaultDimensions.ShouldBe(1536);
        limits.MaxDimensions.ShouldBe(3072);
    }

    [Fact]
    public void EmbeddingModelDescriptor_Constructor_WhenCapabilitiesNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new EmbeddingModelDescriptor(
            new EmbeddingModelAlias("default"),
            new ProviderId("openai"),
            new ApiFamilyId("openai"),
            new ModelId("text-embedding-3-small"),
            null,
            null!,
            EmptyLimits(),
            null,
            ExtensionData.Empty));

        exception.ParamName.ShouldBe("capabilities");
    }

    [Fact]
    public void EmbeddingModelDescriptor_Equality_WhenSameValues_InstancesAreEqual()
    {
        var first = Descriptor();
        var second = Descriptor();

        first.ShouldBe(second);
    }

    [Fact]
    public void EmbeddingAttemptCompleted_Constructor_WhenResponseNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new EmbeddingAttemptCompleted(null!));
        exception.ParamName.ShouldBe("response");
    }

    [Fact]
    public void EmbeddingAttemptFailed_Constructor_WhenFailureNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new EmbeddingAttemptFailed(null!));
        exception.ParamName.ShouldBe("failure");
    }

    [Fact]
    public void EmbeddingAttemptCancelled_Constructor_WhenCancellationNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new EmbeddingAttemptCancelled(null!));
        exception.ParamName.ShouldBe("cancellation");
    }

    [Fact]
    public void EmbeddingAttemptResult_Hierarchy_EveryLeafDerivesFromEmbeddingAttemptResult()
    {
        var items = ImmutableArray.Create<EmbeddingItemOutcome>(SucceededItem());
        var response = new EmbeddingResponse(items, ModelUsage.NotReported, null, ExtensionData.Empty);

        EmbeddingAttemptResult completed = new EmbeddingAttemptCompleted(response);
        EmbeddingAttemptResult failed = new EmbeddingAttemptFailed(Failure());
        EmbeddingAttemptResult cancelled = new EmbeddingAttemptCancelled(Failure(kind: ProviderFailureKind.Cancellation));

        _ = completed.ShouldBeOfType<EmbeddingAttemptCompleted>();
        _ = failed.ShouldBeOfType<EmbeddingAttemptFailed>();
        _ = cancelled.ShouldBeOfType<EmbeddingAttemptCancelled>();
    }

    private static ProviderResponseIdentity ProviderIdentity() => new(
        new ProviderId("openai"),
        null,
        new ApiFamilyId("openai"),
        new ModelId("text-embedding-3-small"),
        new ModelId("text-embedding-3-small"),
        null,
        null,
        null);

    private static EmbeddingSpaceIdentity Space() =>
        new(ProviderIdentity(), 3, EmbeddingElementType.Float32, EmbeddingPurpose.Document, ExtensionData.Empty);

    private static EmbeddingItemSucceeded SucceededItem() =>
        new(0, null, new DenseFloatVector([1.0f, 2.0f, 3.0f]), Space(), ExtensionData.Empty);

    private static ProviderFailure Failure(
        ProviderFailureKind kind = ProviderFailureKind.Unknown, string safeMessage = "failed") =>
        new(kind, new ProviderId("openai"), null, null, null, null, safeMessage, null, ExtensionData.Empty);

    private static EmbeddingLimits EmptyLimits() => new(null, null, null, null);

    private static EmbeddingCapabilities Capabilities() =>
        new(true, true, true, true, true, ExtensionData.Empty);

    private static EmbeddingModelDescriptor Descriptor() => new(
        new EmbeddingModelAlias("default"),
        new ProviderId("openai"),
        new ApiFamilyId("openai"),
        new ModelId("text-embedding-3-small"),
        null,
        Capabilities(),
        EmptyLimits(),
        null, ExtensionData.Empty);
}
