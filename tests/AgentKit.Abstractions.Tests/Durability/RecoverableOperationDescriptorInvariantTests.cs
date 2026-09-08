// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>
/// Verifies the stable declaration values required to replay a recoverable
/// operation without silently selecting a different handler or attempt.
/// </summary>
public sealed class RecoverableOperationDescriptorInvariantTests
{
    [Theory]
    [InlineData("name", false)]
    [InlineData("version", false)]
    [InlineData("idempotencyKey", false)]
    [InlineData("name", true)]
    [InlineData("version", true)]
    [InlineData("idempotencyKey", true)]
    public void Constructor_WhenStableDeclarationValueIsDefault_ThrowsExactArgumentException(
        string parameterName, bool useCanonicalBindingConstructor)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Create(parameterName, useCanonicalBindingConstructor));

        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe(parameterName);
    }

    [Theory]
    [InlineData("Name")]
    [InlineData("Version")]
    [InlineData("IdempotencyKey")]
    public void With_WhenStableDeclarationValueIsDefault_ThrowsExactArgumentExceptionAndPreservesOriginal(
        string propertyName)
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

        return useCanonicalBindingConstructor
            ? new RecoverableOperationDescriptor(
                binding,
                name,
                version,
                key,
                DurabilityTestData.Payload(),
                DurableRetryOwner.Caller,
                DurableTimeoutOwner.Caller,
                CancellationSemantics.LocalWaitOnly,
                SecurityEffect.Execute,
                IdempotencyClassification.Idempotent,
                DurabilityTestData.Now)
            : new RecoverableOperationDescriptor(
                binding.Address,
                binding.ExecutionContext,
                name,
                version,
                key,
                DurabilityTestData.Payload(),
                DurableRetryOwner.Caller,
                DurableTimeoutOwner.Caller,
                CancellationSemantics.LocalWaitOnly,
                SecurityEffect.Execute,
                IdempotencyClassification.Idempotent,
                DurabilityTestData.Now);
    }
}
