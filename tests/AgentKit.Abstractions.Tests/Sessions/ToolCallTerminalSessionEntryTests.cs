// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

using AgentKit;

using static AgentKit.Abstractions.Tests.Tools.ToolRuntimeTestFixture;

/// <summary>Verifies <see cref="ToolCallTerminalSessionEntry"/> validation.</summary>
public sealed class ToolCallTerminalSessionEntryTests
{
    private static readonly SessionEntryId _entry = new(Guid.Parse("c0000000-0000-0000-0000-000000000003"));
    private static readonly BranchId _branch = new(Guid.Parse("c0000000-0000-0000-0000-000000000002"));

    [Fact]
    public void Constructor_WhenResultCarriesNoContent_RoundTripsProperties()
    {
        var result = RejectedCallResult();
        var entry = new ToolCallTerminalSessionEntry(
            _entry, Address(), Correlation(), _branch, new SessionSequence(4), null, DateTimeOffset.UnixEpoch, new SchemaVersion("1"), result);

        entry.Result.ShouldBeSameAs(result);
        entry.Sequence.ShouldBe(new SessionSequence(4));
    }

    [Fact]
    public void Constructor_WhenResultIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ToolCallTerminalSessionEntry(
            _entry, Address(), Correlation(), _branch, new SessionSequence(1), null, DateTimeOffset.UnixEpoch, new SchemaVersion("1"), null!))
            .ParamName.ShouldBe("result");

    [Fact]
    public void Constructor_WhenResultCarriesContent_ThrowsExactParameter()
    {
        var withContent = AcceptedFixtureResultWithContent();

        Should.Throw<ArgumentException>(() => new ToolCallTerminalSessionEntry(
            _entry, Address(), Correlation(), _branch, new SessionSequence(1), null, DateTimeOffset.UnixEpoch, new SchemaVersion("1"), withContent))
            .ParamName.ShouldBe("result");
    }

    [Fact]
    public void Constructor_WhenAddressDiffersFromTheResult_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ToolCallTerminalSessionEntry(
            _entry, new SessionAddress(TestAgentId, new SessionId(Guid.NewGuid())), Correlation(), _branch, new SessionSequence(1), null,
            DateTimeOffset.UnixEpoch, new SchemaVersion("1"), RejectedCallResult())).ParamName.ShouldBe("address");

    [Fact]
    public void Constructor_WhenCorrelationDiffersFromTheResult_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ToolCallTerminalSessionEntry(
            _entry, Address(), new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), TestRunId, TestTurnId), _branch, new SessionSequence(1), null,
            DateTimeOffset.UnixEpoch, new SchemaVersion("1"), RejectedCallResult())).ParamName.ShouldBe("correlation");

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ToolCallTerminalSessionEntry(
            _entry, Address(), Correlation(), _branch, new SessionSequence(1), null, DateTimeOffset.UnixEpoch, new SchemaVersion("1"), RejectedCallResult());

        (original with { }).ShouldBe(original);
    }

    private static SessionAddress Address() => new(TestAgentId, TestSessionId);

    private static ToolCallResult AcceptedFixtureResultWithContent() => new(
        TestAgentId, TestSessionId, TestRunId, TestTurnId, TestOperationId, CallId, Authorization(), grantId: null, acceptance: null,
        ProviderAlias(), toolId: null, toolVersion: null, effects: null, externalIdempotencyKey: null,
        new ToolCallAdmissionEvidence(CatalogVersion(), 0, new InputFingerprint("sha256:raw")),
        ToolTerminalStatus.UnknownTool,
        [new ToolResultTextContent("text", TextSemantics.Plain, ExtensionData.Empty)],
        new ToolError(ToolErrorKind.Tool, "Unknown tool.", null, null, ExtensionData.Empty),
        SideEffectCertainty.DefinitelyNotPerformed, usage: null, retryable: false,
        RejectedCallResult().Normalization, RejectedCallResult().NormalizationInfo, RejectedCallResult().ProjectionPolicy,
        DateTimeOffset.UnixEpoch, invocationStartedAt: null, DateTimeOffset.UnixEpoch, ExtensionData.Empty);
}
