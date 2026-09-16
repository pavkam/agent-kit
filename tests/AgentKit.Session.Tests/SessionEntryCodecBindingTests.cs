// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Tests;
/// <summary>Verifies SessionEntryCodecBinding behavior and contracts.</summary>
public sealed class SessionEntryCodecBindingTests
{
    [Fact]
    public void SessionEntryCodecBinding_WhenCodecOrDescriptorIsNull_ThrowsExactArgumentNullException()
    {
        var codec = new FakeCodec();
        Should.Throw<ArgumentNullException>(() => new SessionEntryCodecBinding(null!, codec.Descriptor)).ParamName.ShouldBe("codec");
        Should.Throw<ArgumentNullException>(() => new SessionEntryCodecBinding(codec, null!)).ParamName.ShouldBe("descriptor");
    }

    [Fact]
    public void Equals_WhenCodecAndDescriptorMatch_AreEqual()
    {
        var codec = new FakeCodec();
        var first = new SessionEntryCodecBinding(codec, codec.Descriptor);
        var second = new SessionEntryCodecBinding(codec, codec.Descriptor);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void With_WhenCloned_ProducesAnEquivalentInstance()
    {
        var codec = new FakeCodec();
        var original = new SessionEntryCodecBinding(codec, codec.Descriptor);

        var clone = original with { };

        clone.ShouldBe(original);
    }

    private static readonly SessionEntryTypeId Type = new("agentkit.test/v1");
    private static readonly SchemaVersion Version = new("agentkit.test/v1");
    private sealed class FakeCodec(bool throwOnSecondDescriptorRead = false): ISessionEntryCodec
    {
        public int DescriptorReads { get; private set; }
        public int EncodeCalls { get; private set; }
        public int DecodeCalls { get; private set; }
        public SessionEntryWireEnvelope Wire { get; } = new(Type, Version, [1]);
        public SessionEntryWireEnvelope? EncodeWire { get; init; }
        public bool ReplaceDecodeWire { get; init; }
        public bool ReturnNullEncode { get; init; }
        public bool ReturnNullDecode { get; init; }
        public Exception? EncodeException { get; init; }
        public bool ReturnNullDescriptor { get; init; }
        public SessionEntryCodecDescriptor Descriptor { get => ReturnNullDescriptor ? null! : ++DescriptorReads > 1 && throwOnSecondDescriptorRead ? throw new InvalidOperationException() : field; } = new(Type, typeof(TestEntry), Version, [Version], new SessionEntryCodecLimits(16, 1, 1, 2));

        public SessionEntryEncodeResult Encode(SessionEntry entry)
        {
            EncodeCalls++;
            return EncodeException is { } exception ? throw exception : ReturnNullEncode ? null! : new SessionEntryEncoded(EncodeWire ?? Wire);
        }

        public SessionEntryDecodeResult Decode(SessionEntryWireEnvelope wire)
        {
            DecodeCalls++;
            if (ReturnNullDecode)
            {
                return null!;
            }

            var retained = ReplaceDecodeWire ? new SessionEntryWireEnvelope(Type, Version, [2]) : wire;
            return new SessionEntryDecoded(new DecodedSessionEntry(new TestEntry(), retained));
        }
    }

    private sealed record TestEntry: SessionEntry
    {
        public TestEntry() : base(default, new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid())), new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null), new BranchId(Guid.NewGuid()), new SessionSequence(1), null, DateTimeOffset.UnixEpoch, Version)
        {
        }
    }
}
