// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies the run-admission boundary name and version are stable configuration values.</summary>
public sealed class EngineDurableOperationsTests
{
    [Fact]
    public void RunAdmission_WhenRead_IsTheStableBoundaryName() =>
        EngineDurableOperations.RunAdmission.Value.ShouldBe("agentkit.engine.run_admission");

    [Fact]
    public void RunAdmissionVersion_WhenRead_IsExplicitlyPublished() =>
        EngineDurableOperations.RunAdmissionVersion.Value.ShouldBe("v1");

    [Fact]
    public void All_WhenRead_ContainsEveryJournaledBoundaryExactlyOnce() =>
        EngineDurableOperations.All.ShouldBe([EngineDurableOperations.RunAdmission]);
}
