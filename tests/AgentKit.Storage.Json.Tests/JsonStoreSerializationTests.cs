// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json.Tests;

using System.Text.Json.Serialization.Metadata;

/// <summary>Verifies the canonical contract's strictness, the bounded encode and decode helpers, and the fidelity self-check.</summary>
/// <remarks>
/// These helpers are the only place the package turns evidence into bytes, so each documented failure mode is asserted
/// directly: an exceeded bound is a data failure, a malformed payload is a contract failure, and an unfaithful configured
/// contract is a composition failure that must surface before any authoritative record is written.
/// </remarks>
public sealed class JsonStoreSerializationTests
{
    /// <summary>Verifies each call yields an independent instance so one composition's mutations never leak into another.</summary>
    [Fact]
    public void CreateCanonicalOptions_WhenCalledTwice_ReturnsIndependentInstances()
    {
        var first = JsonStoreSerialization.CreateCanonicalOptions();
        var second = JsonStoreSerialization.CreateCanonicalOptions();

        first.ShouldNotBeSameAs(second);
        first.WriteIndented = true;
        second.WriteIndented.ShouldBeFalse();
    }

    /// <summary>Verifies the canonical contract applies exactly the documented strict settings.</summary>
    [Fact]
    public void CreateCanonicalOptions_WhenCalled_AppliesDocumentedStrictSettings()
    {
        var options = JsonStoreSerialization.CreateCanonicalOptions();

        options.PropertyNamingPolicy.ShouldBe(JsonNamingPolicy.CamelCase);
        options.DictionaryKeyPolicy.ShouldBe(JsonNamingPolicy.CamelCase);
        options.PropertyNameCaseInsensitive.ShouldBeFalse();
        options.DefaultIgnoreCondition.ShouldBe(JsonIgnoreCondition.WhenWritingNull);
        options.NumberHandling.ShouldBe(JsonNumberHandling.Strict);
        options.ReadCommentHandling.ShouldBe(JsonCommentHandling.Disallow);
        options.AllowTrailingCommas.ShouldBeFalse();
        options.UnmappedMemberHandling.ShouldBe(JsonUnmappedMemberHandling.Disallow);
        options.WriteIndented.ShouldBeFalse();
        options.MaxDepth.ShouldBe(64);
        _ = options.TypeInfoResolver.ShouldBeOfType<DefaultJsonTypeInfoResolver>();
        _ = options.Converters.ShouldHaveSingleItem().ShouldBeOfType<JsonStringEnumConverter>();
    }

    /// <summary>Verifies the canonical contract writes enumerations as stable names rather than reorderable ordinals.</summary>
    [Fact]
    public void CreateCanonicalOptions_WhenEncodingEnumeration_WritesStableMemberName()
    {
        var options = JsonStoreSerialization.CreateCanonicalOptions();

        var payload = JsonSerializer.Serialize(
            new JsonProtectedResource(ProtectedResourceKind.Directory, "/workspace"), options);

        payload.ShouldContain("\"Directory\"");
    }

    /// <summary>Verifies the canonical contract refuses an integer enumeration value written by a foreign writer.</summary>
    [Fact]
    public void CreateCanonicalOptions_WhenDecodingIntegerEnumeration_ThrowsJsonException()
    {
        var options = JsonStoreSerialization.CreateCanonicalOptions();

        _ = Should.Throw<JsonException>(() => JsonStoreSerialization.Decode<JsonProtectedResource>(
                                 /*lang=json,strict*/
                                 """{"kind":1,"identifier":"/workspace"}"""u8, options));
    }

    /// <summary>Verifies the canonical contract refuses an unmapped member rather than silently dropping unknown evidence.</summary>
    [Fact]
    public void CreateCanonicalOptions_WhenDecodingUnmappedMember_ThrowsJsonException()
    {
        var options = JsonStoreSerialization.CreateCanonicalOptions();

        _ = Should.Throw<JsonException>(() => JsonStoreSerialization.Decode<JsonProtectedResource>(
                                 /*lang=json,strict*/
                                 """{"kind":"File","identifier":"/workspace","extra":1}"""u8, options));
    }

