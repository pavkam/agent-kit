// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Tests;

using System.Diagnostics;
using System.Text;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public sealed class OperationAcceptedCodecBoundaryTests
{
    [Fact]
    public void Constructor_WhenDependencyIsNull_ThrowsExactParameterName()
    {
        Should.Throw<ArgumentNullException>(() => new OperationAcceptedSessionEntryCodec(null!, NullLogger<OperationAcceptedSessionEntryCodec>.Instance)).ParamName.ShouldBe("timeProvider");
        Should.Throw<ArgumentNullException>(() => new OperationAcceptedSessionEntryCodec(TimeProvider.System, null!)).ParamName.ShouldBe("logger");
        Should.Throw<ArgumentNullException>(() => new OperationAcceptedSessionEntryCodec(TimeProvider.System, NullLogger<OperationAcceptedSessionEntryCodec>.Instance, null!)).ParamName.ShouldBe("limits");
    }

    [Fact]
    public void EncodeDecode_WhenInputIsNull_ThrowsBeforeObservation()
    {
        var clock = new CountingClock();
        var logger = new CountingLogger();
        var codec = new OperationAcceptedSessionEntryCodec(clock, logger);
        using var parent = new Activity("accepted.boundary").SetIdFormat(ActivityIdFormat.W3C).Start();
        var started = false;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static s => s.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref o) => o.Name == AgentKitActivityNames.SessionEntryCodec && o.Parent == parent.Context ? ActivitySamplingResult.PropagationData : ActivitySamplingResult.None,
            ActivityStarted = a => started |= a.OperationName == AgentKitActivityNames.SessionEntryCodec && a.ParentSpanId == parent.SpanId && a.TraceId == parent.TraceId,
        };
        ActivitySource.AddActivityListener(listener);
        Should.Throw<ArgumentNullException>(() => codec.Encode(null!)).ParamName.ShouldBe("entry");
        Should.Throw<ArgumentNullException>(() => codec.Decode(null!)).ParamName.ShouldBe("wire");
        clock.Calls.ShouldBe(0); logger.Calls.ShouldBe(0); started.ShouldBeFalse();
    }

    [Fact]
    public void EncodeDecode_WhenPayloadLimitIsExactOrOneByteTighter_RespectsBound()
    {
        var baseline = new OperationAcceptedSessionEntryCodec(TimeProvider.System, NullLogger<OperationAcceptedSessionEntryCodec>.Instance);
        var entry = OperationAcceptedSessionEntryCodecTestData.Entry();
        var wire = baseline.Encode(entry).ShouldBeOfType<SessionEntryEncoded>().Wire;
        var exact = new OperationAcceptedSessionEntryCodec(TimeProvider.System, NullLogger<OperationAcceptedSessionEntryCodec>.Instance, new SessionEntryCodecLimits(wire.Payload.Length, 64, 65_536, 64));
        var tighter = new OperationAcceptedSessionEntryCodec(TimeProvider.System, NullLogger<OperationAcceptedSessionEntryCodec>.Instance, new SessionEntryCodecLimits(wire.Payload.Length - 1, 64, 65_536, 64));
        _ = exact.Encode(entry).ShouldBeOfType<SessionEntryEncoded>();
        _ = exact.Decode(wire).ShouldBeOfType<SessionEntryDecoded>();
        _ = tighter.Encode(entry).ShouldBeOfType<SessionEntryEncodeRejected>();
        _ = tighter.Decode(wire).ShouldBeOfType<SessionEntryDecodeRejected>();
    }

    [Fact]
    public void EncodeDecode_WhenJsonDepthIsExactOrOneLevelTooShallow_RespectsBound()
    {
        var entry = OperationAcceptedSessionEntryCodecTestData.Entry();
        var exact = new OperationAcceptedSessionEntryCodec(TimeProvider.System,
            NullLogger<OperationAcceptedSessionEntryCodec>.Instance,
            new SessionEntryCodecLimits(1_048_576, 64, 65_536, 7));
        var shallow = new OperationAcceptedSessionEntryCodec(TimeProvider.System,
            NullLogger<OperationAcceptedSessionEntryCodec>.Instance,
            new SessionEntryCodecLimits(1_048_576, 64, 65_536, 6));

        var wire = exact.Encode(entry).ShouldBeOfType<SessionEntryEncoded>().Wire;

        _ = exact.Decode(wire).ShouldBeOfType<SessionEntryDecoded>();
        _ = shallow.Encode(entry).ShouldBeOfType<SessionEntryEncodeRejected>();
        _ = shallow.Decode(wire).ShouldBeOfType<SessionEntryDecodeRejected>();
    }

    [Fact]
    public void Decode_WhenIdentityClaimUnknownEscapedNameIsAtRawByteAndCountBound_PreservesOriginalWire()
    {
        var baseline = new OperationAcceptedSessionEntryCodec(TimeProvider.System,
            NullLogger<OperationAcceptedSessionEntryCodec>.Instance);
        var encoded = baseline.Encode(OperationAcceptedSessionEntryCodecTestData.Entry())
            .ShouldBeOfType<SessionEntryEncoded>();
        const string known = "\"valueKind\":\"text\"";
        const string extension = "\"f\\u0075ture\":0,";
        var source = Encoding.UTF8.GetString(encoded.Wire.Payload.AsSpan());
        var position = source.IndexOf(known, StringComparison.Ordinal);
        position.ShouldBeGreaterThanOrEqualTo(0);
        var payload = source.Insert(position, extension);
        var wire = new SessionEntryWireEnvelope(encoded.Wire.TypeId, encoded.Wire.SchemaVersion,
            [.. Encoding.UTF8.GetBytes(payload)]);
        var rawPropertyBytes = Encoding.UTF8.GetByteCount(extension[..^1]);
        var exact = new OperationAcceptedSessionEntryCodec(TimeProvider.System,
            NullLogger<OperationAcceptedSessionEntryCodec>.Instance,
            new SessionEntryCodecLimits(1_048_576, 1, rawPropertyBytes, 24));
        var tighter = new OperationAcceptedSessionEntryCodec(TimeProvider.System,
            NullLogger<OperationAcceptedSessionEntryCodec>.Instance,
            new SessionEntryCodecLimits(1_048_576, 1, rawPropertyBytes - 1, 24));

        var decoded = exact.Decode(wire).ShouldBeOfType<SessionEntryDecoded>();

        decoded.Decoded.Wire.ShouldBeSameAs(wire);
        decoded.Decoded.Wire.Payload.AsSpan().SequenceEqual(wire.Payload.AsSpan()).ShouldBeTrue();
        _ = tighter.Decode(wire).ShouldBeOfType<SessionEntryDecodeRejected>();
    }

    private sealed class CountingClock: TimeProvider { public int Calls { get; private set; } public override long GetTimestamp() => ++Calls; }
    private sealed class CountingLogger: ILogger<OperationAcceptedSessionEntryCodec> { public int Calls { get; private set; } public IDisposable? BeginScope<T>(T state) where T : notnull => null; public bool IsEnabled(LogLevel level) => ++Calls >= 0; public void Log<T>(LogLevel l, EventId e, T s, Exception? x, Func<T, Exception?, string> f) => Calls++; }
}
