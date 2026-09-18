// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

using System.Collections.Immutable;

/// <summary>Verifies <see cref="AgentSendRequest"/> argument checks and value semantics.</summary>
public sealed class AgentSendRequestTests
{
    private static readonly ExecutionIdentity _identity = CompositionTestData.Identity();

    [Fact]
    public void Constructor_WhenIdentityIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new AgentSendRequest(null!, "hi")).ParamName.ShouldBe("identity");

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void Constructor_WhenTextIsBlank_ThrowsArgumentException(string? text) =>
        Should.Throw<ArgumentException>(() => new AgentSendRequest(_identity, text!)).ParamName.ShouldBe("text");

    [Fact]
    public void Constructor_WhenPartsAreEmpty_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new AgentSendRequest(_identity, parts: [])).ParamName.ShouldBe("parts");

    [Fact]
    public void Constructor_WhenPartsContainNull_ThrowsArgumentException()
    {
        ImmutableArray<ContentPart> parts = [null!];

        Should.Throw<ArgumentException>(() => new AgentSendRequest(_identity, parts)).ParamName.ShouldBe("parts");
    }

    [Fact]
    public void Constructor_WhenSessionIdIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new AgentSendRequest(_identity, "hi", sessionId: default(SessionId))).ParamName.ShouldBe("sessionId");

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenMaxTurnsIsNotPositive_ThrowsArgumentOutOfRangeException(int maxTurns) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new AgentSendRequest(_identity, "hi", maxTurns: maxTurns)).ParamName.ShouldBe("maxTurns");

    [Fact]
    public void Constructor_WhenAttemptTimeoutIsNotPositive_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new AgentSendRequest(_identity, "hi", attemptTimeout: TimeSpan.Zero)).ParamName.ShouldBe("attemptTimeout");

    [Fact]
    public void Constructor_WhenTextIsSupplied_ProducesOnePlainTextPart()
    {
        var request = new AgentSendRequest(_identity, "hello");

        request.Parts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("hello");
        request.SessionId.ShouldBeNull();
        request.MaxTurns.ShouldBeNull();
        request.AttemptTimeout.ShouldBeNull();
        request.Observer.ShouldBeNull();
    }

    [Fact]
    public void Equals_WhenOnlyTheObserverDiffers_IsTrue()
    {
        var sessionId = new SessionId(Guid.NewGuid());
        var first = new AgentSendRequest(_identity, "hello", sessionId, 3, TimeSpan.FromSeconds(5), new RecordingRunObserver());
        var second = new AgentSendRequest(_identity, "hello", sessionId, 3, TimeSpan.FromSeconds(5));

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equals_WhenTheSessionDiffers_IsFalse()
    {
        var first = new AgentSendRequest(_identity, "hello", new SessionId(Guid.NewGuid()));
        var second = new AgentSendRequest(_identity, "hello", new SessionId(Guid.NewGuid()));

        first.ShouldNotBe(second);
    }

    private sealed class RecordingRunObserver: IAgentRunObserver
    {
        public ValueTask OnEventAsync(AgentRunEvent runEvent, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}
