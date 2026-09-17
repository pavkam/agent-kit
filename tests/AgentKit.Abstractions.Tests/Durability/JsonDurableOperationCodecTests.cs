// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using System.Text;
using System.Text.Json;

/// <summary>Verifies JsonDurableOperationCodec behavior and contracts.</summary>
public sealed class JsonDurableOperationCodecTests
{
    private static readonly DurableOperationName Name = new("test.operation");
    private static readonly DurableOperationVersion Version = new("v1");

    [Theory]
    [InlineData("operationName")]
    [InlineData("version")]
    public void Constructor_WhenAnIdentityIsDefault_RejectsExactArgument(string parameter)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new JsonDurableOperationCodec<TestState>(
            parameter == "operationName" ? default : Name,
            parameter == "version" ? default : Version));

        exception.ParamName.ShouldBe(parameter);
    }

    [Fact]
    public void Constructor_WhenMaximumPayloadBytesIsNotPositive_RejectsExactArgument() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonDurableOperationCodec<TestState>(Name, Version, maximumPayloadBytes: 0))
            .ParamName.ShouldBe("maximumPayloadBytes");

    [Fact]
    public void Encode_WhenCalled_StampsThePayloadWithTheVersionAsSchemaVersion()
    {
        var codec = Codec();

        var payload = codec.Encode(new TestState("value", 42));

        payload.SchemaVersion.ShouldBe(new SchemaVersion(Version.Value));
    }

    [Fact]
    public void Encode_ThenDecode_RoundTripsEqualState()
    {
        var codec = Codec();
        var original = new TestState("value", 42);

        var payload = codec.Encode(original);
        var decoded = codec.Decode(payload);

        decoded.ShouldBeOfType<DurableDecoded<TestState>>().State.ShouldBe(original);
    }

    [Fact]
    public void Encode_WhenValueIsNull_RoundTripsNull()
    {
        var codec = Codec<TestState?>();

        var payload = codec.Encode(null);
        var decoded = codec.Decode(payload);

        decoded.ShouldBeOfType<DurableDecoded<TestState?>>().State.ShouldBeNull();
    }

    [Fact]
    public void Encode_WhenEncodedFormExceedsTheByteBound_ThrowsArgumentException()
    {
        var codec = Codec(maximumPayloadBytes: 4);

        Should.Throw<ArgumentException>(() => codec.Encode(new TestState("a value long enough to exceed the bound", 1)))
            .ParamName.ShouldBe("value");
    }

    [Fact]
    public void Encode_WhenValueCannotBeRepresentedAsJson_ThrowsArgumentException()
    {
        var codec = Codec<Type>();

        Should.Throw<ArgumentException>(() => codec.Encode(typeof(int))).ParamName.ShouldBe("value");
    }

    [Fact]
    public void Properties_WhenConstructed_RoundTripOperationNameAndVersion()
    {
        var codec = Codec();
        codec.OperationName.ShouldBe(Name);
        codec.Version.ShouldBe(Version);
    }

    [Fact]
    public void Decode_WhenPayloadIsNull_RejectsExactArgument() =>
        Should.Throw<ArgumentNullException>(() => Codec().Decode(null!)).ParamName.ShouldBe("payload");

    [Fact]
    public void Decode_WhenSchemaVersionDiffersFromTheCodecVersion_ReturnsIncompatibleWithoutParsingEvenValidJson()
    {
        var codec = Codec();
        var validJsonUnderAForeignVersion = new OperationPayload(
            new SchemaVersion("v2"), [.. JsonSerializer.SerializeToUtf8Bytes(new TestState("value", 1))]);

        var result = codec.Decode(validJsonUnderAForeignVersion);

        var incompatible = result.ShouldBeOfType<DurableDecodeIncompatible<TestState>>();
        incompatible.RecordedVersion.ShouldBe(new SchemaVersion("v2"));
    }

    [Fact]
    public void Decode_WhenPayloadExceedsTheByteBound_ReturnsIncompatibleWithoutParsing()
    {
        var codec = Codec();
        var oversized = codec.Encode(new TestState("value", 1));
        var boundedCodec = Codec(maximumPayloadBytes: oversized.Data.Length - 1);

        var result = boundedCodec.Decode(oversized);

        result.ShouldBeOfType<DurableDecodeIncompatible<TestState>>().RecordedVersion.ShouldBe(oversized.SchemaVersion);
    }

    [Fact]
    public void Decode_WhenPayloadIsNotValidJson_ReturnsIncompatibleWithoutThrowing()
    {
        var codec = Codec();
        var malformed = new OperationPayload(new SchemaVersion(Version.Value), [.. "not json"u8.ToArray()]);

        var result = codec.Decode(malformed);

        _ = result.ShouldBeOfType<DurableDecodeIncompatible<TestState>>();
    }

    [Fact]
    public void Decode_WhenStateTypeCannotBeRepresentedAsJson_ReturnsIncompatibleWithoutThrowing()
    {
        // Symmetric with Encode_WhenValueCannotBeRepresentedAsJson_ThrowsArgumentException: deserializing
        // into a type JsonSerializer cannot represent (System.Type) throws NotSupportedException, not
        // JsonException, so Decode must catch it too rather than letting it escape the codec.
        var codec = Codec<Type>();
        var payload = new OperationPayload(new SchemaVersion(Version.Value), [.. "\"System.Int32\""u8.ToArray()]);

        var result = codec.Decode(payload);

        result.ShouldBeOfType<DurableDecodeIncompatible<Type>>().RecordedVersion.ShouldBe(payload.SchemaVersion);
    }

    [Fact]
    public void Decode_WhenPayloadHasAnUnrecognizedProperty_DropsItSilentlyLikeOrdinaryJsonDeserialization()
    {
        var codec = Codec();
        var withExtra = new OperationPayload(
            new SchemaVersion(Version.Value),
            [.. JsonSerializer.SerializeToUtf8Bytes(new TestStateWithExtraProperty("value", 1, true))]);

        var result = codec.Decode(withExtra);

        result.ShouldBeOfType<DurableDecoded<TestState>>().State.ShouldBe(new TestState("value", 1));
    }

    [Fact]
    public void Constructor_WhenCustomSerializerOptionsAreSupplied_UsesThemForEncodingAndDecoding()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var codec = new JsonDurableOperationCodec<TestState>(Name, Version, options);
        var original = new TestState("value", 7);

        var payload = codec.Encode(original);
        var json = Encoding.UTF8.GetString(payload.Data.AsSpan());

        json.ShouldContain("\"text\":");
        codec.Decode(payload).ShouldBeOfType<DurableDecoded<TestState>>().State.ShouldBe(original);
    }

    private static JsonDurableOperationCodec<TestState> Codec(int maximumPayloadBytes = 1_048_576) =>
        new(Name, Version, maximumPayloadBytes: maximumPayloadBytes);

    private static JsonDurableOperationCodec<T> Codec<T>() => new(Name, Version);

    internal sealed record TestState(string Text, int Number);

    internal sealed record TestStateWithExtraProperty(string Text, int Number, bool Unknown);
}
