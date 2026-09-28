// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Sqlite.Tests;

/// <summary>Verifies the durable codec reproduces evidence exactly and enforces its bounds.</summary>
public sealed class SqliteDurableCodecTests
{
    /// <summary>Verifies the canonical contract round-trips the fidelity probe, which bootstrap depends on.</summary>
    [Fact]
    public void VerifyRoundTrip_WhenContractIsCanonical_DoesNotThrow() =>
        Should.NotThrow(SqliteDurableCodec.VerifyRoundTrip);

    /// <summary>Verifies key bytes are network order and independent of the runtime's in-memory GUID layout.</summary>
    [Fact]
    public void EncodeGuid_WhenValueIsKnown_WritesNetworkByteOrder()
    {
        var value = new Guid("00112233-4455-6677-8899-aabbccddeeff");

        var encoded = SqliteDurableCodec.EncodeGuid(value);

        encoded.ShouldBe(
        [
            0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77,
            0x88, 0x99, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF,
        ]);
    }

    /// <summary>Verifies decoding inverts encoding exactly.</summary>
    [Fact]
    public void DecodeGuid_WhenBytesWereEncoded_ReturnsTheOriginalValue()
    {
        var value = Guid.NewGuid();

        var decoded = SqliteDurableCodec.DecodeGuid(SqliteDurableCodec.EncodeGuid(value));

        decoded.ShouldBe(value);
    }

    /// <summary>Verifies a key of the wrong width is refused rather than reinterpreted.</summary>
    [Fact]
    public void DecodeGuid_WhenLengthIsNotSixteen_ThrowsForTheLength()
    {
        var truncated = new byte[15];

        _ = Should.Throw<ArgumentOutOfRangeException>(() => SqliteDurableCodec.DecodeGuid(truncated));
    }

    /// <summary>Verifies an encoded projection decodes back to a byte-identical projection.</summary>
    /// <remarks>
    /// Re-encoding is the strongest available equality check here, because the projection is a mutable state holder
    /// with reference equality rather than a value object.
    /// </remarks>
    [Fact]
    public void DecodeProjection_WhenPayloadWasEncoded_ReproducesTheProjectionExactly()
    {
        var settings = SqliteDurableStoreSettings.CreateDefault();
        var payload = SqliteDurableCodec.EncodeProjection(RequireSnapshot(), settings);

        var reencoded = SqliteDurableCodec.EncodeProjection(
            SqliteDurableCodec.DecodeProjection(payload, settings), settings);

        reencoded.ShouldBe(payload);
    }

    /// <summary>Verifies an oversized payload is refused before it reaches the decoder.</summary>
    [Fact]
    public void DecodeProjection_WhenPayloadExceedsTheBound_ThrowsForTheLength()
    {
        var settings = new SqliteDurableStoreSettings(TimeSpan.FromSeconds(1), 8);
        var payload = SqliteDurableCodec.EncodeProjection(
            RequireSnapshot(), SqliteDurableStoreSettings.CreateDefault());

        _ = Should.Throw<ArgumentOutOfRangeException>(() => SqliteDurableCodec.DecodeProjection(payload, settings));
    }

    /// <summary>Verifies an oversized projection is refused before it is persisted.</summary>
    [Fact]
    public void EncodeProjection_WhenEncodedSizeExceedsTheBound_ThrowsInvalidData()
    {
        var settings = new SqliteDurableStoreSettings(TimeSpan.FromSeconds(1), 8);

        _ = Should.Throw<InvalidDataException>(() => SqliteDurableCodec.EncodeProjection(RequireSnapshot(), settings));
    }

    /// <summary>Verifies a null projection is refused.</summary>
    [Fact]
    public void EncodeProjection_WhenProjectionIsNull_ThrowsForTheProjectionArgument()
    {
        var exception = Should.Throw<ArgumentNullException>(() => SqliteDurableCodec.EncodeProjection(
            null!, SqliteDurableStoreSettings.CreateDefault()));

        exception.ParamName.ShouldBe("projection");
    }

    private static DurableOperationProjection RequireSnapshot() =>
        DurableJournalProbe.Create().Snapshot!.ToDomain();
}
