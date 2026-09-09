// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

using AgentKit;

public sealed class SessionEntryWireEnvelopeTests
{
    [Fact]
    public void Constructor_WhenTypeIdOrSchemaIsDefault_ThrowsArgumentOutOfRangeException()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new SessionEntryWireEnvelope(default, Version, [1])).ParamName.ShouldBe("typeId");
        Should.Throw<ArgumentOutOfRangeException>(() => new SessionEntryWireEnvelope(Type, default, [1])).ParamName.ShouldBe("schemaVersion");
    }

    [Fact]
    public void Constructor_WhenPayloadIsDefaultOrEmpty_ThrowsArgumentException()
    {
        ImmutableArray<byte> @default = default;
        Should.Throw<ArgumentException>(() => new SessionEntryWireEnvelope(Type, Version, @default)).ParamName.ShouldBe("payload");
        Should.Throw<ArgumentException>(() => new SessionEntryWireEnvelope(Type, Version, [])).ParamName.ShouldBe("payload");
    }

    [Fact]
    public void Equality_WhenPayloadBytesMatchAcrossArrays_IsStructural()
    {
        var first = new SessionEntryWireEnvelope(Type, Version, [1, 2]);
        var second = new SessionEntryWireEnvelope(Type, Version, [1, 2]);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
        new HashSet<SessionEntryWireEnvelope> { first }.Contains(second).ShouldBeTrue();
    }

    [Fact]
    public void Equality_WhenPayloadBytesDiffer_IsNotEqual() => new SessionEntryWireEnvelope(Type, Version, [1]).ShouldNotBe(new SessionEntryWireEnvelope(Type, Version, [2]));

    [Fact]
    public void Equality_WhenIdentitySchemaOrLengthDiffers_IsNotEqual()
    {
        var envelope = new SessionEntryWireEnvelope(Type, Version, [1]);
        envelope.ShouldNotBe(new SessionEntryWireEnvelope(new SessionEntryTypeId("other"), Version, [1]));
        envelope.ShouldNotBe(new SessionEntryWireEnvelope(Type, new SchemaVersion("other"), [1]));
        envelope.ShouldNotBe(new SessionEntryWireEnvelope(Type, Version, [1, 2]));
        envelope.Equals(null).ShouldBeFalse();
    }

    [Fact]
    public void SuccessWrappers_WhenPayloadContainsArbitraryBytes_RetainExactReferencesAndBytes()
    {
        var wire = new SessionEntryWireEnvelope(Type, Version, [0, 255, 128, 1]);
        var entry = new TestEntry();
        var decoded = new DecodedSessionEntry(entry, wire);

        new SessionEntryEncoded(wire).Wire.ShouldBeSameAs(wire);
        new SessionEntryOpaque(wire).Wire.ShouldBeSameAs(wire);
        new SessionEntryDecoded(decoded).Decoded.ShouldBeSameAs(decoded);
        decoded.Entry.ShouldBeSameAs(entry);
        decoded.Wire.ShouldBeSameAs(wire);
        decoded.Wire.Payload.ShouldBe([0, 255, 128, 1]);
    }

    [Fact]
    public void ResultConstructors_WhenRequiredValueIsNull_ThrowArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => new SessionEntryDecoded(null!)).ParamName.ShouldBe("decoded");
        Should.Throw<ArgumentNullException>(() => new SessionEntryOpaque(null!)).ParamName.ShouldBe("wire");
        Should.Throw<ArgumentNullException>(() => new SessionEntryEncoded(null!)).ParamName.ShouldBe("wire");
        Should.Throw<ArgumentNullException>(() => new DecodedSessionEntry(null!, null!)).ParamName.ShouldBe("entry");
        Should.Throw<ArgumentNullException>(() => new DecodedSessionEntry(new TestEntry(), null!)).ParamName.ShouldBe("wire");
    }

    [Fact]
    public void RejectionConstructor_WhenReasonIsNull_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => new SessionEntryDecodeRejected(null!)).ParamName.ShouldBe("reason");
        Should.Throw<ArgumentNullException>(() => new SessionEntryEncodeRejected(null!)).ParamName.ShouldBe("reason");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void RejectionConstructor_WhenReasonIsBlank_ThrowsArgumentException(string reason)
    {
        Should.Throw<ArgumentException>(() => new SessionEntryDecodeRejected(reason)).ParamName.ShouldBe("reason");
        Should.Throw<ArgumentException>(() => new SessionEntryEncodeRejected(reason)).ParamName.ShouldBe("reason");
    }

    private static readonly SessionEntryTypeId Type = new("agentkit.session.test/v1");
    private static readonly SchemaVersion Version = new("agentkit.session.test/v1");

    private sealed record TestEntry: SessionEntry
    {
        public TestEntry() : base(default, new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid())), new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null), new BranchId(Guid.NewGuid()), new SessionSequence(1), null, DateTimeOffset.UnixEpoch, Version) { }
    }
}
