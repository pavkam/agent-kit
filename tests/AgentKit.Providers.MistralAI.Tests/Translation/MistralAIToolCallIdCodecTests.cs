// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MistralAI.Tests.Translation;

/// <summary>
/// Verifies that <see cref="MistralAIToolCallIdCodec"/> produces exactly the
/// nine-character alphanumeric identifiers Mistral accepts
/// (<c>^[a-zA-Z0-9]{9}$</c>), does so deterministically, and recognizes
/// which preserved provider identifiers already have that shape.
/// </summary>
public sealed class MistralAIToolCallIdCodecTests
{
    private static readonly ToolCallId _sampleCallId = new(Guid.Parse("7a4d2c0e-5b1f-4e8a-9c3d-2f6e1b0a9d8c"));

    [Fact]
    public void Encode_WhenCalled_ReturnsExactlyNineCharacters()
    {
        var encoded = MistralAIToolCallIdCodec.Encode(_sampleCallId);

        encoded.Length.ShouldBe(9);
        encoded.Length.ShouldBe(MistralAIToolCallIdCodec.WireLength);
    }

    [Fact]
    public void Encode_WhenCalledForManyIdentities_UsesOnlyAsciiLettersAndDigits()
    {
        foreach (var callId in SampleCallIds(count: 2_000))
        {
            var encoded = MistralAIToolCallIdCodec.Encode(callId);

            encoded.Length.ShouldBe(MistralAIToolCallIdCodec.WireLength, encoded);
            encoded.ShouldAllBe(character => char.IsAsciiLetterOrDigit(character), encoded);
        }
    }

    [Fact]
    public void Encode_WhenCalledTwiceForSameIdentity_ReturnsSameValue()
    {
        var first = MistralAIToolCallIdCodec.Encode(_sampleCallId);
        var second = MistralAIToolCallIdCodec.Encode(new ToolCallId(_sampleCallId.Value));

        second.ShouldBe(first);
    }

    [Fact]
    public void Encode_WhenIdentityIsKnown_ReturnsPinnedValue()
    {
        // Pinned so that a change in hashing, byte order, bit selection, or
        // alphabet is caught: the value must be stable across processes,
        // platforms, and releases because it is what the provider has seen.
        var encoded = MistralAIToolCallIdCodec.Encode(_sampleCallId);

        encoded.ShouldBe(_pinnedSampleEncoding);
    }

    [Fact]
    public void Encode_WhenIdentitiesDiffer_ProducesDistinctValues()
    {
        var callIds = SampleCallIds(count: 5_000).ToArray();

        var encoded = callIds.Select(callId => MistralAIToolCallIdCodec.Encode(callId)).ToArray();

        encoded.Distinct(StringComparer.Ordinal).Count().ShouldBe(callIds.Length);
    }

    [Fact]
    public void Encode_WhenIdentitiesDifferOnlyInLastByte_ProducesDistinctValues()
    {
        var first = new ToolCallId(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var second = new ToolCallId(Guid.Parse("00000000-0000-0000-0000-000000000002"));

        MistralAIToolCallIdCodec.Encode(first).ShouldNotBe(MistralAIToolCallIdCodec.Encode(second));
    }

    [Fact]
    public void Encode_WhenDisambiguatorDiffers_ProducesDifferentValue()
    {
        var preferred = MistralAIToolCallIdCodec.Encode(_sampleCallId, disambiguator: 0);
        var alternate = MistralAIToolCallIdCodec.Encode(_sampleCallId, disambiguator: 1);

        alternate.ShouldNotBe(preferred);
        alternate.Length.ShouldBe(MistralAIToolCallIdCodec.WireLength);
        alternate.ShouldAllBe(character => char.IsAsciiLetterOrDigit(character));
    }

    [Fact]
    public void Encode_WhenDisambiguatorIsOmitted_MatchesDisambiguatorZero() => MistralAIToolCallIdCodec.Encode(_sampleCallId).ShouldBe(MistralAIToolCallIdCodec.Encode(_sampleCallId, disambiguator: 0));

    [Fact]
    public void Encode_WhenCallIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => MistralAIToolCallIdCodec.Encode(default));

        exception.ParamName.ShouldBe("callId");
    }

    [Fact]
    public void Encode_WhenDisambiguatorIsNegative_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => MistralAIToolCallIdCodec.Encode(_sampleCallId, disambiguator: -1));

        exception.ParamName.ShouldBe("disambiguator");
    }

    [Fact]
    public void Encode_WhenDisambiguatorIsMaxValue_Succeeds()
    {
        var encoded = MistralAIToolCallIdCodec.Encode(_sampleCallId, int.MaxValue);

        encoded.Length.ShouldBe(MistralAIToolCallIdCodec.WireLength);
    }

    [Theory]
    [InlineData("D681PevKs")]
    [InlineData("000000000")]
    [InlineData("zzzzzzzzz")]
    [InlineData("ZZZZZZZZZ")]
    [InlineData("aB3dE5fG7")]
    public void IsWireId_WhenExactlyNineAsciiAlphanumerics_ReturnsTrue(string value) => MistralAIToolCallIdCodec.IsWireId(value).ShouldBeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("D681PevK")]
    [InlineData("D681PevKsX")]
    [InlineData("call_abc1")]
    [InlineData("call-abc1")]
    [InlineData("D681Pev s")]
    [InlineData("D681PevK\u00e9")]
    [InlineData("D681PevK\uff11")]
    [InlineData("toolu_01A09q90qw90lq917835lq9")]
    [InlineData("7a4d2c0e-5b1f-4e8a-9c3d-2f6e1b0a9d8c")]
    public void IsWireId_WhenNotExactlyNineAsciiAlphanumerics_ReturnsFalse(string? value) => MistralAIToolCallIdCodec.IsWireId(value).ShouldBeFalse();

    [Fact]
    public void IsWireId_WhenGivenEncodedValue_ReturnsTrue()
    {
        foreach (var callId in SampleCallIds(count: 200))
        {
            MistralAIToolCallIdCodec.IsWireId(MistralAIToolCallIdCodec.Encode(callId)).ShouldBeTrue();
        }
    }

    // SHA-256("7a4d2c0e-5b1f-4e8a-9c3d-2f6e1b0a9d8c" as 16 big-endian bytes ‖ int32 0),
    // leading 53 bits, base-62 with the 0-9A-Za-z alphabet. Cross-checked with an
    // independent Python implementation (hashlib.sha256 + uuid.bytes).
    private const string _pinnedSampleEncoding = "anx4Tk3rz";

    private static IEnumerable<ToolCallId> SampleCallIds(int count)
    {
        // Half sequential (worst case for any scheme that reads raw GUID
        // bits), half pseudo-random from a fixed seed.
        for (var index = 1; index <= count / 2; index++)
        {
            var bytes = new byte[16];
            bytes[15] = (byte) (index & 0xFF);
            bytes[14] = (byte) ((index >> 8) & 0xFF);
            bytes[13] = (byte) ((index >> 16) & 0xFF);
            yield return new ToolCallId(new Guid(bytes));
        }

        var random = new Random(Seed: 20250915);
        for (var index = 0; index < count - (count / 2); index++)
        {
            var bytes = new byte[16];
            random.NextBytes(bytes);
            if (new Guid(bytes) == Guid.Empty)
            {
                bytes[0] = 1;
            }

            yield return new ToolCallId(new Guid(bytes));
        }
    }
}
