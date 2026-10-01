// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

using AgentKit;

using static AgentKit.Abstractions.Tests.Tools.ToolRuntimeTestFixture;

/// <summary>Verifies <see cref="ToolCallAcceptedSessionEntry"/> validation.</summary>
public sealed class ToolCallAcceptedSessionEntryTests
{
    private static readonly SessionEntryId _entry = new(Guid.Parse("c0000000-0000-0000-0000-000000000001"));
    private static readonly BranchId _branch = new(Guid.Parse("c0000000-0000-0000-0000-000000000002"));

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var call = AcceptedCall();
        var entry = new ToolCallAcceptedSessionEntry(
            _entry, Address(), Correlation(), _branch, new SessionSequence(3), null, DateTimeOffset.UnixEpoch, new SchemaVersion("1"), call);

        entry.Call.ShouldBeSameAs(call);
        entry.Id.ShouldBe(_entry);
        entry.Sequence.ShouldBe(new SessionSequence(3));
    }

    [Fact]
    public void Constructor_WhenCallIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ToolCallAcceptedSessionEntry(
            _entry, Address(), Correlation(), _branch, new SessionSequence(1), null, DateTimeOffset.UnixEpoch, new SchemaVersion("1"), null!))
            .ParamName.ShouldBe("call");

    [Fact]
    public void Constructor_WhenAddressDiffersFromTheCall_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ToolCallAcceptedSessionEntry(
            _entry, new SessionAddress(TestAgentId, new SessionId(Guid.NewGuid())), Correlation(), _branch, new SessionSequence(1), null,
            DateTimeOffset.UnixEpoch, new SchemaVersion("1"), AcceptedCall())).ParamName.ShouldBe("address");

    [Fact]
    public void Constructor_WhenCorrelationDiffersFromTheCall_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ToolCallAcceptedSessionEntry(
            _entry, Address(), new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), TestRunId, TestTurnId), _branch, new SessionSequence(1), null,
            DateTimeOffset.UnixEpoch, new SchemaVersion("1"), AcceptedCall())).ParamName.ShouldBe("correlation");

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ToolCallAcceptedSessionEntry(
            _entry, Address(), Correlation(), _branch, new SessionSequence(1), null, DateTimeOffset.UnixEpoch, new SchemaVersion("1"), AcceptedCall());

        (original with { }).ShouldBe(original);
    }

    private static SessionAddress Address() => new(TestAgentId, TestSessionId);
}
