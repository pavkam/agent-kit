// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations.Tests;

/// <summary>Verifies <see cref="ConversationSessionBoundEvent"/> argument checks and value semantics.</summary>
public sealed class ConversationSessionBoundEventTests
{
    [Fact]
    public void Constructor_WhenSessionIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new ConversationSessionBoundEvent(default, new BranchId(Guid.NewGuid())));

        exception.ParamName.ShouldBe("sessionId");
    }

    [Fact]
    public void Constructor_WhenBranchIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new ConversationSessionBoundEvent(new SessionId(Guid.NewGuid()), default));

        exception.ParamName.ShouldBe("branchId");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_SetsProperties()
    {
        var sessionId = new SessionId(Guid.NewGuid());
        var branchId = new BranchId(Guid.NewGuid());

        var boundEvent = new ConversationSessionBoundEvent(sessionId, branchId);

        boundEvent.SessionId.ShouldBe(sessionId);
        boundEvent.BranchId.ShouldBe(branchId);
        _ = boundEvent.ShouldBeAssignableTo<ConversationEvent>();
    }

    [Fact]
    public void Equals_WhenIdentitiesMatch_ReturnsTrueWithMatchingHashCode()
    {
        var sessionId = new SessionId(Guid.NewGuid());
        var branchId = new BranchId(Guid.NewGuid());
        var first = new ConversationSessionBoundEvent(sessionId, branchId);
        var second = new ConversationSessionBoundEvent(sessionId, branchId);

        first.Equals(second).ShouldBeTrue();
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equals_WhenBranchDiffers_ReturnsFalse()
    {
        var sessionId = new SessionId(Guid.NewGuid());
        var first = new ConversationSessionBoundEvent(sessionId, new BranchId(Guid.NewGuid()));
        var second = new ConversationSessionBoundEvent(sessionId, new BranchId(Guid.NewGuid()));

        first.Equals(second).ShouldBeFalse();
    }
}
