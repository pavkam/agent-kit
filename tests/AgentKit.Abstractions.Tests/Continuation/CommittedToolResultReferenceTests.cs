// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Continuation;

/// <summary>Verifies CommittedToolResultReference behavior and contracts.</summary>
public sealed class CommittedToolResultReferenceTests
{
    private static readonly SessionEntryId _entryId = new(Guid.Parse("e0000000-0000-0000-0000-000000000001"));
    private static readonly ToolCallId _callId = new(Guid.Parse("e0000000-0000-0000-0000-000000000002"));
    private static readonly TurnId _turnId = new(Guid.Parse("e0000000-0000-0000-0000-000000000003"));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Constructor_WhenAnyIdentityIsDefault_ThrowsExactArgumentOutOfRangeException(int invalidMember)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new CommittedToolResultReference(
            invalidMember == 0 ? default : _entryId,
            invalidMember == 1 ? default : _callId,
            invalidMember == 2 ? default : _turnId));
        exception.ParamName.ShouldBe(invalidMember switch
        {
            0 => "sessionEntryId",
            1 => "toolCallId",
            _ => "turnId",
        });
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var reference = new CommittedToolResultReference(_entryId, _callId, _turnId);
        reference.SessionEntryId.ShouldBe(_entryId);
        reference.ToolCallId.ShouldBe(_callId);
        reference.TurnId.ShouldBe(_turnId);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new CommittedToolResultReference(_entryId, _callId, _turnId);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
