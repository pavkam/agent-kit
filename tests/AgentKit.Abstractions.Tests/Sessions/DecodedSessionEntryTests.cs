// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies DecodedSessionEntry behavior and contracts.</summary>
public sealed class DecodedSessionEntryTests
{
    [Fact]
    public void Constructor_WhenEntryIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DecodedSessionEntry(null!, Wire()));
        exception.ParamName.ShouldBe("entry");
    }

    [Fact]
    public void Constructor_WhenWireIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DecodedSessionEntry(Entry(), null!));
        exception.ParamName.ShouldBe("wire");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var entry = Entry();
        var wire = Wire();
        var decoded = new DecodedSessionEntry(entry, wire);
        decoded.Entry.ShouldBe(entry);
        decoded.Wire.ShouldBe(wire);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new DecodedSessionEntry(Entry(), Wire());
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
