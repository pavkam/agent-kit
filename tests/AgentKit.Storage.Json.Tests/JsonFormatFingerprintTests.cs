// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json.Tests;

/// <summary>Verifies that the encoding fingerprint separates semantically meaningful settings from presentational ones.</summary>
/// <remarks>
/// The fingerprint is the value a manifest stores to refuse reopening a root under an incompatible contract, so both
/// directions matter equally: a change that alters persisted meaning must change the fingerprint, and a change that cannot
/// alter it must not, or an existing store would be rejected for a cosmetic reason.
/// </remarks>
public sealed class JsonFormatFingerprintTests
{
    /// <summary>Verifies null options are rejected rather than fingerprinted as an empty contract.</summary>
    [Fact]
    public void Compute_WhenOptionsAreNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => JsonFormatFingerprint.Compute(null!));
        exception.ParamName.ShouldBe("options");
    }

    /// <summary>Verifies the fingerprint is a lowercase hexadecimal SHA-256 digest, which is what a manifest persists.</summary>
    [Fact]
    public void Compute_WhenCalled_ReturnsLowercaseHexadecimalDigest()
    {
        var fingerprint = JsonFormatFingerprint.Compute(JsonStoreSerialization.CreateCanonicalOptions());

        fingerprint.Length.ShouldBe(64);
        fingerprint.ShouldMatch("^[0-9a-f]{64}$");
    }

    /// <summary>Verifies two independently built but semantically identical contracts fingerprint identically.</summary>
    [Fact]
    public void Compute_WhenOptionSetsAreEquivalent_ReturnsSameFingerprint()
    {
        var first = JsonStoreSerialization.CreateCanonicalOptions();
        var second = JsonStoreSerialization.CreateCanonicalOptions();

        JsonFormatFingerprint.Compute(first).ShouldBe(JsonFormatFingerprint.Compute(second));
    }

    /// <summary>Verifies repeated computation over one instance is deterministic within a process.</summary>
    [Fact]
    public void Compute_WhenCalledTwiceForOneInstance_ReturnsSameFingerprint()
    {
        var options = JsonStoreSerialization.CreateCanonicalOptions();

        JsonFormatFingerprint.Compute(options).ShouldBe(JsonFormatFingerprint.Compute(options));
    }

    /// <summary>Verifies a different naming policy changes the fingerprint, because emitted property names change.</summary>
    [Fact]
    public void Compute_WhenNamingPolicyDiffers_ReturnsDifferentFingerprint()
    {
        var baseline = JsonStoreSerialization.CreateCanonicalOptions();
        var changed = JsonStoreSerialization.CreateCanonicalOptions();
        changed.PropertyNamingPolicy = JsonNamingPolicy.KebabCaseLower;

        JsonFormatFingerprint.Compute(changed).ShouldNotBe(JsonFormatFingerprint.Compute(baseline));
    }

    /// <summary>Verifies an additional converter changes the fingerprint, because converter selection changes value shapes.</summary>
    [Fact]
    public void Compute_WhenConverterSetDiffers_ReturnsDifferentFingerprint()
    {
        var baseline = JsonStoreSerialization.CreateCanonicalOptions();
        var changed = JsonStoreSerialization.CreateCanonicalOptions();
        changed.Converters.Add(new ConstantTextJsonConverter());

        JsonFormatFingerprint.Compute(changed).ShouldNotBe(JsonFormatFingerprint.Compute(baseline));
    }

    /// <summary>Verifies reordering the same converters changes the fingerprint, since order decides which one handles a type.</summary>
    [Fact]
    public void Compute_WhenConverterOrderDiffers_ReturnsDifferentFingerprint()
    {
        var first = new JsonSerializerOptions
        {
            Converters =
            {
                new JsonStringEnumConverter(),
                new JsonStringEnumConverter<JsonStoreOpenMode>(),
            },
        };
        var second = new JsonSerializerOptions
        {
            Converters =
            {
                new JsonStringEnumConverter<JsonStoreOpenMode>(),
                new JsonStringEnumConverter(),
            },
        };

        JsonFormatFingerprint.Compute(first).ShouldNotBe(JsonFormatFingerprint.Compute(second));
    }

    /// <summary>Verifies relaxed number handling changes the fingerprint, because numeric text becomes readable differently.</summary>
    [Fact]
    public void Compute_WhenNumberHandlingDiffers_ReturnsDifferentFingerprint()
    {
        var baseline = JsonStoreSerialization.CreateCanonicalOptions();
        var changed = JsonStoreSerialization.CreateCanonicalOptions();
        changed.NumberHandling = JsonNumberHandling.AllowReadingFromString;

        JsonFormatFingerprint.Compute(changed).ShouldNotBe(JsonFormatFingerprint.Compute(baseline));
    }

    /// <summary>Verifies a different default ignore condition changes the fingerprint, because omitted members change.</summary>
    [Fact]
    public void Compute_WhenDefaultIgnoreConditionDiffers_ReturnsDifferentFingerprint()
    {
        var baseline = JsonStoreSerialization.CreateCanonicalOptions();
        var changed = JsonStoreSerialization.CreateCanonicalOptions();
        changed.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault;

        JsonFormatFingerprint.Compute(changed).ShouldNotBe(JsonFormatFingerprint.Compute(baseline));
    }

    /// <summary>Verifies a different maximum depth changes the fingerprint, because reader tolerance changes.</summary>
    [Fact]
    public void Compute_WhenMaxDepthDiffers_ReturnsDifferentFingerprint()
    {
        var baseline = JsonStoreSerialization.CreateCanonicalOptions();
        var changed = JsonStoreSerialization.CreateCanonicalOptions();
        changed.MaxDepth = 32;

        JsonFormatFingerprint.Compute(changed).ShouldNotBe(JsonFormatFingerprint.Compute(baseline));
    }

    /// <summary>Verifies case-insensitive matching changes the fingerprint, because previously invalid documents become readable.</summary>
    [Fact]
    public void Compute_WhenPropertyNameCaseSensitivityDiffers_ReturnsDifferentFingerprint()
    {
        var baseline = JsonStoreSerialization.CreateCanonicalOptions();
        var changed = JsonStoreSerialization.CreateCanonicalOptions();
        changed.PropertyNameCaseInsensitive = true;

        JsonFormatFingerprint.Compute(changed).ShouldNotBe(JsonFormatFingerprint.Compute(baseline));
    }

    /// <summary>Verifies indentation alone does not change the fingerprint, so a cosmetic toggle cannot invalidate a store.</summary>
    [Fact]
    public void Compute_WhenOnlyIndentationDiffers_ReturnsSameFingerprint()
    {
        var baseline = JsonStoreSerialization.CreateCanonicalOptions();
        var indented = JsonStoreSerialization.CreateCanonicalOptions();
        indented.WriteIndented = true;

        JsonFormatFingerprint.Compute(indented).ShouldBe(JsonFormatFingerprint.Compute(baseline));
    }
}
