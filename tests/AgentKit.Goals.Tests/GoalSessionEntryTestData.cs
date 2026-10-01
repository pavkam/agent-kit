// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Tests;

/// <summary>Builds deterministic goal session entries for codec and projection tests.</summary>
internal static class GoalSessionEntryTestData
{
    internal static GoalCreatedSessionEntry Created(OperationCorrelation? correlation = null)
    {
        var agent = new AgentId(Guid.Parse("a0000000-0000-0000-0000-000000000001"));
        var session = new SessionId(Guid.Parse("b0000000-0000-0000-0000-000000000002"));
        var run = new RunId(Guid.Parse("c0000000-0000-0000-0000-000000000003"));
        var goal = new AgentGoal(
            new GoalId(Guid.Parse("d0000000-0000-0000-0000-000000000004")), null, agent, session, run,
            GoalTestData.Profile.Key, GoalTestData.Profile.Version, new AgentDefinitionRevision(2), GoalStatus.Ready,
            new GoalDefinition("Objective", ["ref-1"], ExtensionData.Empty), new GoalBudget(3, 5, 2), null,
            new VersionToken("1"), GoalTestData.Now, ExtensionData.Empty);
        return new GoalCreatedSessionEntry(
            new SessionEntryId(Guid.Parse("e0000000-0000-0000-0000-000000000005")), new SessionAddress(agent, session),
            correlation ?? new InRunOperationCorrelation(new OperationId(Guid.Parse("f0000000-0000-0000-0000-000000000006")), run, new TurnId(Guid.Parse("a1000000-0000-0000-0000-000000000007"))),
            new BranchId(Guid.Parse("b1000000-0000-0000-0000-000000000008")), new SessionSequence(4), new SessionEntryId(Guid.Parse("c1000000-0000-0000-0000-000000000009")),
            GoalTestData.Now, new SchemaVersion("1"), new GoalRecord(goal, [], [], null, 3, null, null), new IdempotencyKey("create-key"));
    }

    internal static GoalTransitionSessionEntry Transitioned(bool withAttempt = true)
    {
        var created = Created();
        var goal = created.Record.Goal;
        var attempt = new GoalAttempt(
            new GoalAttemptId(Guid.Parse("d1000000-0000-0000-0000-00000000000a")), goal.Id, new AgentId(Guid.Parse("e1000000-0000-0000-0000-00000000000b")),
            new SessionId(Guid.Parse("f1000000-0000-0000-0000-00000000000c")), new RunId(Guid.Parse("a2000000-0000-0000-0000-00000000000d")), 1,
            GoalAttemptStatus.Running, new GoalBudgetReservation(new GoalBudget(3, 5, 0), new BudgetScopeId(Guid.Parse("b2000000-0000-0000-0000-00000000000e"))),
            null, GoalTestData.Now, null);
        var transition = new GoalTransition(
            goal.Id, goal.OwnerAgentId, goal.SessionId, goal.OriginatingRunId, new OperationId(Guid.Parse("c2000000-0000-0000-0000-00000000000f")),
            GoalStatus.Ready, GoalStatus.Active, TransitionActor.Worker, GoalTransitionReason.AttemptStarted, new VersionToken("1"),
            new IdempotencyKey("active-key"), GoalTestData.Now);
        return new GoalTransitionSessionEntry(
            new SessionEntryId(Guid.Parse("d2000000-0000-0000-0000-000000000010")), created.Address, new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("e2000000-0000-0000-0000-000000000011")), null),
            created.BranchId, new SessionSequence(5), created.Id, GoalTestData.Now, new SchemaVersion("1"), transition,
            withAttempt ? new GoalAttemptStart(attempt) : null, null);
    }
}
