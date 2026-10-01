// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Tests;

using System.Text;

/// <summary>Runs the shared codec suite against the goal-created codec and verifies its bounds, rejection, and passthrough behavior.</summary>
public sealed class GoalCreatedSessionEntryCodecTests: SessionEntryCodecConformanceTests<GoalCreatedSessionEntryCodecFixture>
{
    private readonly GoalCreatedSessionEntryCodec _codec = new();

    [Fact]
    public void Descriptor_WhenRead_NamesTheOwnedTypeAndVersionOne()
    {
        _codec.Descriptor.TypeId.ShouldBe(new SessionEntryTypeId("agentkit.goals/goal-created"));
        _codec.Descriptor.EntryType.ShouldBe(typeof(GoalCreatedSessionEntry));
        _codec.Descriptor.WriteVersion.ShouldBe(new SchemaVersion("1"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void EncodeDecode_WhenEveryCorrelationKindIsUsed_RoundTripsTheCorrelation(int kind)
    {
        var operation = new OperationId(Guid.NewGuid());
        OperationCorrelation correlation = kind switch
        {
            0 => new BeforeRunOperationCorrelation(operation, new AdmissionId(Guid.NewGuid())),
            1 => new InRunOperationCorrelation(operation, new RunId(Guid.NewGuid()), null),
            _ => new AfterRunOperationCorrelation(operation, new RunId(Guid.NewGuid())),
        };
        var entry = GoalSessionEntryTestData.Created(correlation);

        var wire = _codec.Encode(entry).ShouldBeOfType<SessionEntryEncoded>().Wire;
        var decoded = _codec.Decode(wire).ShouldBeOfType<SessionEntryDecoded>().Decoded.Entry.ShouldBeOfType<GoalCreatedSessionEntry>();

        decoded.Correlation.ShouldBe(correlation);
    }

    [Fact]
    public void Encode_WhenTheEntryIsAnotherType_IsRejectedWithoutThrowing() =>
        _ = _codec.Encode(GoalSessionEntryTestData.Transitioned()).ShouldBeOfType<SessionEntryEncodeRejected>();

    [Fact]
    public void Decode_WhenTheWireTypeOrVersionDiffers_PassesThroughAsOpaque()
    {
        var wire = _codec.Encode(GoalSessionEntryTestData.Created()).ShouldBeOfType<SessionEntryEncoded>().Wire;

        _ = _codec.Decode(new SessionEntryWireEnvelope(new SessionEntryTypeId("other/type"), wire.SchemaVersion, wire.Payload)).ShouldBeOfType<SessionEntryOpaque>();
        _ = _codec.Decode(new SessionEntryWireEnvelope(wire.TypeId, new SchemaVersion("9"), wire.Payload)).ShouldBeOfType<SessionEntryOpaque>();
    }

    [Fact]
    public void Decode_WhenThePayloadIsMalformedOrViolatesInvariants_IsRejected()
    {
        var typeId = _codec.Descriptor.TypeId;
        var version = _codec.Descriptor.WriteVersion;

        _ = _codec.Decode(new SessionEntryWireEnvelope(typeId, version, [.. Encoding.UTF8.GetBytes("{not json")])).ShouldBeOfType<SessionEntryDecodeRejected>();
        _ = _codec.Decode(new SessionEntryWireEnvelope(typeId, version, [.. Encoding.UTF8.GetBytes("null")])).ShouldBeOfType<SessionEntryDecodeRejected>();
        _ = _codec.Decode(new SessionEntryWireEnvelope(typeId, version, [.. Encoding.UTF8.GetBytes(/*lang=json,strict*/ "{\"unknown\":1}")])).ShouldBeOfType<SessionEntryDecodeRejected>();
    }

    [Fact]
    public void Decode_WhenTheWireIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => _codec.Decode(null!)).ParamName.ShouldBe("wire");

    [Fact]
    public void Encode_WhenTheEntryIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => _codec.Encode(null!)).ParamName.ShouldBe("entry");
}
