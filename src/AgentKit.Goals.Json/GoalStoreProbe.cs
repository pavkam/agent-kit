// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Json;

/// <summary>Builds a representative goal snapshot used to prove an encoding contract round-trips goal evidence exactly.</summary>
internal static class GoalStoreProbe
{
    /// <summary>Creates a fully populated probe covering attempts, outcomes, transitions, and a delegation.</summary>
    /// <returns>The probe's tenant, creation key, and aggregate.</returns>
    internal static (TenantId Tenant, string CreateKey, GoalRecord Record) Create()
    {
        var agent = new AgentId(Guid.Parse("a0000000-0000-0000-0000-000000000001"));
        var session = new SessionId(Guid.Parse("b0000000-0000-0000-0000-000000000002"));
        var run = new RunId(Guid.Parse("c0000000-0000-0000-0000-000000000003"));
        var goal = new AgentGoal(
            new GoalId(Guid.Parse("d0000000-0000-0000-0000-000000000004")), null, agent, session, run,
            new GoalProfileKey("probe"), new GoalProfileVersion(1), new AgentDefinitionRevision(1), GoalStatus.Ready,
            new GoalDefinition("probe", ["ref"], ExtensionData.Empty), new GoalBudget(1, 1, 0), null,
            new VersionToken("1"), DateTimeOffset.UnixEpoch, ExtensionData.Empty);
        return (new TenantId("probe"), "probe-key", new GoalRecord(goal, [], [], null, 1, null, null));
    }
}
