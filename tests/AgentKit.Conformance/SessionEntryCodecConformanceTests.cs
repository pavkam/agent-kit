// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Defines reusable deterministic, loss-aware, and concurrent behavior for portable session-entry codecs.</summary>
/// <typeparam name="TFixture">The concrete codec fixture.</typeparam>
public abstract class SessionEntryCodecConformanceTests<TFixture>
    where TFixture : ISessionEntryCodecConformanceFixture, new()
{
    /// <summary>Verifies canonical encoding is deterministic and decodes to complete equivalent semantics.</summary>
    [Fact]
    public void EncodeDecode_WhenEntryIsValid_IsDeterministicAndEquivalent()
    {
        var fixture = new TFixture(); var codec = fixture.CreateCodec(); var entry = fixture.CreateEntry();
        var first = codec.Encode(entry).ShouldBeOfType<SessionEntryEncoded>().Wire;
        var second = codec.Encode(fixture.CreateEntry()).ShouldBeOfType<SessionEntryEncoded>().Wire;
        var decoded = codec.Decode(first).ShouldBeOfType<SessionEntryDecoded>().Decoded;
        first.ShouldBe(second);
        fixture.SemanticallyEquivalent(entry, decoded.Entry).ShouldBeTrue();
        decoded.Wire.ShouldBeSameAs(first);
    }

    /// <summary>Verifies the singleton codec supports concurrent deterministic calls.</summary>
    [Fact]
    public void Operations_WhenConcurrent_RemainDeterministic()
    {
        var fixture = new TFixture(); var codec = fixture.CreateCodec(); var expected = codec.Encode(fixture.CreateEntry()).ShouldBeOfType<SessionEntryEncoded>().Wire;
        _ = Parallel.For(0, 64, iteration =>
        {
            _ = iteration;
            var wire = codec.Encode(fixture.CreateEntry()).ShouldBeOfType<SessionEntryEncoded>().Wire;
            wire.ShouldBe(expected);
            _ = codec.Decode(wire).ShouldBeOfType<SessionEntryDecoded>();
        });
    }
}