    /// <summary>Verifies a null value is rejected before any encoding work begins.</summary>
    [Fact]
    public void Encode_WhenValueIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => JsonStoreSerialization.Encode<JsonProtectedResource>(
            null!, JsonStoreSerialization.CreateCanonicalOptions(), 64));

        exception.ParamName.ShouldBe("value");
    }

    /// <summary>Verifies null options are rejected rather than substituted by an implicit default contract.</summary>
    [Fact]
    public void Encode_WhenOptionsAreNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => JsonStoreSerialization.Encode(
            new JsonProtectedResource(ProtectedResourceKind.File, "/workspace"), null!, 64));

        exception.ParamName.ShouldBe("options");
    }

    /// <summary>Verifies a zero byte bound is rejected, since no encoded payload can satisfy it.</summary>
    [Fact]
    public void Encode_WhenMaximumBytesIsZero_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => JsonStoreSerialization.Encode(
            new JsonProtectedResource(ProtectedResourceKind.File, "/workspace"),
            JsonStoreSerialization.CreateCanonicalOptions(),
            0));

        exception.ParamName.ShouldBe("maximumBytes");
    }

    /// <summary>Verifies a negative byte bound is rejected at the boundary just below zero.</summary>
    [Fact]
    public void Encode_WhenMaximumBytesIsNegative_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => JsonStoreSerialization.Encode(
            new JsonProtectedResource(ProtectedResourceKind.File, "/workspace"),
            JsonStoreSerialization.CreateCanonicalOptions(),
            -1));

        exception.ParamName.ShouldBe("maximumBytes");
    }

    /// <summary>Verifies a payload of exactly the permitted length is returned, proving the bound is inclusive.</summary>
    [Fact]
    public void Encode_WhenPayloadLengthEqualsBound_ReturnsPayload()
    {
        var options = JsonStoreSerialization.CreateCanonicalOptions();
        var value = new JsonProtectedResource(ProtectedResourceKind.File, "/workspace");
        var length = JsonSerializer.SerializeToUtf8Bytes(value, options).Length;

        var payload = JsonStoreSerialization.Encode(value, options, length);

        payload.Length.ShouldBe(length);
    }

    /// <summary>Verifies an oversized payload is refused rather than written and truncated later.</summary>
    [Fact]
    public void Encode_WhenPayloadExceedsBound_ThrowsInvalidDataException()
    {
        var options = JsonStoreSerialization.CreateCanonicalOptions();
        var value = new JsonProtectedResource(ProtectedResourceKind.File, "/workspace");
        var length = JsonSerializer.SerializeToUtf8Bytes(value, options).Length;

        _ = Should.Throw<InvalidDataException>(() => JsonStoreSerialization.Encode(value, options, length - 1));
    }

    /// <summary>Verifies a well-formed payload decodes to the value it was encoded from.</summary>
    [Fact]
    public void Decode_WhenPayloadIsWellFormed_ReturnsValue()
    {
        var options = JsonStoreSerialization.CreateCanonicalOptions();
        var value = new JsonProtectedResource(ProtectedResourceKind.Process, "pid:42");
        var payload = JsonStoreSerialization.Encode(value, options, 256);

        var decoded = JsonStoreSerialization.Decode<JsonProtectedResource>(payload, options);

        decoded.ShouldBe(value);
    }

    /// <summary>Verifies null options are rejected before any byte of the payload is inspected.</summary>
    [Fact]
    public void Decode_WhenOptionsAreNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => JsonStoreSerialization.Decode<JsonProtectedResource>("{}"u8, null!));

        exception.ParamName.ShouldBe("options");
    }

    /// <summary>Verifies a truncated payload surfaces as a contract failure rather than a partially built value.</summary>
    [Fact]
    public void Decode_WhenPayloadIsMalformed_ThrowsJsonException()
    {
        _ = Should.Throw<JsonException>(() => JsonStoreSerialization.Decode<JsonProtectedResource>(
            """{"kind":"File","identifier":"""u8, JsonStoreSerialization.CreateCanonicalOptions()));
    }

    /// <summary>Verifies a literal JSON null is reported as invalid persisted data rather than returned as a value.</summary>
    [Fact]
    public void Decode_WhenPayloadIsJsonNull_ThrowsInvalidDataException()
    {
        _ = Should.Throw<InvalidDataException>(() => JsonStoreSerialization.Decode<JsonProtectedResource>(
            "null"u8, JsonStoreSerialization.CreateCanonicalOptions()));
    }

    /// <summary>Verifies a null probe is rejected before the self-check performs any encoding.</summary>
    [Fact]
    public void VerifyRoundTrip_WhenProbeIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => JsonStoreSerialization.VerifyRoundTrip<JsonProtectedResource>(
                null!, JsonStoreSerialization.CreateCanonicalOptions()));

        exception.ParamName.ShouldBe("probe");
    }

    /// <summary>Verifies null options are rejected before the self-check performs any encoding.</summary>
    [Fact]
    public void VerifyRoundTrip_WhenOptionsAreNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => JsonStoreSerialization.VerifyRoundTrip(
            new JsonProtectedResource(ProtectedResourceKind.File, "/workspace"), null!));

        exception.ParamName.ShouldBe("options");
    }

    /// <summary>Verifies a faithful contract passes the self-check for a nested probe carrying optional members.</summary>
    [Fact]
    public void VerifyRoundTrip_WhenContractIsFaithful_DoesNotThrow()
    {
        var probe = JsonSecurityAuthorizationScope.FromDomain(TestEvidenceFactory.Scope());

        Should.NotThrow(
            () => JsonStoreSerialization.VerifyRoundTrip(probe, JsonStoreSerialization.CreateCanonicalOptions()));
    }

    /// <summary>Verifies a contract that cannot encode the probe fails composition instead of failing mid-write.</summary>
    [Fact]
    public void VerifyRoundTrip_WhenContractCannotEncodeProbe_ThrowsInvalidOperationException()
    {
        var options = JsonStoreSerialization.CreateCanonicalOptions();
        options.MaxDepth = 1;
        var probe = JsonSecurityAuthorizationScope.FromDomain(TestEvidenceFactory.Scope());

        var exception = Should.Throw<InvalidOperationException>(
            () => JsonStoreSerialization.VerifyRoundTrip(probe, options));

        _ = exception.InnerException.ShouldBeOfType<JsonException>();
    }

    /// <summary>Verifies a contract that encodes and decodes yet changes a member is rejected as unfaithful.</summary>
    /// <remarks>
    /// A substituting converter is the smallest faithful model of caller-supplied options that lose evidence: both encoding
    /// and decoding succeed, so only the equality comparison can detect that the persisted value would not survive.
    /// </remarks>
    [Fact]
    public void VerifyRoundTrip_WhenContractDoesNotReproduceProbe_ThrowsInvalidOperationException()
    {
        var options = JsonStoreSerialization.CreateCanonicalOptions();
        options.Converters.Add(new ConstantTextJsonConverter());
        var probe = new JsonProtectedResource(ProtectedResourceKind.File, "/workspace/original.txt");

        var exception = Should.Throw<InvalidOperationException>(
            () => JsonStoreSerialization.VerifyRoundTrip(probe, options));

        exception.InnerException.ShouldBeNull();
    }
}
