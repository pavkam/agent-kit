// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies SessionEntryEncodeResult behavior and contracts.</summary>
public sealed class SessionEntryEncodeResultTests
{
    [Fact]
    public void SessionEntryEncoded_WhenWireIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionEntryEncoded(null!));
        exception.ParamName.ShouldBe("wire");
    }

    [Fact]
    public void SessionEntryEncoded_With_WhenApplied_ProducesEqualCopy()
    {
        var wire = Wire();
        var original = new SessionEntryEncoded(wire);
        var copy = original with { };
        copy.ShouldBe(original);
        original.Wire.ShouldBe(wire);
    }

    [Fact]
    public void SessionEntryEncodeRejected_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionEntryEncodeRejected("cannot encode");
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static SessionEntryWireEnvelope Wire() =>
        new(new SessionEntryTypeId("message"), new SchemaVersion("1"), [1, 2, 3]);
}
