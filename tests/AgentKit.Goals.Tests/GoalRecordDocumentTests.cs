// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Tests;

using System.Collections.Immutable;
using System.Text.Json;

/// <summary>Verifies the persisted goal documents round-trip exactly and re-run domain validation on restore.</summary>
public sealed class GoalRecordDocumentTests
{
    private static readonly GoalRecord _shared = FullRecord();

    private static GoalRecord FullRecord()
    {
        var agent = GoalTestData.NewAgent();
        var session = GoalTestData.NewSession();
        var run = GoalTestData.NewRun();
        var extensions = new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("k", new ExtensionValue([.. "1"u8.ToArray()])));
        var goal = new AgentGoal(
            new GoalId(Guid.NewGuid()), new GoalId(Guid.NewGuid()), agent, session, run, GoalTestData.Profile.Key, GoalTestData.Profile.Version,
            new AgentDefinitionRevision(3), GoalStatus.Completed, new GoalDefinition("Objective", ["ref"], extensions), new GoalBudget(4, 5, 1),
            null, new VersionToken("5"), GoalTestData.Now, extensions);
        var outcome = new GoalOutcomeReference(
            run, DelegationStatus.Succeeded, new StructuredGoalResult("Done", extensions),
            [new EvidenceReference("artifact", "a", new ContentHash("sha256:1")), new EvidenceReference("message", "m")],
            new GoalBudgetUsage(1, 2, 3, 4), SideEffectCertainty.PartiallyPerformed);
        var attempt = new GoalAttempt(
            new GoalAttemptId(Guid.NewGuid()), goal.Id, agent, session, run, 1, GoalAttemptStatus.Succeeded,
            new GoalBudgetReservation(new GoalBudget(1, 1, 0), new BudgetScopeId(Guid.NewGuid())), outcome, GoalTestData.Now, GoalTestData.Now.AddMinutes(1));
        var transition = new GoalTransition(
            goal.Id, agent, session, run, GoalTestData.NewOperation(), GoalStatus.Active, GoalStatus.Completed, TransitionActor.Worker,
            GoalTransitionReason.Completed, new VersionToken("4"), new IdempotencyKey("t"), GoalTestData.Now);
        return new GoalRecord(goal, [attempt], [transition], null, 9, 2, 11);
    }

    [Fact]
    public void ToDomain_WhenRoundTrippedThroughJson_RestoresTheExactRecord()
    {
        var record = FullRecord();
        var json = JsonSerializer.Serialize(GoalRecordDocument.FromDomain(record));

        var restored = JsonSerializer.Deserialize<GoalRecordDocument>(json)!.ToDomain(null);

        restored.ShouldBe(record);
    }

    [Fact]
    public void FromDomain_WhenEncodedTwice_ProducesIdenticalDocuments() =>
        JsonSerializer.Serialize(GoalRecordDocument.FromDomain(_shared)).ShouldBe(JsonSerializer.Serialize(GoalRecordDocument.FromDomain(_shared)));

    [Fact]
    public void ToDomain_WhenAStoredTransitionPairIsInvalid_ThrowsRatherThanRestoringIt()
    {
        var document = GoalRecordDocument.FromDomain(FullRecord());
        var corrupt = document with { Transitions = [document.Transitions[0] with { To = GoalStatus.Proposed }] };

        _ = Should.Throw<ArgumentOutOfRangeException>(() => corrupt.ToDomain(null));
    }

    [Fact]
    public void ToDomain_WhenAnExtensionNameRepeats_Throws()
    {
        var entries = ImmutableArray.Create(new GoalExtensionDocument("a", "AQ=="), new GoalExtensionDocument("a", "Ag=="));

        _ = Should.Throw<ArgumentException>(() => GoalExtensionDocument.ToDomain(entries));
    }

    [Fact]
    public void AttemptChangeDocument_WhenRoundTripped_RestoresBothChangeKinds()
    {
        var record = FullRecord();
        var start = new GoalAttemptStart(GoalTestData.Attempt(record.Goal.Id, 1, GoalTestData.NewAgent(), GoalTestData.NewSession(), GoalTestData.NewRun()));
        var settle = new GoalAttemptSettlement(record.Attempts[0].Id, GoalAttemptStatus.Succeeded, record.Attempts[0].Outcome, GoalTestData.Now);

        GoalAttemptChangeDocument.FromDomain(start).ToDomain().ShouldBe(start);
        GoalAttemptChangeDocument.FromDomain(settle).ToDomain().ShouldBe(settle);
    }

    [Fact]
    public void AttemptChangeDocument_WhenNeitherStartNorSettlement_Throws() =>
        Should.Throw<ArgumentException>(() => new GoalAttemptChangeDocument(null, null, null, null, null).ToDomain());
}
