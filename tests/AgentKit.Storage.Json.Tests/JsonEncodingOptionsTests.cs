// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json.Tests;

/// <summary>Verifies the composition-time encoding surface a registration delegate mutates before the contract is frozen.</summary>
/// <remarks>
/// This value exists only while a configure delegate runs, so the guarantees worth protecting are that it starts from the
/// canonical contract, that it never becomes null, and that mutation after freezing fails loudly instead of silently
/// diverging from the fingerprint already written to a manifest.
/// </remarks>
public sealed class JsonEncodingOptionsTests
{
    /// <summary>Verifies a fresh instance starts from the canonical contract rather than serializer defaults.</summary>
    [Fact]
    public void SerializerOptions_WhenNotConfigured_DefaultsToCanonicalContract()
    {
        var options = new JsonEncodingOptions();

        JsonFormatFingerprint.Compute(options.SerializerOptions).ShouldBe(
            JsonFormatFingerprint.Compute(JsonStoreSerialization.CreateCanonicalOptions()));
    }

    /// <summary>Verifies each instance owns an independent contract, so one composition never mutates another.</summary>
    [Fact]
    public void SerializerOptions_WhenTwoInstancesCreated_DoesNotShareOneContract()
    {
        var first = new JsonEncodingOptions();
        var second = new JsonEncodingOptions();

        first.SerializerOptions.ShouldNotBeSameAs(second.SerializerOptions);
    }

    /// <summary>Verifies assigning null is rejected, because a null contract could not encode anything at all.</summary>
    [Fact]
    public void SerializerOptions_WhenAssignedNull_ThrowsArgumentNullException()
    {
        var options = new JsonEncodingOptions();

        var exception = Should.Throw<ArgumentNullException>(() => options.SerializerOptions = null!);

        exception.ParamName.ShouldBe("SerializerOptions");
    }

    /// <summary>Verifies an outright replacement of the contract is retained exactly as supplied.</summary>
    [Fact]
    public void SerializerOptions_WhenReplaced_RetainsSuppliedInstance()
    {
        var replacement = new JsonSerializerOptions();

        var options = new JsonEncodingOptions { SerializerOptions = replacement };

        options.SerializerOptions.ShouldBeSameAs(replacement);
    }

    /// <summary>Verifies a null configure delegate is rejected before the current contract is exposed.</summary>
    [Fact]
    public void Configure_WhenDelegateIsNull_ThrowsArgumentNullException()
    {
        var options = new JsonEncodingOptions();

        var exception = Should.Throw<ArgumentNullException>(() => options.Configure(null!));

        exception.ParamName.ShouldBe("configure");
    }

    /// <summary>Verifies the delegate mutates the current instance in place and the same value is returned for chaining.</summary>
    [Fact]
    public void Configure_WhenDelegateSupplied_MutatesCurrentContractInPlace()
    {
        var options = new JsonEncodingOptions();
        var original = options.SerializerOptions;

        var returned = options.Configure(static contract => contract.WriteIndented = true);

        returned.ShouldBeSameAs(options);
        options.SerializerOptions.ShouldBeSameAs(original);
        options.SerializerOptions.WriteIndented.ShouldBeTrue();
    }

    /// <summary>Verifies mutating an already frozen contract fails rather than diverging from the recorded fingerprint.</summary>
    [Fact]
    public void Configure_WhenContractIsAlreadyReadOnly_ThrowsInvalidOperationException()
    {
        var options = new JsonEncodingOptions();
        _ = new JsonEncodingSettings(options.SerializerOptions);

        _ = Should.Throw<InvalidOperationException>(
            () => options.Configure(static contract => contract.WriteIndented = true));
    }
}
