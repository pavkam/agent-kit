// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies RecoverableOperationDescriptor behavior and contracts.</summary>
public sealed class RecoverableOperationDescriptorTests
{
    [Fact]
    public void RecoverableOperationDescriptor_Constructor_WhenAddressIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => Descriptor(nullAddress: true));
        exception.ParamName.ShouldBe("address");
    }

    [Fact]
    public void RecoverableOperationDescriptor_Constructor_WhenInputIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => Descriptor(nullInput: true));
        exception.ParamName.ShouldBe("input");
    }

    [Fact]
    public void RecoverableOperationDescriptor_Constructor_WhenIdempotencyIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Descriptor(idempotency: (IdempotencyClassification) 99));
        exception.ParamName.ShouldBe("idempotency");
    }

    [Fact]
    public void RecoverableOperationDescriptor_Constructor_WhenRetryOwnerIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Descriptor(retryOwner: (DurableRetryOwner) 42));
        exception.ParamName.ShouldBe("retryOwner");
    }

    [Fact]
    public void RecoverableOperationDescriptor_Constructor_WhenCausalParentIsEmptyIdentity_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Descriptor(causalParentId: default(OperationId)));
        exception.ParamName.ShouldBe("causalParentId");
    }

    [Fact]
    public void RecoverableOperationDescriptor_Constructor_WhenExtensionsOmitted_DefaultsToEmpty() => DurabilityTestData.Descriptor().Extensions.ShouldBe(ExtensionData.Empty);
    [Fact]
    public void RecoverableOperationDescriptor_With_WhenExtensionsNull_Throws()
    {
        var exception = Should.Throw<ArgumentNullException>(() => DurabilityTestData.Descriptor() with { Extensions = null! });
        exception.ParamName.ShouldBe(nameof(RecoverableOperationDescriptor.Extensions));
    }

    /// <summary>
    /// Builds a descriptor whose arguments are all valid unless a test
    /// deliberately overrides one, so each guard is reached in turn rather
    /// than being masked by an earlier null check.
    /// </summary>
    private static RecoverableOperationDescriptor Descriptor(DurableOperationAddress? address = null, OperationPayload? input = null, DurableRetryOwner retryOwner = DurableRetryOwner.Caller, IdempotencyClassification idempotency = IdempotencyClassification.Idempotent, OperationId? causalParentId = null, bool nullAddress = false, bool nullInput = false) => new(nullAddress ? null! : address ?? DurabilityTestData.Address(), DurabilityTestData.Context(), new DurableOperationName("op"), new DurableOperationVersion("v1"), new IdempotencyKey("k"), nullInput ? null! : input ?? DurabilityTestData.Payload(), retryOwner, DurableTimeoutOwner.Caller, CancellationSemantics.LocalWaitOnly, SecurityEffect.Execute, idempotency, DurabilityTestData.Now, causalParentId);
    [Theory]
    [InlineData("name", false)]
    [InlineData("version", false)]
    [InlineData("idempotencyKey", false)]
    [InlineData("name", true)]
    [InlineData("version", true)]
    [InlineData("idempotencyKey", true)]
    public void Constructor_WhenStableDeclarationValueIsDefault_ThrowsExactArgumentException(string parameterName, bool useCanonicalBindingConstructor)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Create(parameterName, useCanonicalBindingConstructor));
        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe(parameterName);
    }

    [Theory]
    [InlineData("Name")]
    [InlineData("Version")]
    [InlineData("IdempotencyKey")]
    public void With_WhenStableDeclarationValueIsDefault_ThrowsExactArgumentExceptionAndPreservesOriginal(string propertyName)
    {
        var original = DurabilityTestData.Descriptor();
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => propertyName switch
        {
            "Name" => original with { Name = default },
            "Version" => original with { Version = default },
            "IdempotencyKey" => original with { IdempotencyKey = default },
            _ => throw new ArgumentOutOfRangeException(nameof(propertyName)),
        });
        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe(propertyName);
        original.Name.ShouldBe(DurabilityTestData.Descriptor().Name);
        original.Version.ShouldBe(DurabilityTestData.Descriptor().Version);
        original.IdempotencyKey.ShouldBe(DurabilityTestData.Descriptor().IdempotencyKey);
    }

    private static RecoverableOperationDescriptor Create(string parameterName, bool useCanonicalBindingConstructor)
    {
        var binding = new DurableOperationBinding(DurabilityTestData.Address(), DurabilityTestData.Context());
        var name = parameterName == "name" ? default : new DurableOperationName("operation");
        var version = parameterName == "version" ? default : new DurableOperationVersion("v1");
        var key = parameterName == "idempotencyKey" ? default : new IdempotencyKey("idempotency");
        return useCanonicalBindingConstructor ? new RecoverableOperationDescriptor(binding, name, version, key, DurabilityTestData.Payload(), DurableRetryOwner.Caller, DurableTimeoutOwner.Caller, CancellationSemantics.LocalWaitOnly, SecurityEffect.Execute, IdempotencyClassification.Idempotent, DurabilityTestData.Now) : new RecoverableOperationDescriptor(binding.Address, binding.ExecutionContext, name, version, key, DurabilityTestData.Payload(), DurableRetryOwner.Caller, DurableTimeoutOwner.Caller, CancellationSemantics.LocalWaitOnly, SecurityEffect.Execute, IdempotencyClassification.Idempotent, DurabilityTestData.Now);
    }
}
