// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Tests;

using System.Text;

/// <summary>Runs the shared codec suite against the goal-transition codec and verifies attempt-change and settlement round trips.</summary>
public sealed class GoalTransitionSessionEntryCodecTests: SessionEntryCodecConformanceTests<GoalTransitionSessionEntryCodecFixture>
{
    private readonly GoalTransitionSessionEntryCodec _codec = new();

    [Fact]
    public void EncodeDecode_WhenThereIsNoAttemptChange_RoundTripsWithNone()
    {
        var entry = GoalSessionEntryTestData.Transitioned(withAttempt: false);

        var decoded = _codec.Decode(_codec.Encode(entry).ShouldBeOfType<SessionEntryEncoded>().Wire)
            .ShouldBeOfType<SessionEntryDecoded>().Decoded.Entry.ShouldBeOfType<GoalTransitionSessionEntry>();

        decoded.Attempt.ShouldBeNull();
        decoded.Transition.ShouldBe(entry.Transition);
    }

    [Fact]
    public void EncodeDecode_WhenASettlementCarriesAnOutcome_RoundTripsOutcomeEvidenceAndSettlementSequence()
    {
        var baseline = GoalSessionEntryTestData.Transitioned();
        var goal = GoalSessionEntryTestData.Created().Record.Goal;
        var run = new RunId(Guid.NewGuid());
        var outcome = new GoalOutcomeReference(
            run, DelegationStatus.Succeeded, new StructuredGoalResult("Done", ExtensionData.Empty),
            [new EvidenceReference("artifact", "a-1", new ContentHash("sha256:abc"))], new GoalBudgetUsage(2, 3, 0, 120), SideEffectCertainty.DefinitelyPerformed);
        var transition = new GoalTransition(
            goal.Id, goal.OwnerAgentId, goal.SessionId, run, new OperationId(Guid.NewGuid()), GoalStatus.Active, GoalStatus.Completed,
            TransitionActor.Worker, GoalTransitionReason.Completed, new VersionToken("2"), new IdempotencyKey("done"), GoalTestData.Now);
        var settlement = new GoalAttemptSettlement(new GoalAttemptId(Guid.NewGuid()), GoalAttemptStatus.Succeeded, outcome, GoalTestData.Now.AddMinutes(1));
        var entry = new GoalTransitionSessionEntry(
            baseline.Id, baseline.Address, baseline.Correlation, baseline.BranchId, baseline.Sequence, baseline.CausalParentId,
            baseline.RecordedAt, baseline.SchemaVersion, transition, settlement, 7);

        var decoded = _codec.Decode(_codec.Encode(entry).ShouldBeOfType<SessionEntryEncoded>().Wire)
            .ShouldBeOfType<SessionEntryDecoded>().Decoded.Entry.ShouldBeOfType<GoalTransitionSessionEntry>();

        decoded.Attempt.ShouldBe(settlement);
        decoded.SettledSequence.ShouldBe(7);
    }

    [Fact]
    public void Decode_WhenTheTransitionPairViolatesTheValidityTable_IsRejected()
    {
        var wire = _codec.Encode(GoalSessionEntryTestData.Transitioned()).ShouldBeOfType<SessionEntryEncoded>().Wire;
        var tampered = Encoding.UTF8.GetString(wire.Payload.AsSpan()).Replace("\"to\":\"Active\"", "\"to\":\"Completed\"", StringComparison.Ordinal).Replace("\"from\":\"Ready\"", "\"from\":\"Completed\"", StringComparison.Ordinal);

        _ = _codec.Decode(new SessionEntryWireEnvelope(wire.TypeId, wire.SchemaVersion, [.. Encoding.UTF8.GetBytes(tampered)])).ShouldBeOfType<SessionEntryDecodeRejected>();
    }

    [Fact]
    public void Encode_WhenTheEntryIsAnotherType_IsRejectedWithoutThrowing() =>
        _ = _codec.Encode(GoalSessionEntryTestData.Created()).ShouldBeOfType<SessionEntryEncodeRejected>();
}
