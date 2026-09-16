// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies SessionEntryDecodeResult behavior and contracts.</summary>
public sealed class SessionEntryDecodeResultTests
{
    [Fact]
    public void SessionEntryDecoded_WhenDecodedIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionEntryDecoded(null!));
        exception.ParamName.ShouldBe("decoded");
    }

    [Fact]
    public void SessionEntryDecoded_With_WhenApplied_ProducesEqualCopy()
    {
        var decoded = new DecodedSessionEntry(Entry(), Wire());
        var original = new SessionEntryDecoded(decoded);
        var copy = original with { };
        copy.ShouldBe(original);
        original.Decoded.ShouldBe(decoded);
    }

    [Fact]
    public void SessionEntryOpaque_WhenWireIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionEntryOpaque(null!));
        exception.ParamName.ShouldBe("wire");
    }

    [Fact]
    public void SessionEntryOpaque_With_WhenApplied_ProducesEqualCopy()
    {
        var wire = Wire();
        var original = new SessionEntryOpaque(wire);
        var copy = original with { };
        copy.ShouldBe(original);
        original.Wire.ShouldBe(wire);
    }

    [Fact]
    public void SessionEntryDecodeRejected_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionEntryDecodeRejected("bad data");
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static MessageSessionEntry Entry() =>
        new MessageSessionEntry(SessionsTestData.EntryId, SessionsTestData.Address(), SessionsTestData.BeforeRun(),
            SessionsTestData.BranchId, new SessionSequence(1), null, DateTimeOffset.UnixEpoch, new SchemaVersion("1"),
            new UserMessage(new MessageId(SessionsTestData.EntryId.Value), SessionsTestData.AgentId, SessionsTestData.SessionId,
                null, SessionsTestData.BranchId, null, null, DateTimeOffset.UnixEpoch, MessageState.Complete,
                [new TextPart("hi", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty));

    private static SessionEntryWireEnvelope Wire() =>
        new(new SessionEntryTypeId("message"), new SchemaVersion("1"), [1, 2, 3]);
}
