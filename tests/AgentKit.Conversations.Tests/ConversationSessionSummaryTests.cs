// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations.Tests;

/// <summary>Verifies ConversationSessionSummary behavior and contracts.</summary>
public sealed class ConversationSessionSummaryTests
{
    [Fact]
    public void Constructor_WhenCalled_SetsAllProperties()
    {
        var sessionId = new SessionId(Guid.NewGuid());
        var storeKey = new SessionStoreKey("sqlite");
        var recordedAt = DateTimeOffset.UnixEpoch;

        var summary = new ConversationSessionSummary(sessionId, storeKey, recordedAt);

        summary.SessionId.ShouldBe(sessionId);
        summary.StoreKey.ShouldBe(storeKey);
        summary.RecordedAt.ShouldBe(recordedAt);
    }

    [Fact]
    public void Equals_WhenAllValuesMatch_ReturnsTrueWithMatchingHashCode()
    {
        var sessionId = new SessionId(Guid.NewGuid());
        var storeKey = new SessionStoreKey("sqlite");
        var recordedAt = DateTimeOffset.UnixEpoch;
        var first = new ConversationSessionSummary(sessionId, storeKey, recordedAt);
        var second = new ConversationSessionSummary(sessionId, storeKey, recordedAt);

        first.Equals(second).ShouldBeTrue();
        first.Equals((object) second).ShouldBeTrue();
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equals_WhenStoreKeyDiffers_ReturnsFalse()
    {
        var sessionId = new SessionId(Guid.NewGuid());
        var recordedAt = DateTimeOffset.UnixEpoch;
        var first = new ConversationSessionSummary(sessionId, new SessionStoreKey("sqlite"), recordedAt);
        var second = new ConversationSessionSummary(sessionId, new SessionStoreKey("in-memory"), recordedAt);

        first.Equals(second).ShouldBeFalse();
    }

    [Fact]
    public void Deconstruct_WhenCalled_ReturnsAllValues()
    {
        var sessionId = new SessionId(Guid.NewGuid());
        var storeKey = new SessionStoreKey("sqlite");
        var recordedAt = DateTimeOffset.UnixEpoch;
        var summary = new ConversationSessionSummary(sessionId, storeKey, recordedAt);

        var (deconstructedSessionId, deconstructedStoreKey, deconstructedRecordedAt) = summary;

        deconstructedSessionId.ShouldBe(sessionId);
        deconstructedStoreKey.ShouldBe(storeKey);
        deconstructedRecordedAt.ShouldBe(recordedAt);
    }

    [Fact]
    public void ToString_WhenCalled_IncludesTypeNameAndValues()
    {
        var summary = new ConversationSessionSummary(
            new SessionId(Guid.NewGuid()), new SessionStoreKey("sqlite"), DateTimeOffset.UnixEpoch);

        var text = summary.ToString();

        text.ShouldContain(nameof(ConversationSessionSummary));
        text.ShouldContain("sqlite");
    }
}
