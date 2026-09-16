// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Sqlite.Tests;

/// <summary>Verifies the strict bounded SQLite evidence codec through its test-visible contract.</summary>
public sealed class SqliteSecurityGrantCodecTests
{
    private static readonly SqliteSecurityGrantStoreSettings _settings = SqliteSecurityGrantStoreSettings.CreateDefault();

    /// <summary>Verifies exact UTF-16 code units remain distinct, including malformed standalone surrogates.</summary>
    [Fact]
    public void EncodeGrant_WhenStringsContainSurrogates_PreservesExactCodeUnits()
    {
        var now = DateTimeOffset.UnixEpoch;
        var highOne = TestGrantFactory.CreateGrant(now, "resource:\ud800");
        var highTwo = TestGrantFactory.CreateGrant(now, "resource:\ud801");
        var replacement = TestGrantFactory.CreateGrant(now, "resource:\ufffd");
        var astral = TestGrantFactory.CreateGrant(now, "resource:\ud83d\ude80");

        var encoded = new[] { highOne, highTwo, replacement, astral }
            .Select(grant => Convert.ToHexString(SqliteSecurityGrantCodec.EncodeGrant(grant, _settings)))
            .ToArray();

        encoded.Distinct(StringComparer.Ordinal).Count().ShouldBe(4);
        SqliteSecurityGrantCodec.DecodeGrant(
            SqliteSecurityGrantCodec.EncodeGrant(highOne, _settings), _settings).ShouldBe(highOne);
    }

    /// <summary>Verifies version-one decoding rejects extensions it cannot interpret.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DecodeGrant_WhenEnvelopeIsFutureOrHasTrailingEvidence_FailsClosed(bool changeVersion)
    {
        var payload = SqliteSecurityGrantCodec.EncodeGrant(
            TestGrantFactory.CreateGrant(DateTimeOffset.UnixEpoch), _settings);
        if (changeVersion)
        {
            payload[4]++;
        }
        else
        {
            Array.Resize(ref payload, payload.Length + 1);
        }

        _ = Should.Throw<InvalidDataException>(() => SqliteSecurityGrantCodec.DecodeGrant(payload, _settings));
    }

    /// <summary>Verifies decoding rejects an empty or over-bound envelope before any parsing begins.</summary>
    [Fact]
    public void DecodeGrant_WhenPayloadIsEmptyOrExceedsTheBoundedLength_ThrowsInvalidDataException()
    {
        var tooLarge = new byte[_settings.MaximumGrantBytes + 1];

        _ = Should.Throw<InvalidDataException>(() => SqliteSecurityGrantCodec.DecodeGrant([], _settings));
        _ = Should.Throw<InvalidDataException>(() => SqliteSecurityGrantCodec.DecodeGrant(tooLarge, _settings));
    }

    /// <summary>Verifies enforcement decoding rejects an empty or over-bound envelope before any parsing begins.</summary>
    [Fact]
    public void DecodeEnforcement_WhenPayloadIsEmptyOrExceedsTheBoundedLength_ThrowsInvalidDataException()
    {
        var tooLarge = new byte[_settings.MaximumEnforcementBytes + 1];

        _ = Should.Throw<InvalidDataException>(() => SqliteSecurityGrantCodec.DecodeEnforcement([], _settings));
        _ = Should.Throw<InvalidDataException>(() => SqliteSecurityGrantCodec.DecodeEnforcement(tooLarge, _settings));
    }

    /// <summary>Verifies complete captured authorization survives strict grant and enforcement reconstruction.</summary>
    [Fact]
    public void Encode_WhenAuthorizationIsCaptured_PreservesEveryPinnedReference()
    {
        var grant = TestGrantFactory.CreateCapturedGrant(DateTimeOffset.UnixEpoch);
        var enforcement = TestGrantFactory.CreateCapturedEnforcement(grant);

        var reconstructedGrant = SqliteSecurityGrantCodec.DecodeGrant(
            SqliteSecurityGrantCodec.EncodeGrant(grant, _settings), _settings);
        var reconstructedEnforcement = SqliteSecurityGrantCodec.DecodeEnforcement(
            SqliteSecurityGrantCodec.EncodeEnforcement(enforcement, _settings), _settings);

        reconstructedGrant.ShouldBe(grant);
        reconstructedEnforcement.ShouldBe(enforcement);
        reconstructedGrant.Authorization.ShouldBe(grant.Authorization);
    }
}
